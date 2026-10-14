module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for a in Samples.bools do
        for b in Samples.bools do
            let h = SidebarRowState.highlighted a b
            sb.Append(sprintf "%b %b -> %b %A | %A" a b h (SidebarRowState.itemStatus h) (SidebarRowState.moreButton a b)).Append('\n') |> ignore
    sb.ToString()
