module ToastQueueRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let q = ToastQueue()
    for step in ToastQueueSamples.script do
        let text = step.Substring 1
        let dismissed = if step.StartsWith "+" then q.Admit text else (q.Remove text; [])
        sb.Append(sprintf "%s -> dismissed %A showing %A" step dismissed q.Showing).Append('\n') |> ignore
    sb.ToString()
