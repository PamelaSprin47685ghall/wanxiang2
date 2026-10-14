module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let f (v: float) = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
    for b in Samples.boxes do
        let w, h, base' = MathBaseline.metrics b
        sb.Append(sprintf "metrics %A -> %s %s %s" b (f w) (f h) (f base')).Append('\n') |> ignore
    for d, own, host, y in Samples.draws do
        sb.Append(sprintf "drawBaseline %b %s %A %s -> %s" d (f own) host (f y) (f (MathBaseline.drawBaseline d own host y))).Append('\n') |> ignore
    sb.ToString()
