module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for p, d, u in Samples.cases do sb.Append(sprintf "text %b %d %d -> %s" p d u (DraftSummaryText.text p d u)).Append('\n') |> ignore
    sb.ToString()
