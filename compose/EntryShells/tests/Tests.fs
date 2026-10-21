namespace Wanxiang.UI.Tests

open System
open Xunit
open Wanxiang.UI

module EffectSinkTests =

    [<Fact>]
    let ``ScrollAnchorMath correctly computes restored prepend offset`` () =
        // 初始情况：extent=1000, offset=200；头插前置历史后 extent 增加到 1500（增加 500）
        // 恢复时视口应下移 500，使原先内容相对视口不动，即 offset 变成 700
        let restored = ScrollAnchorMath.computeRestoredOffset 1000.0 200.0 1500.0
        Assert.Equal(700.0, restored)

        // 边界：新 extent 没有增加（delta <= 0），offset 保持原样
        let same = ScrollAnchorMath.computeRestoredOffset 1000.0 200.0 1000.0
        Assert.Equal(200.0, same)

        // 边界：新 extent 减少（极端情况），不产生负 delta
        let shrunk = ScrollAnchorMath.computeRestoredOffset 1000.0 200.0 800.0
        Assert.Equal(200.0, shrunk)

        // 边界：初始 offset 为 0，增加了 350
        let fromTop = ScrollAnchorMath.computeRestoredOffset 500.0 0.0 850.0
        Assert.Equal(350.0, fromTop)

    [<Fact>]
    let ``EffectSink RunSync executes effects in strict sequence`` () =
        let executionLog = ResizeArray<string>()

        let sink: EffectSink =
            {
                Focus = fun target -> executionLog.Add $"Focus:{target}"
                ScrollChat = fun cmd -> executionLog.Add $"Scroll:{cmd}"
                SendWire = fun _ -> executionLog.Add "SendWire"
                After = fun (ms, _) -> executionLog.Add $"After:{ms}"
                Every = fun (h, ms) -> executionLog.Add $"Every:{h}:{ms}"
                StopEvery = fun h -> executionLog.Add $"StopEvery:{h}"
                ShowToast = fun (text, tone) -> executionLog.Add $"Toast:{text}:{tone}"
                Clipboard = fun text -> executionLog.Add $"Clipboard:{text}"
                OpenLink = fun url -> executionLog.Add $"OpenLink:{url}"
                SaveDownload = fun (f, _) -> executionLog.Add $"SaveDownload:{f}"
                ApplyTheme = fun theme -> executionLog.Add $"ApplyTheme:{theme}"
                SetWindowBusy = fun b -> executionLog.Add $"Busy:{b}"
                CloseWindow = fun () -> executionLog.Add "CloseWindow"
                PrefsSave = fun _ -> executionLog.Add "PrefsSave"
                PrefsLoad = fun () -> executionLog.Add "PrefsLoad"
                DraftWrite = fun _ -> executionLog.Add "DraftWrite"
                BrowserHook = fun h -> executionLog.Add $"BrowserHook:{h}"
            }

        let effects =
            [
                Effect.ScrollChat ScrollCommand.PreparePrepend
                Effect.Focus FocusTarget.ComposerInput
                Effect.ShowToast("Hello", Wanxiang2.WorkspaceState.ToastTone.Success)
                Effect.Clipboard "CopiedText"
                Effect.ScrollChat ScrollCommand.RestorePrepend
                Effect.CloseWindow
            ]

        sink.RunSync effects

        let expected =
            [
                "Scroll:PreparePrepend"
                "Focus:ComposerInput"
                "Toast:Hello:Success"
                "Clipboard:CopiedText"
                "Scroll:RestorePrepend"
                "CloseWindow"
            ]

        Assert.Equal<string list>(expected, List.ofSeq executionLog)

/// 内嵌 CJK 字体的契约（2026-10-21 PWA 中文豆腐块）。
///
/// PWA 里所有中文标签一度整片渲染成方框：根因不是字体缺字、也不是 WebGL，
/// 而是**根可视树没指定字体**——`TextElement.FontFamily` 是 inherits 的附加属性，
/// 不在根上设，控件就都用默认字体族；浏览器端没有系统字体，默认族里没有汉字，
/// 于是全是豆腐块。修法是 PWA 入口在根控件上设 `ThemeFonts.body`。
///
/// 这条用例钉住两件事，防止再退化：
/// 1) 内嵌字体族**能解析出字形**（`avares://` 资源在 wasm 端也要能加载）；
/// 2) 字体**覆盖常用汉字**（缺字就还是豆腐块）。
module EmbeddedFontTests =

    open Avalonia.Media
    open Wanxiang2.ThemeFonts

    [<Fact>]
    let ``内嵌正文字体族能解析出字形`` () =
        Headless.run (fun () ->
            let family = FontFamily ThemeFonts.body
            let mutable glyph = Unchecked.defaultof<GlyphTypeface>
            Assert.True(FontManager.Current.TryGetGlyphTypeface(Typeface(family), &glyph))
            Assert.Equal("Sarasa Gothic SC", glyph.FamilyName))

    [<Fact>]
    let ``内嵌正文字体覆盖常用汉字`` () =
        Headless.run (fun () ->
            let family = FontFamily ThemeFonts.body
            let mutable typeface = Typeface.Default
            // 「中」「文」「万」「象」——界面上无处不在的字。
            for cp in [ 0x4E2D; 0x6587; 0x4E07; 0x8C61 ] do
                Assert.True(
                    FontManager.Current.TryMatchCharacter(
                        cp,
                        FontStyle.Normal,
                        FontWeight.Normal,
                        FontStretch.Normal,
                        family,
                        null,
                        &typeface
                    ),
                    sprintf "内嵌字体缺字 U+%04X" cp
                ))

    [<Fact>]
    let ``等宽字体把正文字体列为回落（否则中文变豆腐块）`` () =
        // 等宽子集只含拉丁与符号；不把正文字体列为回落，中文段落就全是方框。
        Assert.Contains("Sarasa Gothic SC", ThemeFonts.mono)

/// 弹出层（ToolTip / ContextMenu / MenuFlyoutPresenter）的字体契约。
///
/// 弹出层挂在**独立的弹出根**上，不在可视树里，继承不到根控件上设的
/// `TextElement.FontFamily`；browser-wasm 又拿不到系统字体，于是弹出层里的中文
/// 会渲染成豆腐块。这里钉住两件事：
/// 1) 应用样式里三类弹出层（以及菜单项）都被显式指定了内嵌正文字体；
/// 2) 该字体的复合回落能覆盖界面用到的符号（⌘ ⚙ ✎ ✓ ✕ 📦 🔍 🗑）。
module PopupFontTests =

    open Avalonia.Controls
    open Avalonia.Controls.Primitives
    open Avalonia.Media
    open Avalonia.Styling
    open Avalonia.Threading
    open Wanxiang2.ThemeFonts

    /// 找出针对该控件类型设置了 `FontFamily` 的应用样式。
    ///
    /// `Selector.TargetType` 在 Avalonia 12.1.2 里是 internal，外面看不见；
    /// `Selector.ToString()` 是 public，且对 `OfType<T>()` 选择器渲染的就是目标类型名
    /// （见 `TypeNameAndClassSelector.BuildSelectorString`），因此用它来定位样式。
    /// 组合选择器（如 `Descendant().OfType<MenuItem>()`）的字符串是「父 子」，
    /// 不会与单个类型名相等，不会误命中。
    let private fontSetterFor (app: Application) (t: Type) =
        app.Styles
        |> Seq.choose (fun s ->
            match s with
            | :? Style as st -> Some st
            | _ -> None)
        |> Seq.filter (fun s ->
            match s.Selector with
            | null -> false
            | sel -> sel.ToString() = t.Name)
        |> Seq.collect (fun s -> s.Setters)
        |> Seq.choose (fun s ->
            match s with
            | :? Setter as st -> Some st
            | _ -> None)
        |> Seq.tryFind (fun s -> s.Property = TemplatedControl.FontFamilyProperty)

    [<Fact>]
    let ``弹出层三类都被指定了内嵌正文字体`` () =
        Headless.run (fun () ->
            // Application.Current 是可空的：没有应用实例就谈不上「应用级样式」。
            let app =
                match Application.Current with
                | null -> failwith "没有应用实例"
                | a -> a

            let expected = FontFamily ThemeFonts.body

            for t in PopupFonts.popupTypes do
                let setter = fontSetterFor app t
                Assert.True(setter.IsSome, sprintf "%s 没有设置 FontFamily" t.Name)
                let st = setter.Value :?> Setter
                let family = st.Value :?> FontFamily
                Assert.True(family = expected, sprintf "%s 的字体不是内嵌正文字体" t.Name))

    [<Fact>]
    let ``弹出层控件在窗口里真的用上了内嵌正文字体`` () =
        Headless.run (fun () ->
            // 光有样式不够：弹出层控件得真的被样式命中。
            // 放进窗口内容里，控件就有了逻辑父级，样式（应用的 Styles 经 TopLevel 下发）会真正套用。
            let toolTip = ToolTip(Content = "对话操作")
            let contextMenu = ContextMenu()
            let menuItem = MenuItem(Header = "复制")
            let flyout = MenuFlyoutPresenter()
            let host = StackPanel()
            host.Children.Add toolTip
            host.Children.Add contextMenu
            host.Children.Add menuItem
            host.Children.Add flyout
            let w = Window(Width = 800.0, Height = 600.0, Content = host)

            try
                w.Show()
                Dispatcher.UIThread.RunJobs()
                let expected = FontFamily ThemeFonts.body

                for c in
                    [
                        toolTip :> TemplatedControl
                        contextMenu :> TemplatedControl
                        menuItem :> TemplatedControl
                        flyout :> TemplatedControl
                    ] do
                    Assert.True(c.FontFamily = expected, sprintf "%s 实际字体不是内嵌正文字体" (c.GetType().Name))
            finally
                w.Close()
                Dispatcher.UIThread.RunJobs())

    [<Fact>]
    let ``弹出层字体的复合回落覆盖界面符号`` () =
        Headless.run (fun () ->
            let family = FontFamily ThemeFonts.body

            for cp in [ 0x2318; 0x2699; 0x270E; 0x2713; 0x2715; 0x1F4E6; 0x1F50D; 0x1F5D1 ] do
                let matched, typeface =
                    FontManager.Current.TryMatchCharacter(
                        cp,
                        FontStyle.Normal,
                        FontWeight.Normal,
                        FontStretch.Normal,
                        family,
                        null
                    )

                Assert.True(matched, sprintf "弹出层字体缺字 U+%04X" cp)
                let ok, glyph = FontManager.Current.TryGetGlyphTypeface(typeface)
                Assert.True(ok, sprintf "U+%04X 匹配到的字族没有字形" cp)
                Assert.NotEqual(0us, glyph.CharacterToGlyphMap.[cp]))
