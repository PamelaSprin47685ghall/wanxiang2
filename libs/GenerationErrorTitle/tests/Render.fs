module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let js (s: string) = System.Text.Json.JsonSerializer.Serialize s
    for m in Samples.messages do sb.Append(sprintf "ofMessage %s -> %s" (js m) (GenerationErrorTitle.ofMessage m)).Append('\n') |> ignore
    sb.ToString()
