module MathHtml

open System.IO
open System.Text.Json

/// 公式 HTML 取自 MathFigure 的金样（旧生成器经 RichJint 现场产出），两边共用同一份。
let table (path: string) : Map<string * bool, string> =
    let lines = File.ReadAllLines path

    [
        for i in 0 .. lines.Length - 2 do
            if lines.[i].StartsWith "== " then
                let head = lines.[i].Substring 3
                let display = head.EndsWith " display"

                let tex =
                    JsonSerializer.Deserialize<string>(if display then head.Substring(0, head.Length - 8) else head)

                match JsonSerializer.Deserialize<string>(lines.[i + 1].Substring 6) with
                | null -> ()
                | html -> yield (tex, display), html
    ]
    |> Map.ofList
