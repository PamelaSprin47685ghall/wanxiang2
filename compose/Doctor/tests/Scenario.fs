module Scenario

// 金样场景：几种数据目录状态下的 doctor 报告（检查名、是否通过、说明文字），以及修复证据包的清单。
// 同一份源码既编进新组合者的测试，也编进旧 Doctor.fs、Store/*.fs 生成金样。
// 随机器变化的部分（临时目录、剩余空间、时间）统一替换。

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Store
open Wanxiang.Server

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

let gid (n: int) =
    Guid(sprintf "00000000-0000-0000-0000-%012d" n)

let cfg =
    {
        provider = "p"
        model = "m"
        instructions = None
        tools = []
        temperature = None
        topP = None
        maxTokens = None
        thinkingBudget = None
        extraJson = None
    }

let attachmentMessage (sha: string) =
    JsonNode.Parse(
        sprintf
            """{"role":"user","contents":[{"type":"attachment","sha256":"%s","mediaType":"text/plain","fileName":"a.txt","size":1}]}"""
            sha
    )

let norm (dir: string) (s: string) =
    let s = s.Replace(dir, "<dir>")
    let s = Regex.Replace(s, "\\d+ MiB free", "<n> MiB free")

    let s =
        Regex.Replace(s, "\\d{4}-\\d\\d-\\d\\dT\\d\\d[-:]\\d\\d[-:]\\d\\d(\\.\\d+)?Z", "<time>")

    s

let report (dir: string) (d: Doctor.Diagnosis) =
    [ for c in d.checks -> norm dir (sprintf "%s|%b|%s" c.name c.ok c.detail) ]
    @ [
        sprintf "healthy %b" (Doctor.isHealthy d)
        sprintf "snapshot %s" d.snapshotStatus
        norm dir (sprintf "replayError %s" d.replayError)
        norm dir (sprintf "configErrors %A" d.configErrors)
    ]

let all () =
    let out = ResizeArray<string>()

    let newDir () =
        Path.Combine(Path.GetTempPath(), "wx-doc-" + Guid.NewGuid().ToString("N"))
    // 1. 健康：一个对话，一条消息引用了已存在的附件，另一条引用了缺失的；目录里还有一个孤儿附件
    let dir = newDir ()
    let configPath = Path.Combine(dir, "config.toml")
    Directory.CreateDirectory dir |> ignore

    let appCfg =
        { AppConfig.defaults (gid 1) with
            listen = "127.0.0.1:1"
            providers =
                Map.ofList
                    [
                        "x",
                        {
                            id = "x"
                            kind = "openai"
                            label = "X"
                            baseUrl = "http://x"
                            apiKey = None
                            models = [ "m" ]
                            defaultModel = "m"
                            timeoutSeconds = 60
                            maxRetries = 0
                            enabled = true
                            promptCaching = false
                            headers = Map.empty
                            extraJson = None
                            visionOverrides = Map.empty
                        }
                    ]
            mcpServers =
                Map.ofList
                    [
                        "s",
                        {
                            id = "s"
                            label = "s"
                            enabled = true
                            command = Some "/no/such/cmd"
                            args = []
                            env = Map.empty
                            url = None
                            maxConcurrency = None
                            callTimeoutSeconds = 5
                        }
                    ]
        }

    File.WriteAllText(configPath, TomlCodec.serialize appCfg)
    let att = AttachmentStore(dir, 1000L)

    let put (bytes: byte[]) =
        let sha =
            Convert.ToHexString(Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant()

        let id = Guid.NewGuid()
        att.Begin(1, id, int64 bytes.Length, sha, "text/plain", "f") |> ignore
        att.AppendChunk(id, 0, Convert.ToBase64String bytes) |> ignore
        att.Complete(id, sha) |> ignore
        sha

    let present = put "present"B
    let _orphan = put "orphan"B
    att.Dispose()

    do
        use s = new EventStore(dir)

        s.Submit
            {
                events =
                    [
                        ConversationCreated
                            {
                                conversationId = gid 2
                                title = "t"
                                config = cfg
                            }
                    ]
                commandId = None
                commandType = None
                commandHash = None
                nowUtc = Some t0
            }
        |> ignore

        s.Submit
            {
                events =
                    [
                        AgentMessageRecorded
                            {
                                conversationId = gid 2
                                payloadJson = attachmentMessage present
                            }
                    ]
                commandId = None
                commandType = None
                commandHash = None
                nowUtc = Some t0
            }
        |> ignore

        s.Submit
            {
                events =
                    [
                        AgentMessageRecorded
                            {
                                conversationId = gid 2
                                payloadJson = attachmentMessage (String('e', 64))
                            }
                    ]
                commandId = None
                commandType = None
                commandHash = None
                nowUtc = Some t0
            }
        |> ignore

    out.Add "== healthy"
    out.AddRange(report dir (Doctor.run (configPath, dir)))

    out.Add(
        (Doctor.toChecksJson (fun s -> s.Replace("/no/such/cmd", "***")) (Doctor.run (configPath, dir))
         |> fun a -> norm dir (a.ToJsonString()))
    )
    // 2. 主机内（live）
    out.Add "== live"
    out.AddRange(report dir (Doctor.runLive (configPath, dir, fun () -> Inspect.run dir)))
    // 3. 日志尾部损坏 + 证据包
    let log = DataPaths.logFile dir
    File.AppendAllText(log, "\u001e{\"broken")
    out.Add "== damaged"
    out.AddRange(report dir (Doctor.run (configPath, dir)))

    match Inspect.run dir with
    | RepairRequired(_, plan) ->
        match RecoveryBundle.create dir plan with
        | Ok bundle ->
            let manifest =
                JsonNode.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")))

            manifest["createdAt"] <- "<time>"
            out.Add(norm dir (manifest.ToJsonString()))
            out.Add(norm dir (File.ReadAllText(Path.Combine(bundle, "diagnosis.json"))))
        | Error e -> out.Add e
    | other -> out.Add(sprintf "unexpected %A" other)

    out.Add(sprintf "%A" (Repair.repair dir |> Result.map (List.map (norm dir))))
    out.Add "== repaired"
    out.AddRange(report dir (Doctor.run (configPath, dir)))
    // 4. 旧版日志目录 + 配置缺失
    Directory.CreateDirectory(DataPaths.legacyEventsDir dir) |> ignore
    File.WriteAllText(Path.Combine(DataPaths.legacyEventsDir dir, "2025-01-01.jsonseq"), "")
    File.Delete configPath
    out.Add "== legacy + no config"
    out.AddRange(report dir (Doctor.run (configPath, dir)))

    try
        Directory.Delete(dir, true)
    with _ ->
        ()

    String.concat "\n" out
