namespace Wanxiang.Server

open System
open System.Collections.Concurrent
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks

/// JSON-RPC 2.0 的信封与结果。错误一律表示成 `{"error":{"message":...}}` 节点，调用方只看一种形状。
module JsonRpc =

    let request (id: int) (method: string) (paramsObj: JsonObject) : JsonObject =
        let o = JsonObject()
        o["jsonrpc"] <- "2.0"
        o["id"] <- JsonNode.op_Implicit id
        o["method"] <- method

        if paramsObj.Count > 0 then
            o["params"] <- paramsObj

        o

    let notification (method: string) (paramsObj: JsonObject) : JsonObject =
        let o = JsonObject()
        o["jsonrpc"] <- "2.0"
        o["method"] <- method

        if paramsObj.Count > 0 then
            o["params"] <- paramsObj

        o

    let errorNode (message: string) : JsonNode =
        let o = JsonObject()
        let e = JsonObject()
        e["message"] <- message
        o["error"] <- e
        o :> JsonNode

    let private prop (o: JsonObject) (k: string) =
        let mutable n: JsonNode = null

        if o.TryGetPropertyValue(k, &n) && not (isNull n) then
            Some n
        else
            None

    /// 响应里的 result；没有 result 时给出 error 的原文。
    let resultOf (response: JsonNode) : Result<JsonNode, string> =
        match response with
        | :? JsonObject as o ->
            match prop o "result", prop o "error" with
            | Some r, _ -> Ok r
            | None, Some e -> Error(e.ToJsonString())
            | None, None -> Error "MCP 响应既无 result 也无 error"
        | _ -> Error "MCP 响应不是 JSON 对象"

    /// 用 id 的 JSON 文本做键：数字 1 和字符串 "1" 是两个不同的 id。
    let idKey (o: JsonObject) : string option =
        prop o "id" |> Option.map (fun n -> n.ToJsonString())

/// 在途请求表：发请求时登记，收到同 id 的响应时完成；对端消失时全部以错误结束。
type PendingCalls() =
    let pending = ConcurrentDictionary<string, TaskCompletionSource<JsonNode>>()

    member _.Count = pending.Count

    /// 登记一个 id，返回它的等待任务。
    member _.Register(id: int) : Task<JsonNode> =
        let tcs =
            TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously)

        pending[(JsonNode.op_Implicit id).ToJsonString()] <- tcs
        tcs.Task

    member _.Forget(id: int) =
        pending.TryRemove((JsonNode.op_Implicit id).ToJsonString()) |> ignore

    /// 交来一条响应；有登记的 id 就完成它，返回是否匹配。
    member _.Complete(response: JsonObject) : bool =
        match JsonRpc.idKey response with
        | Some key ->
            match pending.TryRemove key with
            | true, tcs -> tcs.TrySetResult response
            | _ -> false
        | None -> false

    /// 所有在途请求以同一个错误结束（对端退出、关闭）。
    member _.FailAll(message: string) =
        for kv in pending do
            kv.Value.TrySetResult(JsonRpc.errorNode message) |> ignore

        pending.Clear()

    /// 等待结果；超时或取消时撤销登记并返回错误节点（文案与旧实现一致）。
    member this.Await(id: int, waiting: Task<JsonNode>, timeoutMs: int, ct: CancellationToken) : Task<JsonNode> =
        task {
            let timeout =
                if timeoutMs > 0 then
                    TimeSpan.FromMilliseconds(float timeoutMs)
                else
                    Timeout.InfiniteTimeSpan

            try
                return! waiting.WaitAsync(timeout, ct)
            with
            | :? TimeoutException ->
                this.Forget id
                return JsonRpc.errorNode (sprintf "MCP 调用超时（%d 秒）" (timeoutMs / 1000))
            | :? OperationCanceledException ->
                this.Forget id
                return JsonRpc.errorNode "调用已取消"
        }
