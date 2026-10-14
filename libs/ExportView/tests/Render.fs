module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for p in Samples.phases do
     for s in Samples.saves do
      for o in [ false; true ] do
       for j in [ false; true ] do
        for r in [ false; true ] do
          sb.Append(sprintf "%A %A %b %b %b -> %A" p s o j r (ExportView.decide p s o j r)).Append('\n') |> ignore
    for f in [ false; true ] do
     for p in Samples.phases do
      for e in Samples.elapsed do
       for pend in [ false; true ] do
          sb.Append(sprintf "refresh %b %A %g %b -> %A" f p e pend (ExportView.refresh f p e 120.0 pend)).Append('\n') |> ignore
    sb.ToString()
