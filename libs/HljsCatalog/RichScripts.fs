namespace Wanxiang.UI

open System.IO

type private RichScriptsMarker = class end

/// 内嵌的 highlight.js（BSD-3-Clause）与 KaTeX（MIT）脚本（从旧 RichAssets 拆出）。
/// 资源由旧仓库 `tools/build_webassets.js` 装配：hljs 拆成「基座 + 按需语言」，因为全语言打包体约 1MB，在 WASM 里一次性解析太贵。
/// 走清单资源而不是 avares：预热线程与单元测试都在 Avalonia 起来之前读它。
module RichScripts =

    /// 读一份内嵌脚本；不存在时抛（这是打包错误，不是运行时状况）。
    let read (name: string) : string =
        let assembly = typeof<RichScriptsMarker>.Assembly
        use stream = assembly.GetManifestResourceStream $"js/{name}"
        if isNull stream then failwith $"missing embedded script: {name}"
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let katex () = read "katex.min.js"
    let hljsCore () = read "hljs.core.js"
    let hljsLanguage (canonical: string) = read $"hljs.lang/{canonical}.js"
    /// 语言目录 JSON（builtin / onDemand / aliases）。
    let hljsAliases () = read "hljs.aliases.json"
