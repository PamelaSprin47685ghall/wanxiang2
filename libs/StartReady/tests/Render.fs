module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for a, c, g, e in Samples.flags do
        for id, view in Samples.actives do
            sb.Append(sprintf "%b %b %b %b %A %A -> %b" a c g e id view (Adapter.isReady a c g e id view)).Append('\n') |> ignore
    sb.ToString()
