module Render

open System.Text
open Avalonia.Media

/// 金样文本：两套配色逐项写成 #AARRGGBB。color 由调用方给：测试用新库，生成器用旧 Palette。
let render (keys: string list) (color: string -> string -> Color) =
    let sb = StringBuilder()
    for mode in [ "Light"; "Dark" ] do
        for k in keys do
            let c = color mode k
            sb.Append(sprintf "%s %s #%02X%02X%02X%02X" mode k c.A c.R c.G c.B).Append('\n') |> ignore
    sb.ToString()
