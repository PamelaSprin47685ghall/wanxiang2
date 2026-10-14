module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染（步进 200ms，旧 MotionLedger.toastDismissTick）
let render () =
    let sb = StringBuilder()
    for ms, action in Samples.dwells do
        let d = ToastDwell(ms, action)
        let trace = ResizeArray<string>()
        let mutable expiredAt = -1
        Samples.script |> List.iteri (fun i step ->
            match step with
            | "t" -> if d.Tick 200.0 && expiredAt < 0 then expiredAt <- i
            | "in" -> d.PointerEntered()
            | "out0" -> d.PointerExited false
            | "out1" -> d.PointerExited true
            | "f" -> d.ActionFocused()
            | _ -> d.ActionUnfocused()
            if i < 12 then trace.Add(sprintf "%s:%g%s" step d.RemainingMs (if d.Paused then "p" else "")))
        sb.Append(sprintf "dwell %g action=%b -> %s | expired at step %d" ms action (String.concat " " trace) expiredAt).Append('\n') |> ignore
    sb.ToString()
