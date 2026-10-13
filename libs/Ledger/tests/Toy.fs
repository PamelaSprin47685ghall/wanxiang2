/// 测试用玩具领域：计数器。证明 Ledger 不需要任何真实领域类型。
module Toy

open System
open System.Text.Json.Nodes
open Ledger

type Ev =
    | Add of int
    | Reset

let codec : Codec<Ev> =
    { encode = function
        | Add n -> JsonObject([ Collections.Generic.KeyValuePair("add", JsonValue.Create n :> JsonNode) ]) :> JsonNode
        | Reset -> JsonValue.Create "reset" :> JsonNode
      decode = fun n ->
        match n with
        | :? JsonObject as o when o.ContainsKey "add" -> Ok(Add(o["add"].GetValue<int>()))
        | :? JsonValue as v when v.GetValue<string>() = "reset" -> Ok Reset
        | _ -> Error "bad toy event" }

/// 计数器不允许为负：用来测试折叠拒绝。
let apply (s: int) (c: Commit<Ev>) : Result<int, string> =
    let s' = c.events |> List.fold (fun acc e -> match e with Add n -> acc + n | Reset -> 0) s
    if s' < 0 then Error "negative" else Ok s'

/// 内存日志：偏移 = 记录序号 * 1000 + 长度（只需单调且可回传）。
/// failNextAppend：注入写失败（写一半后抛出）。
type MemLog() =
    let recs = ResizeArray<LogRecord>()
    member val FailNextAppend = false with get, set
    member _.Records = recs
    member _.End = if recs.Count = 0 then 0L else recs[recs.Count - 1].endOffset
    member this.Log : Log =
        { readFrom = fun off -> recs |> Seq.filter (fun r -> r.startOffset >= off) |> List.ofSeq
          append = fun text ->
            let start = this.End
            if this.FailNextAppend then
                this.FailNextAppend <- false
                recs.Add { payload = text.Substring(0, text.Length / 2); complete = false; startOffset = start; endOffset = start + 1L }
                raise (IO.IOException "disk full")
            let r = { payload = text; complete = true; startOffset = start; endOffset = start + int64 text.Length + 2L }
            recs.Add r
            r.endOffset
          truncate = fun off -> recs.RemoveAll(fun r -> r.startOffset >= off) |> ignore }

let openReady (log: Log) (from: Checkpoint<int>) =
    match Ledger.openLog log codec apply (from) with
    | Ready l -> l
    | Damaged d -> failwithf "unexpected damage: %s" d.reason
    | Unsupported v -> failwithf "unexpected version %d" v
