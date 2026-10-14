module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for name, f in Samples.edits do
        let r = ProviderForm.validate (f Samples.baseInput)
        sb.Append(sprintf "%s -> %s|%s|%s|%A|%s|%d|%d|%A|%A|%A|%A" name r.id r.label r.url r.models r.defaultModel r.timeout r.retries r.visionYes r.visionNo r.autoPicked r.errors).Append('\n') |> ignore
    for u in Samples.urls do sb.Append(sprintf "loopback %A -> %b" u (ProviderForm.isLoopbackUrl u)).Append('\n') |> ignore
    sb.ToString()
