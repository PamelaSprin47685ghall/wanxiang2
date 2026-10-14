module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for e, s, c in Samples.combos do
        sb.Append(sprintf "sends enterSends=%b shift=%b ctrl=%b -> %b" e s c (SendKey.sends e s c)).Append('\n') |> ignore
    sb.ToString()
