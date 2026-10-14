module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for s in [ false; true ] do
     for u in [ false; true ] do
      for l in [ false; true ] do
       for t in Samples.texts do
        for c in Samples.commits do
         for h in [ false; true ] do
            let items = MessageMenu.entries s u l t c h
            let shown = items |> List.map (fun i -> sprintf "%s[%s]%s" (MessageMenu.label i) (MessageMenu.hint u l i) (if MessageMenu.isDanger i then "!" else ""))
            sb.Append(sprintf "%b %b %b %A %A %b -> %s" s u l t c h (String.concat ", " shown)).Append('\n') |> ignore
    sb.ToString()
