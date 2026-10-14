module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for a in Samples.awaitings do
        for s in [ false; true ] do
            for id in Samples.ids do
                for o in Samples.outcomes do
                    sb.Append(sprintf "%A %b %s %A -> %A" (Set.toList a) s id o (ToolCallState.resolve a s id o)).Append('\n') |> ignore
    sb.ToString()
