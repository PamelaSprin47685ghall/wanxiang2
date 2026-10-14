namespace Wanxiang.Pwa

open System.Collections.Generic
open Wanxiang.Interop
open Wanxiang.UI

/// PWA 的脚本后端：直接用浏览器自带的 JS 引擎跑 highlight.js 与 KaTeX（旧 `Wanxiang.Pwa.BrowserRichBackend`）。
/// 协作：BrowserRich（JSImport 绑定）、RichAssets（脚本正文与按需语言）。脚本正文与桌面同一份，两端着色一致。
/// 不引用界面：旧 UI 的 IRichBackend 由 PWA 入口用对象表达式转接。
type BrowserRichBackend() =

    let mutable hljsLoaded = false
    let mutable katexLoaded = false
    let onDemandLoaded = HashSet<string>()

    let ensureHljs () =
        if not hljsLoaded then
            BrowserRich.Load(RichAssets.hljsCoreScript ())
            hljsLoaded <- true

    let ensureKatex () =
        if not katexLoaded then
            BrowserRich.Load(RichAssets.katexScript ())
            katexLoaded <- true

    let ensureLanguage (canonical: string) =
        if RichAssets.requiresLoad canonical && not (onDemandLoaded.Contains canonical) then
            BrowserRich.Load(RichAssets.hljsLanguageScript canonical)
            onDemandLoaded.Add canonical |> ignore

    /// 预热。浏览器引擎解析这两个库只要几十毫秒，启动时顺手做掉。
    member _.Warmup() =
        try
            ensureHljs ()
            ensureKatex ()
        with _ -> ()

    /// 浏览器侧失败返回空串；这里与旧实现一样把空串当 None。
    member _.HighlightHtml(code: string, canonicalLanguage: string) : string option =
        try
            ensureHljs ()
            ensureLanguage canonicalLanguage
            match BrowserRich.Highlight(code, canonicalLanguage) with
            | "" -> None
            | html -> Some html
        with _ -> None

    member _.MathHtml(tex: string, displayMode: bool) : string option =
        try
            ensureKatex ()
            match BrowserRich.Math(tex, displayMode) with
            | "" -> None
            | html -> Some html
        with _ -> None
