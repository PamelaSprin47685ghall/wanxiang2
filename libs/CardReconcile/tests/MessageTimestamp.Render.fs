module MessageTimestampRender
open System.Globalization
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染；固定不变文化，时间分隔符才稳定
let render () =
    let saved = CultureInfo.CurrentCulture
    CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture
    try
        let sb = StringBuilder()
        for at in MessageTimestampSamples.ats do
            sb.Append(sprintf "format %s -> %s" (at.ToString("o", CultureInfo.InvariantCulture)) (MessageTimestamp.format MessageTimestampSamples.now at)).Append('\n') |> ignore
        sb.ToString()
    finally
        CultureInfo.CurrentCulture <- saved
