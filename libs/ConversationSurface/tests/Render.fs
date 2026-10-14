module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for e, s, r, p in Samples.flags do
        for g, u, a, i in Samples.flags do
            sb.Append(sprintf "%b %b %b %b %b %b %b %b -> %A" e s r p g u a i (ConversationSurface.decide e s r p g u a i)).Append('\n') |> ignore
    for c in [ false; true ] do sb.Append(ConversationSurface.hostStatusLine c).Append('\n') |> ignore
    sb.ToString()
