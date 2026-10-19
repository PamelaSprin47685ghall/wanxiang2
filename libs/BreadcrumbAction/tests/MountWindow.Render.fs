module MountWindowRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let w = MountWindow(MountWindow.Initial, MountWindow.Step)
    for act, a, b, s in MountWindowSamples.script do
        let r =
            match act with
            | "plan" -> sprintf "%d" (w.Plan(MountWindowSamples.msgs a b s, false))
            | "planp" -> sprintf "%d" (w.Plan(MountWindowSamples.msgs a b s, true))
            | "expand" -> sprintf "%b" (w.Expand())
            | "ensure" -> sprintf "%b" (w.EnsureMounted(uint64 a))
            | _ -> w.Reset(); "-"
        let h = w.Hidden
        sb.Append(sprintf "%s %d %d %b -> %s | window=%d hidden=%d [%A..%A] first=%A" act a b s r w.Window w.HiddenCount (Array.tryHead h) (Array.tryLast h) w.LastFirstCommit).Append('\n') |> ignore
    sb.ToString()
