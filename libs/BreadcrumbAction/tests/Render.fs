module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let b = BreadcrumbAction<int>()
    for c in Samples.crumbs do
        let v = b.Set c
        sb.Append(sprintf "%A -> %A %A %A" c v b.Text (b.Activate())).Append('\n') |> ignore
    sb.ToString()
