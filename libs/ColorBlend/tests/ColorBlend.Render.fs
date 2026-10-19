module ColorBlendRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for b in ColorBlendSamples.bases do
        for o in ColorBlendSamples.overlays do
            sb.Append(sprintf "over %A %A -> %A" b o (ColorBlend.over b o)).Append('\n') |> ignore
    sb.ToString()
