module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let js (s: string) = System.Text.Json.JsonSerializer.Serialize s
    sb.Append(sprintf "FirstPrompt %s" OnboardingRules.FirstPrompt).Append('\n') |> ignore
    for b in Samples.bodies do
        sb.Append(sprintf "parseModelIds %s -> %s" (js b) (OnboardingRules.parseModelIds b |> List.map js |> String.concat ",")).Append('\n') |> ignore
    for u in Samples.urls do
        sb.Append(sprintf "canDiscoverLocal %s -> %b" (js u) (OnboardingRules.canDiscoverLocal u)).Append('\n') |> ignore
    for m, p in Samples.picks do
        sb.Append(sprintf "pickDefaultModel %A %A -> %s" m p (js (OnboardingRules.pickDefaultModel m p))).Append('\n') |> ignore
    sb.ToString()
