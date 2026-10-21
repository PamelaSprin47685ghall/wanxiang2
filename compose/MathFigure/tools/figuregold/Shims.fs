namespace Wanxiang.UI

open Avalonia.Media
// 旧代码引用的设计令牌：只给公式用到的三项，值与旧 Tokens/Spacing 相同（画笔取探测共用色）
module Tokens =
    let fontFamily = FontFamily("avares://Wanxiang.UI/Assets/Fonts/#Sarasa Gothic SC")
    let text: IBrush = Probe.ink

module Spacing =
    let spaceMd = 8.0
