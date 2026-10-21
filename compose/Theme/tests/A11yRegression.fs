module Wanxiang2.Theme.Tests.A11yRegression

// 无障碍机械回归，迁自旧仓库 tests/Wanxiang.Tests/A11yRegressionTests.fs。
// 全部为非 UI 测试：只读 ThemePalette 数值与本仓库源文件，不碰 Avalonia 平台。
// 放在 compose/Theme/tests（主题管理者一侧）：对比度契约以 ThemePalette 为权威；
// 对 compose/、app/、libs/ 的源文件只做文件系统读取，不构成项目引用。

open System
open System.IO
open System.Text.RegularExpressions
open Avalonia.Media
open Xunit
open Wanxiang2.ThemePalette

let private repoRoot =
    let rec up (dir: DirectoryInfo) =
        if isNull dir then
            failwith "找不到仓库根（wanxiang2.slnx）"
        elif File.Exists(Path.Combine(dir.FullName, "wanxiang2.slnx")) then
            dir.FullName
        else
            up dir.Parent

    up (DirectoryInfo AppContext.BaseDirectory)

let private read (rel: string) =
    File.ReadAllText(Path.Combine(repoRoot, rel))

let private luminance (c: Color) =
    let lin (v: byte) =
        let x = float v / 255.0

        if x <= 0.03928 then
            x / 12.92
        else
            Math.Pow((x + 0.055) / 1.055, 2.4)

    0.2126 * lin c.R + 0.7152 * lin c.G + 0.0722 * lin c.B

let private ratio (a: Color) (b: Color) =
    let la, lb = luminance a, luminance b
    (max la lb + 0.05) / (min la lb + 0.05)

let private palettes: (string * PaletteMode) list =
    [ "light", PaletteMode.Light; "dark", PaletteMode.Dark ]

let private color (mode: PaletteMode) (key: PaletteKey) = ThemePalette.color mode key

let private check (label: string) (minimum: float) (pairs: (string * Color * string * Color) list) =
    let bad =
        [
            for (fgName, fg, bgName, bg) in pairs do
                let r = ratio fg bg

                if r < minimum then
                    sprintf "%s: %s on %s = %.2f < %.1f" label fgName bgName r minimum
        ]

    Assert.True(bad.IsEmpty, String.Join("\n", bad))

// ---------- 对比度 ----------

[<Fact>]
let ``代码块内全部着色与弱化文字在代码面和代码头上过 AA`` () =
    for (label, mode) in palettes do
        let inks =
            [
                "codeText", color mode PaletteKey.CodeText
                "codeMuted", color mode PaletteKey.CodeMuted
                "codeComment", color mode PaletteKey.CodeComment
                "codeKeyword", color mode PaletteKey.CodeKeyword
                "codeString", color mode PaletteKey.CodeString
                "codeNumber", color mode PaletteKey.CodeNumber
                "codeType", color mode PaletteKey.CodeType
                "codeFunction", color mode PaletteKey.CodeFunction
                "codeMeta", color mode PaletteKey.CodeMeta
                "codeVariable", color mode PaletteKey.CodeVariable
                "codeOperator", color mode PaletteKey.CodeOperator
                "codeAddition", color mode PaletteKey.CodeAddition
                "codeDeletion", color mode PaletteKey.CodeDeletion
            ]

        let grounds =
            [
                "codeBg", color mode PaletteKey.CodeBg
                "codeHeaderBg", color mode PaletteKey.CodeHeaderBg
            ]

        check
            label
            4.5
            [
                for (i, f) in inks do
                    for (g, b) in grounds -> (i, f, g, b)
            ]

[<Fact>]
let ``正文与次要文字在选中行行内代码表格底上过 AA`` () =
    for (label, mode) in palettes do
        let grounds =
            [
                "selected", color mode PaletteKey.Selected
                "inlineCode", color mode PaletteKey.InlineCode
                "tableStripe", color mode PaletteKey.TableStripe
                "tableHeader", color mode PaletteKey.TableHeader
                "accentSoft", color mode PaletteKey.AccentSoft
                "accentFaint", color mode PaletteKey.AccentFaint
            ]

        check
            label
            4.5
            [
                for (g, b) in grounds do
                    yield ("text", color mode PaletteKey.Text, g, b)
                    yield ("textMuted", color mode PaletteKey.TextMuted, g, b)
            ]

[<Fact>]
let ``状态色文字在各自的浅底上过 AA`` () =
    for (label, mode) in palettes do
        check
            label
            4.5
            [
                "danger", color mode PaletteKey.Danger, "dangerSoft", color mode PaletteKey.DangerSoft
                "info", color mode PaletteKey.Info, "infoSoft", color mode PaletteKey.InfoSoft
                "accent", color mode PaletteKey.Accent, "accentSoft", color mode PaletteKey.AccentSoft
            ]

[<Fact>]
let ``强调色悬停态与用户气泡上的文字过 AA`` () =
    for (label, mode) in palettes do
        check
            label
            4.5
            [
                "textOnAccent", color mode PaletteKey.TextOnAccent, "accentHover", color mode PaletteKey.AccentHover
                "textOnAccent", color mode PaletteKey.TextOnAccent, "accent", color mode PaletteKey.Accent
                "userBubbleText", color mode PaletteKey.UserBubbleText, "userBubble", color mode PaletteKey.UserBubble
            ]

[<Fact>]
let ``键盘焦点环颜色在所有基础表面上过非文本 3 比 1`` () =
    // WCAG 1.4.11：焦点指示器与相邻颜色至少 3:1。焦点环取 accent（见 Theme 的样式类）。
    for (label, mode) in palettes do
        check
            label
            3.0
            [
                for (g, b) in
                    [
                        "canvas", color mode PaletteKey.Canvas
                        "rail", color mode PaletteKey.Rail
                        "surface", color mode PaletteKey.Surface
                        "surfaceRaised", color mode PaletteKey.SurfaceRaised
                        "surfaceContainer", color mode PaletteKey.SurfaceContainer
                    ] -> ("accent(focus ring)", color mode PaletteKey.Accent, g, b)
            ]

[<Fact>]
let ``控件边界描边在四种底上都过 1.4.11`` () =
    // 输入壳、开关轨道这类「描边就是边界」的控件：填充色与所在表面同值（FieldSurface 取 Surface），
    // 所以边界只能靠这条描边，WCAG 1.4.11 要求它至少 3:1。2026-10-18 引入 ControlStroke 之前，
    // 这些控件用 border（浅 1.33~1.47、深 1.46~1.92），达不到 3:1。
    for (label, mode) in palettes do
        check
            label
            3.0
            [
                for (g, b) in
                    [
                        "canvas", color mode PaletteKey.Canvas
                        "surface", color mode PaletteKey.Surface
                        "surfaceRaised", color mode PaletteKey.SurfaceRaised
                        "surfaceContainer", color mode PaletteKey.SurfaceContainer
                    ] -> ("controlStroke", color mode PaletteKey.ControlStroke, g, b)
            ]

[<Fact>]
let ``遮罩不透明度足以压住背后内容`` () =
    Assert.True((color PaletteMode.Light PaletteKey.Scrim).A >= 0x55uy, "浅色遮罩 alpha 过低")
    Assert.True((color PaletteMode.Dark PaletteKey.Scrim).A >= 0x80uy, "深色遮罩 alpha 过低")

// ---------- 共享原语：图标按钮必须有可访问名称 ----------

// ---------- 文案一致性：术语表 ----------

[<Fact>]
let ``PWA 入口页声明语言标题并允许缩放`` () =
    let html = read "app/Pwa/wwwroot/index.html"
    Assert.Matches("<html[^>]*\\blang=\"zh", html)
    Assert.Matches("<title>[^<]+</title>", html)
    Assert.DoesNotContain("user-scalable=no", html)
    Assert.DoesNotContain("user-scalable=0", html)
    Assert.DoesNotMatch("maximum-scale=1(\\.0)?[,\"\\s]", html)

[<Fact>]
let ``PWA 启动页对辅助技术可读且装饰元素隐藏`` () =
    let html = read "app/Pwa/wwwroot/index.html"
    Assert.Contains("role=\"status\"", html)
    Assert.Contains("aria-live=", html)
    Assert.Contains("aria-hidden=\"true\"", html)
    Assert.Matches("<img[^>]*\\balt=\"[^\"]+\"", html)

[<Fact>]
let ``PWA 样式保留焦点环并尊重减少动态偏好`` () =
    let css = read "app/Pwa/wwwroot/app.css"
    Assert.Contains("prefers-reduced-motion", css)
    Assert.Contains(":focus-visible", css)

[<Fact>]
let ``PWA 清单声明名称与图标`` () =
    let m = read "app/Pwa/wwwroot/manifest.webmanifest"
    Assert.Matches("\"name\"\\s*:\\s*\"[^\"]+\"", m)
    Assert.Matches("\"short_name\"\\s*:\\s*\"[^\"]+\"", m)
    Assert.Contains("\"icons\"", m)

// ── r13 无障碍复审（旧仓库决策 302–306）：r7–r12 新增的芯片 / 提示条 / 倒计时 / 切换器 ──

[<Fact>]
let ``r7-r12 新增控件的文字在浅色与深色主题下都过 AA`` () =
    for (label, mode) in palettes do
        check
            label
            4.5
            [ // 顶栏模型芯片（决策 240）
                "accent", color mode PaletteKey.Accent, "accentFaint", color mode PaletteKey.AccentFaint
                // 限流倒计时（决策 242）：错误卡在 surface / surfaceRaised 上
                "warning", color mode PaletteKey.Warning, "surface", color mode PaletteKey.Surface
                "warning", color mode PaletteKey.Warning, "surfaceRaised", color mode PaletteKey.SurfaceRaised
                "success", color mode PaletteKey.Success, "surface", color mode PaletteKey.Surface
                "success", color mode PaletteKey.Success, "surfaceRaised", color mode PaletteKey.SurfaceRaised
                // 提示条（撤销删除）与模型切换器浮层
                "text", color mode PaletteKey.Text, "surfaceRaised", color mode PaletteKey.SurfaceRaised
                "textMuted", color mode PaletteKey.TextMuted, "surfaceRaised", color mode PaletteKey.SurfaceRaised
                "accent", color mode PaletteKey.Accent, "surfaceRaised", color mode PaletteKey.SurfaceRaised
                // 选中行（切换器当前项、搜索命中）
                "text", color mode PaletteKey.Text, "selected", color mode PaletteKey.Selected
                "textMuted", color mode PaletteKey.TextMuted, "selected", color mode PaletteKey.Selected
            ]
