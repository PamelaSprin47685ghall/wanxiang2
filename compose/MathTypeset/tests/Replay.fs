module Replay

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open Wanxiang.UI

// 金样由旧 MathLayout（Avalonia headless + Skia + 内嵌 KaTeX 字体）生成，
// 每个字形都记下了当时的字号与度量；这里回放那张度量表，排版逻辑与旧实现逐行比对。

let bodyFamily = "Sarasa Gothic SC"

let f (v: float) = v.ToString("R", CultureInfo.InvariantCulture)
let num (s: string) = Double.Parse(s, CultureInfo.InvariantCulture)

type Case = { name: string; html: string; expected: string }

let cases =
    let path = Path.Combine(AppContext.BaseDirectory, "golden", "math.txt")
    let text = File.ReadAllText path
    text.Split("\n== ", StringSplitOptions.None)
    |> Array.map (fun chunk ->
        let chunk = if chunk.StartsWith "== " then chunk.Substring 3 else chunk
        let lines = chunk.TrimEnd('\n').Split '\n'
        { name = lines[0]
          html = JsonSerializer.Deserialize<string>(lines[1].Substring 6)
          expected = String.Join("\n", lines[2..]) })
    |> List.ofArray

let private field (line: string) (key: string) =
    let i = line.IndexOf(" " + key + "=")
    let rest = line.Substring(i + key.Length + 2)
    let j = rest.IndexOf ' '
    if j < 0 then rest else rest.Substring(0, j)

/// 金样里出现过的全部 (字形, 字族, 字号) → 度量
let table =
    [ for c in cases do
        for line in c.expected.Split '\n' do
            if line.StartsWith "glyph: " then
                let reader = Utf8JsonReader(Encoding.UTF8.GetBytes(line.Substring 7))
                reader.Read() |> ignore
                let glyph = reader.GetString()
                let m = line.Substring(line.IndexOf " | measure")
                let fam = let a = line.IndexOf " family=" + 8 in line.Substring(a, line.IndexOf(" size=") - a)
                yield (glyph, fam, num (field line "size")),
                      { width = num (field m "w"); baseline = num (field m "base"); height = num (field m "h") } ]

/// 回放量尺：查不到就抛错（排版器会把它变成 Error，金样比对随之失败）
type ReplayMeter() =
    interface IGlyphMeter with
        member _.Measure(text, family, size) =
            let family = if family = "" then bodyFamily else family
            match table |> List.tryFind (fun ((t, fam, s), _) -> t = text && fam = family && abs (s - size) <= 1e-9 * size) with
            | Some(_, m) -> m
            | None -> failwith $"度量表里没有 {text}/{family}/{size}"

let typesetter = MathTypesetter(ReplayMeter())

/// 与旧生成器相同的文本格式
let render (html: string) =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let meter = ReplayMeter() :> IGlyphMeter
    match typesetter.TryRender(14.5, html) with
    | Error e -> line ("error: " + e)
    | Ok box ->
        line (sprintf "box: w=%s above=%s below=%s" (f box.width) (f box.above) (f box.below))
        for it in box.items do
            match it with
            | MathItem.Glyph(text, family, size, x, y) ->
                let m = meter.Measure(text, family, size)
                let fam = if family = "" then bodyFamily else family
                line (sprintf "glyph: %s family=%s size=%s x=%s y=%s | measure w=%s base=%s h=%s"
                        (JsonSerializer.Serialize text) fam (f size) (f x) (f y) (f m.width) (f m.baseline) (f m.height))
            | MathItem.Rule(x, y, w, h) -> line (sprintf "rule: x=%s y=%s w=%s h=%s" (f x) (f y) (f w) (f h))
            | MathItem.Path(_, s, dx, dy, cx, cy, cw, ch) ->
                line (sprintf "path: m=%s,%s,%s,%s,%s,%s clip=%s,%s,%s,%s" (f s) (f 0.0) (f 0.0) (f s) (f dx) (f dy) (f cx) (f cy) (f cw) (f ch))
    sb.ToString().TrimEnd('\n')
