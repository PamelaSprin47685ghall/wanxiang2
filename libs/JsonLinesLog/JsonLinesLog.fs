namespace Wanxiang.Server

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes

/// 结构化 JSON Lines 日志：每条一行，公共字段 level、event、utc，可选 instanceId。
/// 写出前把已登记的密钥原值精确替换成 ***；最近的错误行留在有上限的环形缓冲里。
/// 不懂具体事件。输出去向由构造参数决定。线程安全。
type JsonLinesLog(emit: string -> unit, now: unit -> DateTimeOffset, recentLimit: int) =
    let options = JsonSerializerOptions(JsonSerializerDefaults.General)
    let secretLock = obj ()
    let mutable secrets: string list = []
    let mutable sorted: string list = []
    let recent = Queue<string>()
    let recentLock = obj ()
    let idLock = obj ()
    let mutable instanceId: string option = None

    member _.ApplyInstanceId(id: string) =
        lock idLock (fun () -> instanceId <- if String.IsNullOrEmpty id then None else Some id)

    /// 登记密钥原值（幂等；空白和短于 8 个字符的忽略）。
    member _.RegisterSecrets(values: string seq) =
        lock secretLock (fun () ->
            let existing = set secrets

            secrets <-
                secrets
                @ (values
                   |> Seq.filter (fun s -> not (String.IsNullOrWhiteSpace s) && s.Length >= 8)
                   |> Seq.filter (fun s -> not (existing.Contains s))
                   |> Seq.toList)

            sorted <- secrets |> List.sortByDescending (fun x -> x.Length))

    member _.ClearSecrets() =
        lock secretLock (fun () ->
            secrets <- []
            sorted <- [])

    /// 精确替换已登记的密钥；长的先换，免得短密钥是长密钥子串时破坏匹配。
    member _.Redact(text: string) : string =
        lock secretLock (fun () ->
            let mutable s = text

            for secret in sorted do
                if s.Contains secret then
                    s <- s.Replace(secret, "***")

            s)

    member private _.Envelope(level: string, event: string, o: JsonObject) =
        o["level"] <- level
        o["event"] <- event
        o["utc"] <- now().UtcDateTime.ToString("o")

        lock idLock (fun () ->
            match instanceId with
            | Some id -> o["instanceId"] <- id
            | None -> ())

    /// 错误事件。字段值按类型写入；字符串和异常文本先脱敏，异常另写一个 <key>.stack 字段。
    member this.Error(event: string, fields: (string * obj) list) =
        let o = JsonObject()
        this.Envelope("error", event, o)

        for (k, v) in fields do
            match v with
            | :? string as s -> o[k] <- this.Redact s
            | :? int as i -> o[k] <- i
            | :? int64 as l -> o[k] <- l
            | :? uint64 as u -> o[k] <- u
            | :? float as f -> o[k] <- f
            | :? bool as b -> o[k] <- b
            | :? JsonNode as n -> o[k] <- n.DeepClone()
            | :? exn as e ->
                o[k] <- this.Redact(sprintf "%s: %s" (e.GetType().Name) e.Message)
                o[sprintf "%s.stack" k] <- this.Redact(e.ToString())
            | null -> o[k] <- null
            | other -> o[k] <- this.Redact(string other)

        let line = o.ToJsonString options

        lock recentLock (fun () ->
            recent.Enqueue line

            while recent.Count > recentLimit do
                recent.Dequeue() |> ignore)

        emit line

    member this.Info(message: string) =
        let o = JsonObject()
        this.Envelope("info", "info", o)
        o["message"] <- this.Redact message
        emit (o.ToJsonString options)

    member _.RecentErrors: string list = lock recentLock (fun () -> recent |> Seq.toList)
