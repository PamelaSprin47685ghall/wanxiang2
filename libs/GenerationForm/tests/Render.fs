module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let js (s: string) = System.Text.Json.JsonSerializer.Serialize s
    for f in Samples.floats do sb.Append(sprintf "floatText %A -> %s" f (GenerationForm.floatText f)).Append('\n') |> ignore
    for t, p, m, c, r, i, a in Samples.forms do
        let errors, payload = Adapter.run t p m c r i a
        sb.Append(sprintf "form %s %s %s %s %s %s %b -> errors %A payload %s" (js t) (js p) (js m) (js c) (js r) (js i) a errors payload).Append('\n') |> ignore
    sb.ToString()
