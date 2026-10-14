module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for s in [ false; true ] do
        for h in Samples.heights do sb.Append(sprintf "%b %g -> %b" s h (StartLayout.centered s h 560.0)).Append('\n') |> ignore
    for w in Samples.widths do sb.Append(sprintf "w %g -> %g" w (StartLayout.sidebarWidth w 232.0 420.0)).Append('\n') |> ignore
    sb.ToString()
