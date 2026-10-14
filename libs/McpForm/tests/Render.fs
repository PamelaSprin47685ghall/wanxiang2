module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for isNew in [ true; false ] do
      for id in Samples.ids do
        for l in Samples.labels do
          for c in Samples.commands do
            for u in Samples.urls do
              for t in Samples.timeouts do
                for a in Samples.args do
                  let v, errs = McpForm.validate isNew Samples.taken id l c a u t
                  let json = (McpForm.payload v true).ToJsonString()
                  sb.Append(sprintf "%b %A %A %A %A %A %A -> %A %s" isNew id l c u t a errs json).Append('\n') |> ignore
    sb.ToString()
