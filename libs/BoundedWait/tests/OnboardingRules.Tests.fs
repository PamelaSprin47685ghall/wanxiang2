module OnboardingRulesTests
open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.Sockets
open Xunit
open Wanxiang.UI


let private freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    p

/// 只回一次固定响应的本机 HTTP 端点
let private serveOnce (status: int) (body: string) =
    let port = freePort ()
    let listener = new HttpListener()
    let url = sprintf "http://127.0.0.1:%d" port
    listener.Prefixes.Add(url + "/")
    listener.Start()
    let worker =
        System.Threading.Tasks.Task.Run(fun () ->
            try
                let ctx = listener.GetContext()
                ctx.Response.StatusCode <- status
                let bytes = Text.Encoding.UTF8.GetBytes body
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
                ctx.Response.Close()
            with _ -> ())
    url, (fun () -> listener.Stop(); worker.Wait(1000) |> ignore)

// ---- 旧 OnboardingConfigTests

[<Fact>]
let ``默认模型从探测清单里挑：优先预设推荐的，否则清单第一项`` () =
    let openaiModels = [ "gpt-5"; "gpt-5-mini" ]
    Assert.Equal("gpt-5", OnboardingRules.pickDefaultModel openaiModels [ "zzz"; "gpt-5"; "gpt-4o" ])
    Assert.Equal("aaa", OnboardingRules.pickDefaultModel openaiModels [ "aaa"; "zzz" ])

[<Fact>]
let ``发现本机 Ollama：有响应给出模型清单，没有响应为 None`` () =
    let url, stop = serveOnce 200 """{"object":"list","data":[{"id":"qwen3:8b"},{"id":"llama3.2"}]}"""
    try
        let found = OnboardingRules.discoverOllamaAt (url + "/v1/models") |> Async.AwaitTask |> Async.RunSynchronously
        Assert.Equal(Some [ "qwen3:8b"; "llama3.2" ], found)
    finally
        stop ()
    let none =
        OnboardingRules.discoverOllamaAt (sprintf "http://127.0.0.1:%d/v1/models" (freePort ()))
        |> Async.AwaitTask
        |> Async.RunSynchronously
    Assert.Equal(None, none)
    Assert.Equal<string list>([ "a"; "b" ], OnboardingRules.parseModelIds """{"data":[{"id":"a"},{"id":"b"},{"id":"a"},{"x":1}]}""")
    Assert.Empty(OnboardingRules.parseModelIds "not json")

[<Fact>]
let ``只有主机在本机时才探测本机端口`` () =
    Assert.True(OnboardingRules.canDiscoverLocal "ws://127.0.0.1:8765/ws")
    Assert.True(OnboardingRules.canDiscoverLocal "http://localhost:8765")
    Assert.False(OnboardingRules.canDiscoverLocal "https://wanxiang.example.com")
    Assert.False(OnboardingRules.canDiscoverLocal "")

[<Fact>]
let ``non success status is no discovery`` () =
    let url, stop = serveOnce 500 """{"data":[{"id":"x"}]}"""
    try
        Assert.Equal(None, OnboardingRules.discoverOllamaAt url |> Async.AwaitTask |> Async.RunSynchronously)
    finally
        stop ()
