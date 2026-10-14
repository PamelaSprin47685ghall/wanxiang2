module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for a, e, r, h, c, o in Samples.cases do
        sb.Append(sprintf "auth=%b err=%A rePair=%b saved=%b ready=%b busy=%b -> %s" a e r h c o (Adapter.run a e r h c o)).Append('\n') |> ignore
    for a in [ true; false ] do sb.Append(sprintf "openComposer %b -> %A" a (Adapter.openComposer a)).Append('\n') |> ignore
    sb.ToString()
