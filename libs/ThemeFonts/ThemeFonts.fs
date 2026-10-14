namespace Wanxiang2.ThemeFonts

open Avalonia.Media

/// 内嵌字体（旧 Wanxiang.UI/Assets/Fonts 原样迁入）：正文 Sarasa Gothic SC、等宽 Sarasa Term SC、公式 KaTeX 全套。
/// browser-wasm 拿不到宿主系统字体，桌面与 PWA 必须用同一份内嵌字形；公式字体与正文同一目录，
/// 分成两个目录时浏览器端解析不到，公式里的 − 与 ± 会变成豆腐块（旧 MathLayout 注释）。
module ThemeFonts =

    /// 字体目录的资源地址
    let directory = "avares://ThemeFonts/Assets/Fonts/"

    /// 正文字体（比例宽度；等宽会把中文段落里的拉丁词拉成日志样）
    let body = directory + "#Sarasa Gothic SC"

    /// 代码与等宽：等宽子集只含拉丁与符号，正文字体列为回落，否则中文变豆腐块
    let mono = directory + "#Sarasa Term SC, Sarasa Gothic SC"

    /// 同一目录下按字族名取字体（公式的 KaTeX_* 字族）
    let family (name: string) = FontFamily(directory + "#" + name)
