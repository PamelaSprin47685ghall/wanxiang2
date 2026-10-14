module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for t in Samples.elapsed do
        sb.Append(sprintf "%g -> %.12f %.12f %.12f" t (CaretBreath.phase 900.0 t) (CaretBreath.wave 900.0 t) (CaretBreath.opacity 0.55 1.0 900.0 t)).Append('\n') |> ignore
    sb.ToString()
