module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for t in Samples.texts do
        for l in Samples.lasts do
            sb.Append(sprintf "%A %A -> %A %b" t l (SearchRequery.query t) (SearchRequery.shouldQuery t l)).Append('\n') |> ignore
    sb.ToString()
