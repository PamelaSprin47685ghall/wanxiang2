module Render

open System
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染。宿主替身：Now 或尾巴补渲染时调 Rendered。
let render () =
    let sb = StringBuilder()
    let p = ProgressThrottle()
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    for act, ms in Samples.script do
        let now = t0.AddMilliseconds ms
        let r =
            match act with
            | "tail" ->
                let fire = p.TailFired()
                if fire then p.Rendered now
                sprintf "tail render=%b" fire
            | _ ->
                let x = p.Request(now, (act = "force"), (act = "read"))
                if x = ProgressRefresh.Now then p.Rendered now
                sprintf "%A" x
        sb.Append(sprintf "%s %g -> %s pending=%b" act ms r p.Pending).Append('\n') |> ignore
    sb.Append(ProgressThrottle.WindowMs).Append('\n') |> ignore
    sb.ToString()
