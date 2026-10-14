module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for r in Samples.raws do
        let d = PairingCode.normalize r
        sb.Append(sprintf "%A -> %s %g" r d (PairingCode.letterSpacing d)).Append('\n') |> ignore
    let a = PairingAutoSubmit()
    for t in Samples.typing do sb.Append(sprintf "type %s -> %b" t (a.Changed t)).Append('\n') |> ignore
    sb.Append(PairingCode.incomplete).Append('\n') |> ignore
    sb.ToString()
