/// 测试用玩具领域：计数器。区间是测试直接给出的事实输入，不模拟任何存储对象的流程。
module Toy

open System
open System.Text.Json.Nodes
open Ledger

type Ev =
    | Add of int
    | Reset

let writeEv =
    function
    | Add n -> JsonValue.Create n :> JsonNode
    | Reset -> JsonValue.Create "reset" :> JsonNode

let readEv (n: JsonNode) =
    match n.GetValueKind() with
    | Text.Json.JsonValueKind.Number -> Ok(Add(n.GetValue<int>()))
    | Text.Json.JsonValueKind.String when n.GetValue<string>() = "reset" -> Ok Reset
    | _ -> Error "bad toy event"

let ofJson (c: Commit<JsonNode>) : Result<Commit<Ev>, string> =
    match
        c.events
        |> List.map readEv
        |> List.tryPick (function
            | Error e -> Some e
            | Ok _ -> None)
    with
    | Some e -> Error e
    | None ->
        Ok(
            c
            |> Commit.map (fun n ->
                match readEv n with
                | Ok e -> e
                | Error e -> failwith e)
        )

/// 计数器不允许为负：用来测试折叠拒绝。错误类型是领域自己的。
type ToyError = Negative of int

let apply (s: int) (c: Commit<Ev>) : Result<int, ToyError> =
    let s' =
        c.events
        |> List.fold
            (fun acc e ->
                match e with
                | Add n -> acc + n
                | Reset -> 0)
            s

    if s' < 0 then Error(Negative s') else Ok s'

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

type L = Ledger<int, Ev, ToyError>

let sp a b = { start = a; ``end`` = b }

/// 提议并以「已持久化在 [tail, tail+len)」这一事实记入；返回提交。
let commit (l: L) (len: int64) (events: Ev list) (stamp: Stamp option) =
    match
        (match stamp with
         | Some s -> l.Propose(events, t0, s)
         | None -> l.Propose(events, t0))
    with
    | Proposed c ->
        match l.Record(c, sp l.Tail (l.Tail + len)) with
        | Ok() -> c
        | Error p -> failwithf "%O" p
    | d -> failwithf "expected Proposed, got %A" d
