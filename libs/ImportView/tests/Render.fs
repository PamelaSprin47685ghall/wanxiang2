module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for p in [ false; true ] do
        for b in [ false; true ] do sb.Append(sprintf "%b %b -> %A" p b (ImportView.chrome p b)).Append('\n') |> ignore
    let a = ImportAttempt()
    for s in Samples.steps do
        let r =
            match s.[0] with
            | 'b' -> sprintf "%A" (a.Begin())
            | 'f' -> sprintf "%b" (a.Finish(int s.[1..]))
            | _ -> a.Reset(); "-"
        sb.Append(sprintf "%s -> %s busy=%b" s r a.Busy).Append('\n') |> ignore
    for line in [ ImportView.noFile; ImportView.importing; ImportView.timedOut; ImportView.rejected; ImportView.readFailed "x"; ImportView.pickerFailed "y"; ImportView.previewStatus "标题" 3 ] do
        sb.Append(line).Append('\n') |> ignore
    sb.Append(sprintf "%A %A" (ImportView.skippedNote 0) (ImportView.skippedNote 2)).Append('\n') |> ignore
    sb.ToString()
