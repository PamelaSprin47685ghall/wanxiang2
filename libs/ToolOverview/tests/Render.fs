module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for c in Samples.counts do
        sb.Append(sprintf "%d %d %d %d %d -> %d %s %A %A" c.running c.awaiting c.success c.failed c.unknown (ToolOverview.total c) (ToolOverview.text c) (ToolOverview.tone c) (ToolOverview.icon c)).Append('\n') |> ignore
    sb.ToString()
