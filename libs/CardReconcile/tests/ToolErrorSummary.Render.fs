module ToolErrorSummaryRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let js (s: string) = System.Text.Json.JsonSerializer.Serialize s
    for r in ToolErrorSummarySamples.results do
        let input = match r with Some s -> js s | None -> "None"
        let output = match ToolErrorSummary.tryExtract r with Some s -> js s | None -> "None"
        sb.Append(sprintf "tryExtract %s -> %s" input output).Append('\n') |> ignore
    sb.ToString()
