namespace Wanxiang.Agent

open System
open System.Collections.Generic
open System.Net
open System.Net.Http

/// 按（键、超时、附加请求头）共享 HttpClient：连接池复用，参数变了就另建一个。只依赖 BCL。线程安全。
/// 从旧 AgentRuntime 的 ProviderHttp 拆出，行为不变（旧客户端不释放，与旧实现相同）。
type SharedHttpClients() =
    let clients = Dictionary<string, HttpClient>()
    let gate = obj ()

    /// 流式响应可能远超单次请求超时，HttpClient.Timeout 取 timeoutSeconds 的 10 倍；首字节超时归调用方的传输。
    member _.Get(key: string, timeoutSeconds: int, headers: Map<string, string>) : HttpClient =
        let fp =
            let hs = headers |> Map.toList |> List.map (fun (k, v) -> k + "=" + v) |> String.concat ";"
            sprintf "%s|%d|%s" key timeoutSeconds hs
        lock gate (fun () ->
            match clients.TryGetValue fp with
            | true, existing -> existing
            | _ ->
                let handler = new SocketsHttpHandler()
                handler.PooledConnectionLifetime <- TimeSpan.FromMinutes 5.0
                handler.AutomaticDecompression <- DecompressionMethods.All
                let client = new HttpClient(handler)
                client.Timeout <- TimeSpan.FromSeconds(float timeoutSeconds * 10.0)
                for KeyValue(name, value) in headers do
                    try client.DefaultRequestHeaders.TryAddWithoutValidation(name, value) |> ignore with _ -> ()
                clients[fp] <- client
                client)

    /// 进程共享的一份。
    static member val Shared = SharedHttpClients()
