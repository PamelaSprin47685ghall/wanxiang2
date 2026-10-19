namespace Wanxiang.Server

open System
open System.Net.Http
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks

/// MCP Streamable HTTP 的一次会话：POST JSON，响应是单个 JSON 或 SSE 流；服务端给的 Mcp-Session-Id 之后每次带上。
type StreamableHttpSession(endpoint: string, extraHeaders: Map<string, string>, timeout: TimeSpan) =

    let http = new HttpClient(Timeout = timeout)
    let mutable sessionId: string option = None

    /// SSE 正文里取最后一个带 id 的 JSON 帧（`data:` 行各自解析）。
    static member PickResponse(body: string) : JsonNode option =
        body.Split('\n')
        |> Array.filter (fun line -> line.StartsWith("data:", StringComparison.Ordinal))
        |> Array.map (fun line -> line.Substring(5).Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.rev
        |> Array.tryPick (fun payload ->
            try
                match JsonNode.Parse payload with
                | :? JsonObject as o ->
                    let mutable idNode: JsonNode = null
                    if o.TryGetPropertyValue("id", &idNode) then Some(o :> JsonNode) else None
                | _ -> None
            with _ -> None)

    member _.SessionId = sessionId

    /// 发一个信封。HTTP 失败、空响应、SSE 里没有响应帧、正文不是 JSON 都返回 Error（文案与旧实现一致）。
    /// 超时与取消以 OperationCanceledException 抛出，由调用方区分。
    member _.Post(envelope: JsonObject, timeoutMs: int, ct: CancellationToken) : Task<Result<JsonNode, string>> =
        task {
            use request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            request.Content <- new StringContent(envelope.ToJsonString(), Encoding.UTF8, "application/json")
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream") |> ignore
            match sessionId with
            | Some sid -> request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sid) |> ignore
            | None -> ()
            for KeyValue(k, v) in extraHeaders do request.Headers.TryAddWithoutValidation(k, v) |> ignore
            use timeoutCts = new CancellationTokenSource(if timeoutMs > 0 then timeoutMs else -1)
            use linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token)
            let! response = http.SendAsync(request, linked.Token)
            let mutable header: seq<string> = null
            if response.Headers.TryGetValues("Mcp-Session-Id", &header) then sessionId <- header |> Seq.tryHead
            let! body = response.Content.ReadAsStringAsync linked.Token
            if not response.IsSuccessStatusCode then
                return Error(sprintf "MCP HTTP %d：%s" (int response.StatusCode) (body.Substring(0, min 200 body.Length)))
            elif String.IsNullOrWhiteSpace body then
                return Error "MCP 响应为空"
            else
                let mediaType = match response.Content.Headers.ContentType with null -> "" | c -> c.MediaType
                if mediaType.Contains "event-stream" then
                    match StreamableHttpSession.PickResponse body with
                    | Some node -> return Ok node
                    | None -> return Error "MCP SSE 响应中没有可用帧"
                else
                    try return Ok(JsonNode.Parse body)
                    with ex -> return Error(sprintf "MCP 响应解析失败：%s" ex.Message)
        }

    interface IDisposable with
        member _.Dispose() = try http.Dispose() with _ -> ()
