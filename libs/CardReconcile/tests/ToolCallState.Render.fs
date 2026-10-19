module ToolCallStateRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for a in ToolCallStateSamples.awaitings do
        for s in [ false; true ] do
            for id in ToolCallStateSamples.ids do
                for o in ToolCallStateSamples.outcomes do
                    sb.Append(sprintf "%A %b %s %A -> %A" (Set.toList a) s id o (ToolCallState.resolve a s id o)).Append('\n') |> ignore
    sb.ToString()
