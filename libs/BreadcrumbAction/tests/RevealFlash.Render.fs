module RevealFlashRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let f = RevealFlash()
    for act, c in RevealFlashSamples.script do
        let r =
            match act with
            | "reveal" -> f.Revealed c; "-"
            | "begin" -> sprintf "%A" (f.Begin c)
            | _ -> sprintf "%A" (f.End())
        sb.Append(sprintf "%s %d -> %s | %A %A" act c r f.LastFlashed f.Highlighted).Append('\n') |> ignore
    sb.Append(RevealFlash.DwellMs).Append('\n') |> ignore
    sb.ToString()
