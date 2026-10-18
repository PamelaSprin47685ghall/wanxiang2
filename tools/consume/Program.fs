/// 仓库外消费者（./x consume 拷到仓库外的临时目录）：只引用交付包 Wanxiang2.LedgerStore
/// （根 SPEC 2；compose/LedgerStore SPEC 14）。
/// 提交一条事件、存快照、关闭后重开，断言启动信息为 Used 1UL；任何一步不符就以非零码退出。
module Program

open System
open System.IO
open System.Text.Json.Nodes
open Ledger
open LedgerStore

/// 计数器领域（与 compose/LedgerStore 的测试相同）：事件与状态都是整数，结果不能为负。
type CounterError = Negative of int

let apply (s: int) (c: Commit<int>) : Result<int, CounterError> =
    let v = s + List.sum c.events
    if v < 0 then Error(Negative v) else Ok v

let codec : Codec<int, int> =
    { encodeEvent = fun n -> JsonValue.Create n :> JsonNode
      decodeEvent = fun j -> try Ok(j.GetValue<int>()) with e -> Error e.Message
      encodeState = fun n -> JsonValue.Create n :> JsonNode
      decodeState = fun j -> try Ok(j.GetValue<int>()) with e -> Error e.Message }

[<EntryPoint>]
let main _ =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang2-consume-data-" + Guid.NewGuid().ToString("N"))
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    do
        use store = new LedgerStore<int, int, CounterError>(dir, apply, 0, codec, SnapshotPolicy.Manual)
        match store.Submit([ 1 ], t0) with
        | Committed c when c.id = 1UL -> printfn "committed id=%d" c.id
        | r -> failwithf "first submit should commit id 1, got %A" r
        match store.SaveSnapshot() with
        | Ok() -> ()
        | Error e -> failwithf "save snapshot: %s" e
    use reopened = new LedgerStore<int, int, CounterError>(dir, apply, 0, codec, SnapshotPolicy.Manual)
    printfn "reopen: startup=%A state=%d lastId=%d" reopened.Startup reopened.State reopened.LastId
    match reopened.Startup with
    | { snapshot = Used 1UL; replayedCommits = 0 } when reopened.State = 1 && reopened.LastId = 1UL ->
        printfn "consume ok: commit and reopen yield Used 1UL"
        0
    | s ->
        eprintfn
            "consume FAIL: expected Used 1UL with state=1 lastId=1, got startup=%A state=%d lastId=%d"
            s
            reopened.State
            reopened.LastId
        1
