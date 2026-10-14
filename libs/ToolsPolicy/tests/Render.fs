module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for p in Samples.policies do sb.Append(sprintf "%A -> %s" p (ToolsPolicy.label p)).Append('\n') |> ignore
    for r in Samples.rootsList do
      for d in [ "auto"; "first" ] do
        for m in Samples.maps do
          for e in [ false; true ] do
            sb.Append((ToolsPolicy.payload r d m e).ToJsonString()).Append('\n') |> ignore
    sb.ToString()
