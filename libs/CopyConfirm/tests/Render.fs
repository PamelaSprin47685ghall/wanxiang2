module Render

open System
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let c = CopyConfirm()
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let at (ms: float) = t0.AddMilliseconds ms
    for act, ms in Samples.script do
        let r =
            match act with
            | "copy" -> c.Copied(at ms, false); "-"
            | "copyReduced" -> c.Copied(at ms, true); "-"
            | "reset" -> c.Reset(); "-"
            | "detach" -> c.Detached(); "-"
            | _ -> sprintf "%A" (c.Tick(at ms))
        let due = c.NextDue |> Option.map (fun d -> (d - t0).TotalMilliseconds |> string) |> Option.defaultValue "none"
        sb.Append(sprintf "%s %g -> %s due=%s" act ms r due).Append('\n') |> ignore
    sb.Append(sprintf "%g %g" CopyConfirm.HoldMs CopyConfirm.FadeMs).Append('\n') |> ignore
    sb.ToString()
