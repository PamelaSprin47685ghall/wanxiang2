module ProviderProbeTextRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for v in ProviderProbeTextSamples.verdicts do
        for w in ProviderProbeTextSamples.whys do sb.Append(ProviderProbeText.visionLine "gpt-4o" v w).Append('\n') |> ignore
    sb.Append(ProviderProbeText.visionSummary ProviderProbeTextSamples.models ProviderProbeTextSamples.judge).Append('\n') |> ignore
    sb.Append(ProviderProbeText.visionSummary [] ProviderProbeTextSamples.judge).Append("|\n") |> ignore
    for e in [ false; true ] do
        for ok in [ false; true ] do
            for n in [ 0; 1; 7 ] do
                sb.Append(sprintf "%b %b %d -> %A" e ok n (ProviderProbeText.feedback e ok n "密钥无效")).Append('\n') |> ignore
    sb.ToString()
