namespace Wanxiang2.ThemeFonts

open Avalonia.Media

/// 内嵌字体（旧 Wanxiang.UI/Assets/Fonts 原样迁入）：正文 Sarasa Gothic SC、等宽 Sarasa Term SC、公式 KaTeX 全套。
/// browser-wasm 拿不到宿主系统字体，桌面与 PWA 必须用同一份内嵌字形；公式字体与正文同一目录，
/// 分成两个目录时浏览器端解析不到，公式里的 − 与 ± 会变成豆腐块（旧 MathLayout 注释）。
module ThemeFonts =

    /// 字体目录的资源地址
    let directory = "avares://ThemeFonts/Assets/Fonts/"

    /// 正文字体（比例宽度；等宽会把中文段落里的拉丁词拉成日志样）
    ///
    /// 桌面优先用设计稿的 Noto 栈；browser-wasm 没有这些系统字体时，复合字族按字符回落到内嵌 Sarasa。
    /// 尾部的「符号字族」补 Sarasa 不含的 ⌘ ⚙ ✎ ✓ ✕ 📦 🔍 🗑。Avalonia 按字符回落
    /// （FontManager.TryMatchCharacter 逐段尝试），所以符号字族列在最后。
    let sans =
        "Noto Sans SC, PingFang SC, Hiragino Sans GB, Microsoft YaHei, Noto Sans CJK SC"

    let serif = "Noto Serif SC, Songti SC, STSong, Noto Serif CJK SC"

    let body =
        sans
        + ", "
        + directory
        + "#Sarasa Gothic SC, "
        + directory
        + "#Wanxiang Symbols"

    /// 标题字族：衬线栈在前，内嵌 Sarasa 与符号字族垫后，wasm 上标题仍有汉字。
    let display =
        serif
        + ", "
        + directory
        + "#Sarasa Gothic SC, "
        + directory
        + "#Wanxiang Symbols"

    /// 代码与等宽：等宽子集只含拉丁与符号，正文字体列为回落，否则中文变豆腐块
    let mono =
        directory
        + "#Sarasa Term SC, "
        + directory
        + "#Sarasa Gothic SC, "
        + directory
        + "#Wanxiang Symbols"

    /// 符号字族（Noto Sans Symbols 子集，OFL 1.1）：正文与等宽的逐字回落来源
    let symbols = directory + "#Wanxiang Symbols"

    /// 同一目录下按字族名取字体（公式的 KaTeX_* 字族）
    let family (name: string) = FontFamily(directory + "#" + name)
