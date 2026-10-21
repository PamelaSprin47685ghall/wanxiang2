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
            { Focus = fun target -> executionLog.Add $"Focus:{target}"
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
              BrowserHook = fun h -> executionLog.Add $"BrowserHook:{h}" }

        let effects =
            [ Effect.ScrollChat ScrollCommand.PreparePrepend
              Effect.Focus FocusTarget.ComposerInput
              Effect.ShowToast("Hello", Wanxiang2.WorkspaceState.ToastTone.Success)
              Effect.Clipboard "CopiedText"
              Effect.ScrollChat ScrollCommand.RestorePrepend
              Effect.CloseWindow ]

        sink.RunSync effects

        let expected =
            [ "Scroll:PreparePrepend"
              "Focus:ComposerInput"
              "Toast:Hello:Success"
              "Clipboard:CopiedText"
              "Scroll:RestorePrepend"
              "CloseWindow" ]

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
                    FontManager.Current.TryMatchCharacter(cp, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, family, null, &typeface),
                    sprintf "内嵌字体缺字 U+%04X" cp))

    [<Fact>]
    let ``等宽字体把正文字体列为回落（否则中文变豆腐块）`` () =
        // 等宽子集只含拉丁与符号；不把正文字体列为回落，中文段落就全是方框。
        Assert.Contains("Sarasa Gothic SC", ThemeFonts.mono)
