/// 测试用玩具领域：计数器；位置用 int（玩具「存储」就是一个文本列表的下标）。
module Toy

open System
open System.Text.Json.Nodes
open Ledger

type Ev =
    | Add of int
    | Reset

let writeEv = function
    | Add n -> JsonValue.Create n :> JsonNode
    | Reset -> JsonValue.Create "reset" :> JsonNode

let readEv (n: JsonNode) =
    match n.GetValueKind() with
    | Text.Json.JsonValueKind.Number -> Ok(Add(n.GetValue<int>()))
    | Text.Json.JsonValueKind.String when n.GetValue<string>() = "reset" -> Ok Reset
    | _ -> Error "bad toy event"

let ofJson (c: Commit<JsonNode>) : Result<Commit<Ev>, string> =
    match c.events |> List.map readEv |> List.tryPick (function Error e -> Some e | Ok _ -> None) with
    | Some e -> Error e
    | None -> Ok(c |> Commit.map (fun n -> match readEv n with Ok e -> e | Error e -> failwith e))

/// 计数器不允许为负：用来测试折叠拒绝。
let apply (s: int) (c: Commit<Ev>) : Result<int, string> =
    let s' = c.events |> List.fold (fun acc e -> match e with Add n -> acc + n | Reset -> 0) s
    if s' < 0 then Error "negative" else Ok s'

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

/// 玩具组装：只做消息路由。账本要求写入 -> 写进列表 -> 回复 Appended。
/// failNext：让下一次写入回复 AppendFailed。
type Store(?ledger: Ledger<int, Ev, int>) =
    let ledger = defaultArg ledger (Ledger<int, Ev, int>(apply, 0))
    let lines = ResizeArray<string>()
    let out = ResizeArray<LedgerOut<Ev, int>>()
    let mutable failNext = false
    do
        ledger.Outbox.Add(fun o ->
            out.Add o
            match o with
            | AppendRequested(_, c) ->
                if failNext then
                    failNext <- false
                    ledger.Post(AppendFailed(c.id, "disk full"))
                else
                    lines.Add(Envelope.write (Commit.map writeEv c))
                    ledger.Post(Appended(c.id, lines.Count - 1))
            | _ -> ())
    member _.Ledger = ledger
    member _.Lines = lines
    member _.Out = out
    member _.FailNext() = failNext <- true
    /// 提交并返回本 ticket 的最终回复（Committed 或 Refused）。
    member _.Submit(events, ?stamp) =
        let t = Guid.NewGuid()
        ledger.Post(Submit(t, events, t0, stamp))
        out |> Seq.find (function Committed(Some x, _, _) | Refused(x, _) when x = t -> true | _ -> false)

/// 把文本行重放进一个新账本，返回账本与第一个 ReplayRejected（若有）。
let replay (lines: string seq) =
    let l = Ledger<int, Ev, int>(apply, 0)
    let rejected = ResizeArray()
    l.Outbox.Add(function ReplayRejected(_, p, why) -> rejected.Add(p, why) | _ -> ())
    lines |> Seq.iteri (fun i line ->
        if rejected.Count = 0 then
            match Envelope.read line with
            | Envelope.Parsed cj ->
                match ofJson cj with
                | Ok c -> l.Post(Replay(c, i))
                | Error e -> rejected.Add(i, e)
            | other -> rejected.Add(i, sprintf "%A" other))
    l, Seq.tryHead rejected
