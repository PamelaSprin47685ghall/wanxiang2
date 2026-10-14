module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for k in Samples.keys do
        for n in Samples.counts do
            for c in -1 .. n do
                sb.Append(sprintf "%A %d %d -> %A" k c n (RovingIndex.menu k c n)).Append('\n') |> ignore
    for n in [ 1; 4 ] do
        for i in 0 .. n - 1 do
            for d in [ -1; 1 ] do sb.Append(sprintf "wrap %d %d %d -> %d" i d n (RovingIndex.wrap i d n)).Append('\n') |> ignore
    sb.ToString()
