module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let f = ScrollFollow()
    for act, a, b, c in Samples.script do
        let r =
            match act with
            | "render" -> sprintf "%A" (f.Rendered((a = 1), b, (c = 1)))
            | "scroll" -> sprintf "%b" (f.Scrolled((a = 1), (b = 1)))
            | "button" -> sprintf "%A" (f.Button((a = 1), (b = 1)))
            | "return" -> f.ReturnToLatest(); "-"
            | "wheel" -> f.Wheel(); "-"
            | "pin" -> f.Pin(); "-"
            | "jump" -> f.JumpedTo((a = 1)); "-"
            | "begin" -> f.LeaveForBeginning(); "-"
            | "arrive" -> f.Arrived(); "-"
            | _ -> f.Reset(); "-"
        sb.Append(sprintf "%s %d %d %d -> %s | bottom=%b returning=%b unread=%d pinned=%b" act a b c r f.AtBottom f.Returning f.Unread f.RevealPinned).Append('\n') |> ignore
    sb.ToString()
