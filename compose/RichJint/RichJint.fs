namespace Wanxiang.App

/// 桌面端的脚本后端：在进程内跑 highlight.js 与 KaTeX（旧 `Wanxiang.App.JintRichBackend`）。
/// 协作：JintScriptHost（引擎、只加载一次、串行）、RichAssets（脚本正文与按需语言）。
/// 它不引用界面：旧 UI 的 IRichBackend 由入口用一个对象表达式转接（两行），布局与绘制仍在 Avalonia 侧。
/// 刻意留在桌面而不进 UI：PWA 引用 UI，放进 UI 就等于把 1MB 的 Jint 塞进 WASM 包。
type JintRichBackend() =

    let host = JintScriptHost()

    let ensureHljs () = host.LoadOnce("hljs", Wanxiang.UI.RichAssets.hljsCoreScript)
    let ensureKatex () = host.LoadOnce("katex", Wanxiang.UI.RichAssets.katexScript)

    let ensureLanguage (canonical: string) =
        if Wanxiang.UI.RichAssets.requiresLoad canonical then
            host.LoadOnce("hljs.lang/" + canonical, fun () -> Wanxiang.UI.RichAssets.hljsLanguageScript canonical)

    /// 预热。基座脚本解析要几百毫秒，放到后台线程先付掉，免得用户第一次收到代码块时卡在 UI 线程上。
    member _.Warmup() =
        try
            ensureHljs ()
            ensureKatex ()
            // 空跑一次：解释器首次执行语法定义比稳态慢一个量级
            host.EvaluateString("hljs.highlight(__warm,{language:'javascript',ignoreIllegals:true}).value", [ "__warm", box "let x = 1" ]) |> ignore
            host.EvaluateString("katex.renderToString('x',{throwOnError:false})", []) |> ignore
        with _ -> ()

    /// hljs 的 `<span class="hljs-…">` HTML。任何失败（语言脚本缺失、脚本抛错）都是 None。
    member _.HighlightHtml(code: string, canonicalLanguage: string) : string option =
        try
            ensureHljs ()
            ensureLanguage canonicalLanguage
            Some(
                host.EvaluateString(
                    "hljs.highlight(__code,{language:__lang,ignoreIllegals:true}).value",
                    [ "__code", box code; "__lang", box canonicalLanguage ]))
        with _ -> None

    /// KaTeX 的 HTML（throwOnError:false：语法错误也给出带 katex-error 的 HTML）。
    member _.MathHtml(tex: string, displayMode: bool) : string option =
        try
            ensureKatex ()
            Some(
                host.EvaluateString(
                    "katex.renderToString(__tex,{displayMode:__display,throwOnError:false,strict:false})",
                    [ "__tex", box tex; "__display", box displayMode ]))
        with _ -> None
