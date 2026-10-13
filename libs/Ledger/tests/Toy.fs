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

/// 计数器不允许为负：用来测试折叠拒绝。错误类型是领域自己的。
type ToyError = Negative of int

let apply (s: int) (c: Commit<Ev>) : Result<int, ToyError> =
    let s' = c.events |> List.fold (fun acc e -> match e with Add n -> acc + n | Reset -> 0) s
    if s' < 0 then Error(Negative s') else Ok s'

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

type L = Ledger<int, Ev, ToyError>

/// 玩具组装：只做消息路由。介质是一串 (起点, 文本)，偏移按文本长度累加。
type Store(?ledger: L) =
    let ledger = defaultArg ledger (L(apply, 0))
    let lines = ResizeArray<int64 * string>()
    let out = ResizeArray<LedgerOut<Ev, ToyError>>()
    let mutable failNext = false
    do
        ledger.Outbox.Add(fun o ->
            lock out (fun () -> out.Add o)
            match o with
            | AppendRequested(_, c, start) ->
                if failNext then
                    failNext <- false
                    ledger.Post(AppendFailed(c.id, "disk full"))
                else
                    let text = Envelope.write (Commit.map writeEv c)
                    lines.Add(start, text)
                    ledger.Post(Appended(c.id, start + int64 text.Length))
            | _ -> ())
    member _.Ledger = ledger
    member _.Lines = lines
    member _.Out = out
    member _.FailNext() = failNext <- true
    member _.Submit(events, ?stamp) =
        match stamp with
        | Some s -> ledger.Ask(events, t0, s)
        | None -> ledger.Ask(events, t0)

/// 把介质重放进一个新账本，返回账本与第一个 ReplayRejected（若有）。
let replay (lines: (int64 * string) seq) =
    let l = L(apply, 0)
    let rejected = ResizeArray()
    l.Outbox.Add(function ReplayRejected(_, sp, p) -> rejected.Add(sp, sprintf "%A" p) | _ -> ())
    for start, line in lines do
        if rejected.Count = 0 then
            let sp = { start = start; ``end`` = start + int64 line.Length }
            match Envelope.read line with
            | Envelope.Parsed cj ->
                match ofJson cj with
                | Ok c -> l.Post(Replay(c, sp))
                | Error e -> rejected.Add(sp, e)
            | other -> rejected.Add(sp, sprintf "%A" other)
    l, Seq.tryHead rejected
