module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染（inset 16、gap 8，旧 Spacing.space2xl 与 spaceMd）
let render () =
    let sb = StringBuilder()
    let f (v: float) = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
    for ax, ay, aw, ah, w, h, vw, vh, right in Samples.cases do
        let x, y = PopupPlacement.place ax ay aw ah w h vw vh 16.0 8.0 right
        sb.Append(sprintf "place %s %s %s %s %s %s %s %s right=%b -> %s %s" (f ax) (f ay) (f aw) (f ah) (f w) (f h) (f vw) (f vh) right (f x) (f y)).Append('\n') |> ignore
    sb.ToString()
