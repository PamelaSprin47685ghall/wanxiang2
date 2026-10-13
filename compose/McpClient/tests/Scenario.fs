module Scenario

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open Wanxiang.Config
open Wanxiang.Server

let cfg id =
    { id = id; label = id; enabled = true; command = None; args = []; env = Map.empty; url = None; maxConcurrency = None; callTimeoutSeconds = 5 }

/// stdio：脚本把收到的每行原样记到文件，按 method 回答。
let stdio () =
    let log = Path.GetTempFileName()
    let script =
        String.concat "\n"
            [ "while IFS= read -r line; do"
              sprintf "  printf '%%s\\n' \"$line\" >> '%s'" log
              "  id=$(printf '%s' \"$line\" | sed -n 's/.*\"id\":\\([0-9]*\\).*/\\1/p')"
              "  case \"$line\" in"
              "    *'\"initialize\"'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{}}\" ;;"
              "    *'tools/list'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{\\\"tools\\\":[{\\\"name\\\":\\\"b\\\",\\\"description\\\":\\\"乙\\\",\\\"inputSchema\\\":{\\\"type\\\":\\\"object\\\"}},{\\\"name\\\":\\\"a\\\"},{\\\"description\\\":\\\"no name\\\"}]}}\" ;;"
              "    *'\"ok\"'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"result\\\":{\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"好\\\"}]}}\" ;;"
              "    *'tools/call'*) echo \"{\\\"jsonrpc\\\":\\\"2.0\\\",\\\"id\\\":$id,\\\"error\\\":{\\\"code\\\":-1,\\\"message\\\":\\\"坏了\\\"}}\" ;;"
              "  esac"
              "done" ]
    let c = new McpClient({ cfg "s" with command = Some "/bin/sh"; args = [ "-c"; script ]; env = Map.ofList [ "WX_SECRET_TEST", "v" ] }, ignore)
    let tools = c.Tools() |> List.map (fun t -> sprintf "%s|%s|%s" t.name t.description (t.inputSchema |> Option.map (fun s -> s.ToJsonString()) |> Option.defaultValue "-"))
    let ok = c.Call("t", """{"q":"ok"}""", CancellationToken.None).Result
    let bad = c.Call("t", """{"q":"no"}""", CancellationToken.None).Result
    let invalid = c.Call("t", "{oops", CancellationToken.None).Result
    let empty = c.Call("t", "", CancellationToken.None).Result
    c.Stop()
    let lines = File.ReadAllLines log
    [ yield "== stdio sent"; yield! lines
      yield "== stdio tools"; yield! tools
      yield "== stdio results"; yield ok; yield bad; yield invalid; yield empty ]

let freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    p

/// HTTP：记录每个请求的头和正文；tools/call 用 SSE 回答。
let http () =
    let port = freePort ()
    let url = sprintf "http://127.0.0.1:%d/mcp/" port
    let listener = new HttpListener()
    listener.Prefixes.Add url
    listener.Start()
    let seen = Collections.Concurrent.ConcurrentQueue<string>()
    let _worker =
        Threading.Tasks.Task.Run(fun () ->
            try
                while listener.IsListening do
                    let ctx = listener.GetContext()
                    use reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)
                    let body = reader.ReadToEnd()
                    let hs = [ for k in [ "Accept"; "Content-Type"; "Mcp-Session-Id"; "X-Token" ] -> k + "=" + (match ctx.Request.Headers[k] with null -> "-" | v -> v) ] |> String.concat ";"
                    seen.Enqueue(hs + "\n" + body)
                    let id = (try (let n = Text.Json.Nodes.JsonNode.Parse body in n["id"].ToJsonString()) with _ -> "")
                    let reply, ctype =
                        if body.Contains "\"initialize\"" then sprintf """{"jsonrpc":"2.0","id":%s,"result":{}}""" id, "application/json"
                        elif body.Contains "tools/call" then sprintf "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"progress\"}\n\ndata: {\"jsonrpc\":\"2.0\",\"id\":%s,\"result\":{\"v\":\"甲\"}}\n\n" id, "text/event-stream"
                        elif body.Contains "tools/list" then sprintf """{"jsonrpc":"2.0","id":%s,"result":{"tools":[]}}""" id, "application/json"
                        else "", "application/json"
                    if body.Contains "\"initialize\"" then ctx.Response.Headers["Mcp-Session-Id"] <- "sess-1"
                    ctx.Response.ContentType <- ctype
                    let bytes = Encoding.UTF8.GetBytes reply
                    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
                    ctx.Response.Close()
            with _ -> ())
    let c = new McpClient({ cfg "h" with url = Some url; env = Map.ofList [ "X-Token", "tk" ] }, ignore)
    let tools = c.Tools() |> List.length
    let r = c.Call("t", """{"q":1}""", CancellationToken.None).Result
    c.Stop()
    listener.Stop()
    [ yield "== http sent"; yield! seen; yield sprintf "== http tools %d" tools; yield "== http result"; yield r ]

let all () = String.concat "\n" (stdio () @ http ())
