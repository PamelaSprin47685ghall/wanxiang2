namespace Wanxiang.UI

/// 旧 `Wanxiang.UI.RichAssets`：内嵌脚本与 hljs 语言目录，公开面不变。
/// 协作：RichScripts（脚本正文与目录 JSON）、HljsCatalog（目录解析与语言解析）。
module RichAssets =

    let katexScript () = RichScripts.katex ()
    let hljsCoreScript () = RichScripts.hljsCore ()
    let hljsLanguageScript (name: string) = RichScripts.hljsLanguage name

    /// 目录只解析一次；读不到或格式不对时为空目录（全部不着色）。
    let private catalog = lazy (try HljsCatalog.parse (RichScripts.hljsAliases ()) with _ -> HljsCatalog.empty)

    /// 语言标注 → hljs 规范名。认不出的返回 None，调用方退化为不着色。
    let resolveLanguage (language: string) : string option = HljsCatalog.resolve catalog.Value language

    let requiresLoad (canonical: string) = HljsCatalog.requiresLoad catalog.Value canonical
