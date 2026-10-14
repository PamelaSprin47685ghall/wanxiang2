module Render

open System.Globalization
open System.Text

/// 金样文本：每项度量一行「名字 值」，值用往返格式
let render (items: (string * float) list) =
    let sb = StringBuilder()
    for n, v in items do sb.Append(n).Append(' ').Append(v.ToString("R", CultureInfo.InvariantCulture)).Append('\n') |> ignore
    sb.ToString()
