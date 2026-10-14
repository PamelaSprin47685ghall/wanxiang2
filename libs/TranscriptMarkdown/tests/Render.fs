module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for title, messages in Samples.transcripts do
        sb.Append("==== ").Append(title).Append('\n').Append(TranscriptMarkdown.render title messages).Append('\n') |> ignore
    sb.ToString()
