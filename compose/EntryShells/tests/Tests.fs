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
