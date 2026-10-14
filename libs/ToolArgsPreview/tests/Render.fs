module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let js (s: string) = System.Text.Json.JsonSerializer.Serialize s
    for a in Samples.args do
        let n, p = ToolArgsPreview.preview a
        sb.Append(sprintf "preview %s -> %d %s" (js a) n (js p)).Append('\n') |> ignore
        sb.Append(sprintf "prettyJson %s -> %s" (js a) (js (ToolArgsPreview.prettyJson a))).Append('\n') |> ignore
    sb.ToString()
