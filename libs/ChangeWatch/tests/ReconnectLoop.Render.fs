module ReconnectLoopRender
open System.Text
open Wanxiang.Client

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let r = ReconnectLoop(1000, ReconnectLoopSamples.next)
    for act, arg in ReconnectLoopSamples.script do
        let out =
            match act with
            | "connect" -> sprintf "%d" (r.Connect())
            | "sched" -> sprintf "%A" (r.Schedule((arg = 1)))
            | "fire" -> sprintf "%b" (r.Fire arg)
            | "resume" -> sprintf "%b" (r.ResumeNow())
            | "current" -> sprintf "%b" (r.IsCurrent arg)
            | _ -> r.ResetDelay(); "-"
        sb.Append(sprintf "%s %d -> %s | attempt=%d delay=%d pending=%b" act arg out r.Attempt r.DelayMs r.Pending).Append('\n') |> ignore
    sb.ToString()
