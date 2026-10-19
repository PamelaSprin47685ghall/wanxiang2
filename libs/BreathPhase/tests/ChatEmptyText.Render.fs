module ChatEmptyTextRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for s in ChatEmptyTextSamples.states do
        let title, hint = ChatEmptyText.text s
        sb.Append(sprintf "text %A -> %s | %s" s title hint).Append('\n') |> ignore
    for label, prefix in ChatStarters.items do
        sb.Append(sprintf "starter %s -> %s" label (System.Text.Json.JsonSerializer.Serialize prefix)).Append('\n') |> ignore
    sb.ToString()
