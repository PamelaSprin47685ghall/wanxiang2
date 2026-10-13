/// 测试用玩具领域：计数器；位置用 int（玩具「存储」就是一个列表的下标）。
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

/// 计数器不允许为负：用来测试折叠拒绝。
let apply (s: int) (c: Commit<Ev>) : Result<int, string> =
    let s' = c.events |> List.fold (fun acc e -> match e with Add n -> acc + n | Reset -> 0) s
    if s' < 0 then Error "negative" else Ok s'

/// 把 JSON 形式的提交转回领域提交（任一事件读不懂即失败）。
let ofJson (c: Commit<JsonNode>) : Result<Commit<Ev>, string> =
    let evs = c.events |> List.map readEv
    match evs |> List.tryPick (function Error e -> Some e | Ok _ -> None) with
    | Some e -> Error e
    | None -> Ok(c |> Commit.map (fun n -> match readEv n with Ok e -> e | Error e -> failwith e))

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

/// 玩具组装：账本 + 文本列表。这就是消费方要写的那几行。
type Store() =
    member val Lines = ResizeArray<string>()
    member val Ledger = Ledger<int, Ev, int>(apply, 0)
    member this.Submit(events, ?stamp) =
        let d = match stamp with Some s -> this.Ledger.Decide(events, t0, s) | None -> this.Ledger.Decide(events, t0)
        match d with
        | Accept c ->
            this.Lines.Add(Envelope.write (Commit.map writeEv c))
            this.Ledger.Record(c, this.Lines.Count - 1) |> Result.map (fun () -> d)
        | other -> Ok other
