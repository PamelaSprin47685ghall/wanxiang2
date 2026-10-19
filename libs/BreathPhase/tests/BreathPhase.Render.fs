module BreathPhaseRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let b = BreathPhase()
    for act, reduced, attached in BreathPhaseSamples.script do
        let r =
            match act with
            | "start" -> sprintf "%b" (b.Start(reduced, attached))
            | _ -> sprintf "%.9f" (b.Step())
        sb.Append(sprintf "%s %b %b -> %s phase=%.6f" act reduced attached r b.Phase).Append('\n') |> ignore
    sb.Append(sprintf "%g %g" BreathPhase.FrameMs BreathPhase.StepRadians).Append('\n') |> ignore
    sb.ToString()
