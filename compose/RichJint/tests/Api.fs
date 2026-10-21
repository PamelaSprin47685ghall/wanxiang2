module Api

open Wanxiang.UI
// 新实现一侧的接线；旧代码一侧的同名文件在生成器里（见 AGENTS.md）。
let backend = Wanxiang.App.JintRichBackend()
let resolve (l: string) = RichAssets.resolveLanguage l
let requires (c: string) = RichAssets.requiresLoad c
let highlight (code: string) (canonical: string) = backend.HighlightHtml(code, canonical)

let tokens (html: string) =
    HljsHtml.parseHtml html
    |> List.map (fun t -> sprintf "%A %s" t.kind (System.Text.Json.JsonSerializer.Serialize t.text))

let math (tex: string) (display: bool) = backend.MathHtml(tex, display)
let katex (html: string) = sprintf "%A" (KatexHtml.parse html)

let visual (html: string) =
    KatexHtml.parse html |> KatexHtml.visualSubtree |> Option.isSome

let display (html: string) =
    KatexHtml.parse html |> KatexHtml.isDisplay
