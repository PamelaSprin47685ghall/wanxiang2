module Scenario

// 新组合与旧 BuiltinTools.fs、ToolRegistry.fs 共用的场景；输出逐字节比较。
// 用固定目录，路径不需要归一化；MCP 服务是一段 /bin/sh 脚本。

open System
open System.IO
open System.Text
open System.Threading
open Microsoft.Extensions.AI
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Server

let private mcpScript =
    String.concat "\n"
                [ "while IFS= read -r line; do"
                  "  id=$(printf '%s' \"$line\" | sed -n 's/.*\"id\":\\([0-9]*\\).*/\\1/p')"
                  "  case \"$line\" in"
                  "    *'\"initialize\"'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{}}\" ;;"
                  "    *'tools/list'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{\\\"tools\\\":[{\\\"name\\\":\\\"b\\\",\\\"description\\\":\\\"乙\\\",\\\"inputSchema\\\":{\\\"type\\\":\\\"object\\\"}},{\\\"name\\\":\\\"a\\\"},{\\\"description\\\":\\\"no name\\\"}]}}\" ;;"
                  "    *'\"ok\"'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"好\\\"}]}}\" ;;"
                  "    *'tools/call'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"error\\\":{\\\"code\\\":-1,\\\"message\\\":\\\"坏了\\\"}}\" ;;"
                  "  esac"
                  "done" ]

let all () =
    let root = "/tmp/wx-tools-golden"
    if Directory.Exists root then Directory.Delete(root, true)
    Directory.CreateDirectory(Path.Combine(root, "sub")) |> ignore
    File.WriteAllText(Path.Combine(root, "a.txt"), "你好")
    File.WriteAllBytes(Path.Combine(root, "big.bin"), Array.zeroCreate (1024 * 1024 + 1))
    let b = StringBuilder()
    let line (s: string) = b.AppendLine s |> ignore
    let invoke (t: AITool) (args: (string * obj) list) =
        let f = t :?> AIFunction
        let a = AIFunctionArguments()
        for (k, v) in args do a[k] <- v
        string (f.InvokeAsync(a, CancellationToken.None).AsTask().Result)
    let mcp id enabled =
        { id = id; label = id; enabled = enabled; command = Some "/bin/sh"; args = [ "-c"; mcpScript ]; env = Map.empty; url = None; maxConcurrency = None; callTimeoutSeconds = 5 }
    let mutable cfg =
        { AppConfig.defaults Guid.Empty with
            tools = { ToolsConfig.defaults with fileReadRoots = [ root ] }
            mcpServers = Map [ "s", mcp "s" true; "off", mcp "off" false ] }
    let reg = ToolRegistry((fun () -> cfg), (fun m -> line ("log " + m)))
    line (reg.DescriptorsJson().ToJsonString())
    let session = { SessionConfig.empty with tools = [ "builtin:echo"; "builtin:time"; "builtin:file.read"; "builtin:file.list"; "mcp:s/b"; "mcp:s/zz"; "mcp:off/a"; "mcp:bad"; "builtin:nope" ] }
    let tools = reg.BuildTools session
    for t in tools do
        let f = t :?> AIFunction
        line (sprintf "tool %s | %s | %s | %s" t.Name (reg.StableIdOf t) f.Description (f.JsonSchema.GetRawText()))
    line (sprintf "missing %A" (reg.MissingTools session))
    let byName n = tools |> List.find (fun t -> t.Name = n)
    line (invoke (byName "builtin_echo") [ "other", box "o"; "text", box "回声" ])
    line (invoke (byName "builtin_file_read") [ "path", box (Path.Combine(root, "a.txt")) ])
    line (invoke (byName "builtin_file_read") [ "path", box (Path.Combine(root, "big.bin")) ])
    line (invoke (byName "builtin_file_read") [ "path", box (Path.Combine(root, "none.txt")) ])
    line (invoke (byName "builtin_file_read") [ "path", box "/etc/hostname" ])
    // 目录枚举顺序取决于文件系统（旧实现也不排序），这里排序后再比较
    line ((invoke (byName "builtin_file_list") [ "dir", box root ]).Split('\n') |> Array.sortWith (fun x y -> String.CompareOrdinal(x, y)) |> String.concat "\n")
    line (invoke (byName "builtin_file_list") [ "path", box (Path.Combine(root, "sub")) ])
    line (invoke (byName "mcp_s_b") [ "q", box "ok"; "n", box 3 ])
    line (sprintf "%s %s" (BuiltinTools.functionName "builtin:file.read") (BuiltinTools.stableId "builtin_file_list"))
    line (sprintf "%A" (BuiltinTools.resolveInRoots [ root ] ""))
    // 换配置：s 停用，调试工具可见、文件根清空
    let c1 = reg.GetMcpClient "s"
    cfg <- { cfg with mcpServers = Map [ "s", { mcp "s" true with callTimeoutSeconds = 6 } ]; tools = { cfg.tools with exposeDebug = true; fileReadRoots = [] } }
    let c2 = reg.GetMcpClient "s"
    line (sprintf "refreshed %b" (not (Object.ReferenceEquals(c1.Value, c2.Value))))
    line (sprintf "same %b" (Object.ReferenceEquals(c2.Value, (reg.GetMcpClient "s").Value)))
    line (reg.DescriptorsJson().ToJsonString())
    line (sprintf "missing %A" (reg.MissingTools session))
    cfg <- { cfg with mcpServers = Map.empty }
    reg.PruneRemoved()
    line (sprintf "after prune %b" (reg.GetMcpClient "s").IsNone)
    reg.Dispose()
    b.ToString()
