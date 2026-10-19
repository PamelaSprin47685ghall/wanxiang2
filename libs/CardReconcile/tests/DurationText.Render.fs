module DurationTextRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for v in DurationTextSamples.values do sb.Append(sprintf "format %d -> %s" v (DurationText.format v)).Append('\n') |> ignore
    sb.ToString()
