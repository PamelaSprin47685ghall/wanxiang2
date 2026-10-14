module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let d = GenerationDraft<int>(0)
    let pending = System.Collections.Generic.Queue<GenerationSave<int>>()
    let mutable stale: GenerationSave<int> option = None
    for step in Samples.script do
        let fills = ref 0
        let fill () = fills.Value <- fills.Value + 1; d.Edited()
        let arg () = int (step.Substring(step.IndexOf ':' + 1))
        let note =
            match step with
            | "edit" -> d.Edited(); ""
            | "ok" | "fail" -> string (d.Finish(pending.Dequeue(), (step = "ok")))
            | "okstale" -> string (d.Finish(stale.Value, true))
            | s when s.StartsWith "cat:" -> d.CatalogArrived(arg (), fill); ""
            | s when s.StartsWith "scope:" ->
                if pending.Count > 0 then stale <- Some(pending.Dequeue())
                d.SetScope(s.Substring 6); ""
            | s when s.StartsWith "discard:" -> string (d.Discard(arg (), fill))
            | _ -> if d.CanSave then (pending.Enqueue(d.BeginSave(arg ())); "began") else "blocked"
        sb.Append(sprintf "%s -> %s fills=%d dirty=%b saving=%b canSave=%b baseline=%d | %s" step note fills.Value d.Dirty d.Saving d.CanSave d.Baseline d.Status).Append('\n') |> ignore
    sb.ToString()
