module HighlightQueueRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let q = HighlightQueue<string>()
    for act, key in HighlightQueueSamples.script do
        let r =
            match act with
            | "claim" -> string (q.TryClaim key)
            | "ok" -> string (q.Finished(key, true))
            | "fail" -> string (q.Finished(key, false))
            | _ -> q.NotifyFired(); "-"
        sb.Append(sprintf "%s %s -> %s inflight=%d" act key r q.InFlightCount).Append('\n') |> ignore
    sb.Append(HighlightQueue<string>.NotifyWindowMs).Append('\n') |> ignore
    sb.ToString()
