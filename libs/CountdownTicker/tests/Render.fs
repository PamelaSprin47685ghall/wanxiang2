module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for run in Samples.runs do
        let t = CountdownTicker()
        sb.Append(sprintf "run %A" t.Left).Append('\n') |> ignore
        for left in run do
            let f = t.Refresh left
            sb.Append(sprintf "%d -> ready=%b announce=%b keep=%b left=%A" left f.Ready f.Announce f.KeepTicking t.Left).Append('\n') |> ignore
    sb.Append(CountdownTicker.IntervalMs).Append('\n') |> ignore
    sb.ToString()
