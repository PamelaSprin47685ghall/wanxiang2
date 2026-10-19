module McpFormRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for isNew in [ true; false ] do
      for id in McpFormSamples.ids do
        for l in McpFormSamples.labels do
          for c in McpFormSamples.commands do
            for u in McpFormSamples.urls do
              for t in McpFormSamples.timeouts do
                for a in McpFormSamples.args do
                  let v, errs = McpForm.validate isNew McpFormSamples.taken id l c a u t
                  let json = (McpForm.payload v true).ToJsonString()
                  sb.Append(sprintf "%b %A %A %A %A %A %A -> %A %s" isNew id l c u t a errs json).Append('\n') |> ignore
    sb.ToString()
