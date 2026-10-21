namespace Wanxiang.Agent

open System
open System.Net.Http
open System.Text.Json.Nodes
open Wanxiang.Core

/// 旧仓库的 ProviderSse 入口：分帧（Sse）、重试与状态码（HttpRetry）、取值（JsonPick）各有权威实现，
/// 这里只做一件自己的事：把限流头（Wire 的 ProviderRateLimit）挂到错误上，交给编排层下发。
module ProviderSse =

    let read (stream: IO.Stream) (ct: Threading.CancellationToken) = Sse.read stream ct

    /// 从 HTTP 响应头读出限流信号（决策 241）。头名不区分大小写，响应头与内容头都看。
    let rateLimitOfResponse (response: HttpResponseMessage) : ProviderRateLimit option =
        if isNull response then
            None
        else
            let lookup (name: string) =
                let mutable values: Collections.Generic.IEnumerable<string> = null

                if not (isNull response.Headers) && response.Headers.TryGetValues(name, &values) then
                    Seq.tryHead values
                elif
                    not (isNull response.Content)
                    && response.Content.Headers.TryGetValues(name, &values)
                then
                    Seq.tryHead values
                else
                    None

            ProviderRateLimit.ofHeaders DateTimeOffset.UtcNow lookup

    /// 非 2xx 抛带状态码的 HttpRequestException；限流 / 额度头随异常带走（Data["RateLimit"]）。
    let raiseForStatus (response: HttpResponseMessage) (body: string) : unit =
        match HttpRetry.statusError response body with
        | None -> ()
        | Some ex ->
            try
                match rateLimitOfResponse response with
                | Some limit -> ex.Data["RateLimit"] <- box limit
                | None -> ()
            with _ ->
                ()

            raise ex

    let sendWithRetry http makeRequest maxRetries ct =
        HttpRetry.sendWithRetry http makeRequest maxRetries ct

    let tryString (node: JsonNode) key = JsonPick.tryString node key
    let tryInt (node: JsonNode) key = JsonPick.tryInt node key
    let tryBool (node: JsonNode) key = JsonPick.tryBool node key
    let tryObject (node: JsonNode) key = JsonPick.tryObject node key
    let tryArray (node: JsonNode) key = JsonPick.tryArray node key
    let schemaOf schema = JsonPick.schemaOf schema
