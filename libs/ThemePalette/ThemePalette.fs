namespace Wanxiang2.ThemePalette

open Avalonia.Media

/// 主题模式。跟随系统时由宿主解析成明/暗之一后再传进来。
[<RequireQualifiedAccess>]
type PaletteMode =
    | Light
    | Dark

/// 配色表里的一项。明暗两套结构相同，切换只换值，不换语义（旧 Design/Palette.fs 的字段，外加旧 Tokens 的两支派生画笔）。
[<RequireQualifiedAccess>]
type PaletteKey =
    | Canvas
    | Rail
    | Surface
    | SurfaceRaised
    | SurfaceContainer
    | Border
    | BorderSoft
    | Hairline
    | HairlineStrong
    | Line
    | Text
    | TextMuted
    | TextFaint
    | TextOnAccent
    | Accent
    | AccentHover
    | AccentSoft
    | AccentFaint
    | Selected
    | UserBubble
    | UserBubbleText
    | InlineCode
    | TableStripe
    | TableHeader
    | Success
    | Warning
    | Danger
    | DangerSoft
    | Info
    | InfoSoft
    | CodeBg
    | CodeHeaderBg
    | CodeBorder
    | CodeText
    | CodeMuted
    | CodeKeyword
    | CodeString
    | CodeComment
    | CodeNumber
    | CodeType
    | CodeFunction
    | CodeMeta
    | CodeVariable
    | CodeOperator
    | CodeAddition
    | CodeDeletion
    | Scrim
    | Shadow
    | ShadowStrong
    | Hover
    | Pressed
    | SurfaceSoft
    | UserBubbleSelection
    /// 控件边界描边（输入壳、开关轨道这类「描边就是边界」的控件用）；对四种基础表面 ≥ 3:1（WCAG 1.4.11）。
    | ControlStroke

/// 旧 Design/Palette.fs 的两套配色数据（暖纸浅色、墨夜深色）。只有数据，没有可变状态：
/// 活的画笔归主题管理者。
module ThemePalette =

    let private rgb (v: int) =
        Color.FromRgb(byte (v >>> 16), byte (v >>> 8), byte v)

    let private argb (v: uint32) =
        Color.FromArgb(byte (v >>> 24), byte (v >>> 16), byte (v >>> 8), byte v)

    /// 全部配色项，按旧字段顺序
    let keys: PaletteKey list =
        [
            PaletteKey.Canvas
            PaletteKey.Rail
            PaletteKey.Surface
            PaletteKey.SurfaceRaised
            PaletteKey.SurfaceContainer
            PaletteKey.Border
            PaletteKey.BorderSoft
            PaletteKey.Hairline
            PaletteKey.HairlineStrong
            PaletteKey.Line
            PaletteKey.Text
            PaletteKey.TextMuted
            PaletteKey.TextFaint
            PaletteKey.TextOnAccent
            PaletteKey.Accent
            PaletteKey.AccentHover
            PaletteKey.AccentSoft
            PaletteKey.AccentFaint
            PaletteKey.Selected
            PaletteKey.UserBubble
            PaletteKey.UserBubbleText
            PaletteKey.InlineCode
            PaletteKey.TableStripe
            PaletteKey.TableHeader
            PaletteKey.Success
            PaletteKey.Warning
            PaletteKey.Danger
            PaletteKey.DangerSoft
            PaletteKey.Info
            PaletteKey.InfoSoft
            PaletteKey.CodeBg
            PaletteKey.CodeHeaderBg
            PaletteKey.CodeBorder
            PaletteKey.CodeText
            PaletteKey.CodeMuted
            PaletteKey.CodeKeyword
            PaletteKey.CodeString
            PaletteKey.CodeComment
            PaletteKey.CodeNumber
            PaletteKey.CodeType
            PaletteKey.CodeFunction
            PaletteKey.CodeMeta
            PaletteKey.CodeVariable
            PaletteKey.CodeOperator
            PaletteKey.CodeAddition
            PaletteKey.CodeDeletion
            PaletteKey.Scrim
            PaletteKey.Shadow
            PaletteKey.ShadowStrong
            PaletteKey.Hover
            PaletteKey.Pressed
            PaletteKey.SurfaceSoft
            PaletteKey.UserBubbleSelection
            PaletteKey.ControlStroke
        ]

    let rec private light (key: PaletteKey) : Color =
        match key with
        | PaletteKey.Canvas -> rgb 0xF5F4ED
        | PaletteKey.Rail -> rgb 0xF0EEE5
        | PaletteKey.Surface -> rgb 0xFBFAF7
        | PaletteKey.SurfaceRaised -> rgb 0xFFFFFF
        | PaletteKey.SurfaceContainer -> rgb 0xF5F4EE
        | PaletteKey.Border -> rgb 0xD9D5C7
        | PaletteKey.BorderSoft -> rgb 0xE3E0D3
        | PaletteKey.Hairline -> rgb 0xECE9DE
        | PaletteKey.HairlineStrong -> rgb 0xDDD9CA
        | PaletteKey.Line -> rgb 0xC7C3B3
        | PaletteKey.Text -> rgb 0x141413
        | PaletteKey.TextMuted -> rgb 0x4E4C46
        | PaletteKey.TextFaint -> rgb 0x6E6B62
        | PaletteKey.TextOnAccent -> rgb 0xFBFAF7
        | PaletteKey.Accent -> rgb 0x1B365D
        | PaletteKey.AccentHover -> rgb 0x25476F
        | PaletteKey.AccentSoft -> rgb 0xE2EAF4
        | PaletteKey.AccentFaint -> rgb 0xEFF3F8
        | PaletteKey.Selected -> rgb 0xE1DDCC
        | PaletteKey.UserBubble -> rgb 0x1B365D
        | PaletteKey.UserBubbleText -> rgb 0xFBFAF7
        | PaletteKey.InlineCode -> rgb 0xE2DCC7
        | PaletteKey.TableStripe -> rgb 0xEAE7D8
        | PaletteKey.TableHeader -> rgb 0xE1DCC8
        | PaletteKey.Success -> rgb 0x2F6B4F
        | PaletteKey.Warning -> rgb 0x8A5A18
        | PaletteKey.Danger -> rgb 0x8E3B2F
        | PaletteKey.DangerSoft -> rgb 0xF5E4DF
        | PaletteKey.Info -> rgb 0x2F5578
        | PaletteKey.InfoSoft -> rgb 0xDEE7F1
        | PaletteKey.CodeBg -> rgb 0x17171A
        | PaletteKey.CodeHeaderBg -> rgb 0x202024
        | PaletteKey.CodeBorder -> rgb 0x2E2E33
        | PaletteKey.CodeText -> rgb 0xF2F1EC
        | PaletteKey.CodeMuted -> rgb 0x9B9890
        | PaletteKey.CodeKeyword -> rgb 0x9CBCE4
        | PaletteKey.CodeString -> rgb 0xA9CDA1
        | PaletteKey.CodeComment -> rgb 0x96938B
        | PaletteKey.CodeNumber -> rgb 0xE2B487
        | PaletteKey.CodeType -> rgb 0x8CC9C2
        | PaletteKey.CodeFunction -> rgb 0xD9C384
        | PaletteKey.CodeMeta -> rgb 0xB79BD0
        | PaletteKey.CodeVariable -> rgb 0xE4A9A0
        | PaletteKey.CodeOperator -> rgb 0xC6C3BA
        | PaletteKey.CodeAddition -> rgb 0x8FBF95
        | PaletteKey.CodeDeletion -> rgb 0xD98C80
        | PaletteKey.Scrim -> argb 0x66141413u
        | PaletteKey.Shadow -> argb 0x1A141413u
        | PaletteKey.ShadowStrong -> argb 0x33141413u
        | PaletteKey.Hover -> argb 0x0D141413u
        | PaletteKey.Pressed -> argb 0x1A141413u
        // 柔和表面微底色：surface 压到 0x55 透明度（旧 Tokens.surfaceSoft）
        | PaletteKey.SurfaceSoft -> let s = light PaletteKey.Surface in Color.FromArgb(0x55uy, s.R, s.G, s.B)
        // 用户气泡选中底：半透明白，明暗相同（旧 Tokens.userBubbleSelection）
        | PaletteKey.UserBubbleSelection -> argb 0x59FFFFFFu
        // 控件边界描边：对 Surface 3.31、SurfaceRaised 3.46、SurfaceContainer / Canvas 3.14（本仓库补的令牌，旧配色没有）
        | PaletteKey.ControlStroke -> rgb 0x8D8A7E

    let rec private dark (key: PaletteKey) : Color =
        match key with
        | PaletteKey.Canvas -> rgb 0x15151A
        | PaletteKey.Rail -> rgb 0x1A1A22
        | PaletteKey.Surface -> rgb 0x22222A
        | PaletteKey.SurfaceRaised -> rgb 0x2C2C35
        | PaletteKey.SurfaceContainer -> rgb 0x1E1E26
        | PaletteKey.Border -> rgb 0x45454F
        | PaletteKey.BorderSoft -> rgb 0x3A3A43
        | PaletteKey.Hairline -> rgb 0x2E2E37
        | PaletteKey.HairlineStrong -> rgb 0x40404A
        | PaletteKey.Line -> rgb 0x55555F
        | PaletteKey.Text -> rgb 0xEDEBE4
        | PaletteKey.TextMuted -> rgb 0xB2AFA6
        | PaletteKey.TextFaint -> rgb 0xA19E95
        | PaletteKey.TextOnAccent -> rgb 0x0F1A28
        | PaletteKey.Accent -> rgb 0x8FB4DE
        | PaletteKey.AccentHover -> rgb 0xA6C6E9
        | PaletteKey.AccentSoft -> rgb 0x22313F
        | PaletteKey.AccentFaint -> rgb 0x1C2530
        | PaletteKey.Selected -> rgb 0x34343E
        | PaletteKey.UserBubble -> rgb 0x2F4864
        | PaletteKey.UserBubbleText -> rgb 0xE7EEF7
        | PaletteKey.InlineCode -> rgb 0x2E2E38
        | PaletteKey.TableStripe -> rgb 0x2A2A35
        | PaletteKey.TableHeader -> rgb 0x343442
        | PaletteKey.Success -> rgb 0x7FBF9C
        | PaletteKey.Warning -> rgb 0xD6A75E
        | PaletteKey.Danger -> rgb 0xE09385
        | PaletteKey.DangerSoft -> rgb 0x422421
        | PaletteKey.Info -> rgb 0x95A9C6
        | PaletteKey.InfoSoft -> rgb 0x2A3442
        | PaletteKey.CodeBg -> rgb 0x1E1E28
        | PaletteKey.CodeHeaderBg -> rgb 0x262634
        | PaletteKey.CodeBorder -> rgb 0x363646
        | PaletteKey.CodeText -> rgb 0xEDEBE4
        | PaletteKey.CodeMuted -> rgb 0x9B9890
        | PaletteKey.CodeKeyword -> rgb 0x9CBCE4
        | PaletteKey.CodeString -> rgb 0xA9CDA1
        | PaletteKey.CodeComment -> rgb 0x96938B
        | PaletteKey.CodeNumber -> rgb 0xE2B487
        | PaletteKey.CodeType -> rgb 0x8CC9C2
        | PaletteKey.CodeFunction -> rgb 0xD9C384
        | PaletteKey.CodeMeta -> rgb 0xB79BD0
        | PaletteKey.CodeVariable -> rgb 0xE4A9A0
        | PaletteKey.CodeOperator -> rgb 0xC6C3BA
        | PaletteKey.CodeAddition -> rgb 0x8FBF95
        | PaletteKey.CodeDeletion -> rgb 0xD98C80
        | PaletteKey.Scrim -> argb 0x99000000u
        | PaletteKey.Shadow -> argb 0x59000000u
        | PaletteKey.ShadowStrong -> argb 0x8C000000u
        | PaletteKey.Hover -> argb 0x14FFFFFFu
        | PaletteKey.Pressed -> argb 0x24FFFFFFu
        // 柔和表面微底色：surface 压到 0x55 透明度（旧 Tokens.surfaceSoft）
        | PaletteKey.SurfaceSoft -> let s = dark PaletteKey.Surface in Color.FromArgb(0x55uy, s.R, s.G, s.B)
        // 用户气泡选中底：半透明白，明暗相同（旧 Tokens.userBubbleSelection）
        | PaletteKey.UserBubbleSelection -> argb 0x59FFFFFFu
        // 控件边界描边：对 Surface 3.51、SurfaceRaised 3.07、SurfaceContainer 3.68、Canvas 4.04（本仓库补的令牌，旧配色没有）
        | PaletteKey.ControlStroke -> rgb 0x76767F

    /// 某一模式下某一项的颜色
    let color (mode: PaletteMode) (key: PaletteKey) : Color =
        match mode with
        | PaletteMode.Light -> light key
        | PaletteMode.Dark -> dark key
