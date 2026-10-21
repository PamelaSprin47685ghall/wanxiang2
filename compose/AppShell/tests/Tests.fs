module Wanxiang2.AppShell.Tests.Tests

open Avalonia.Input
open Xunit
open Wanxiang.Core
open Wanxiang.UI

// 宿主桥的单元面：KeyShortcut（Avalonia 按键事件 → KeyChord 四字段 → 意图分派）与
// UnansweredReply（末尾用户消息久无回复时补出的失败卡形状）。装配在 app/Desktop 与 app/Pwa，
// 这里只钉换算与分派规则、失败卡的 kind/message/detail/retryable。

let private keyArgs (key: Key) (modifiers: KeyModifiers) : KeyEventArgs =
    KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers)

[<Fact>]
let ``KeyShortcut 换算按键事件的四字段`` () =
    Headless.run (fun () ->
        let ctrlK = KeyShortcut.ofEvent (keyArgs Key.K KeyModifiers.Control)

        Assert.Equal(
            {
                key = "K"
                ctrl = true
                shift = false
                noModifiers = false
            },
            ctrlK
        )

        let ctrlShiftB =
            KeyShortcut.ofEvent (keyArgs Key.B (KeyModifiers.Control ||| KeyModifiers.Shift))

        Assert.Equal(
            {
                key = "B"
                ctrl = true
                shift = true
                noModifiers = false
            },
            ctrlShiftB
        )

        let bareEscape = KeyShortcut.ofEvent (keyArgs Key.Escape KeyModifiers.None)

        Assert.Equal(
            {
                key = "Escape"
                ctrl = false
                shift = false
                noModifiers = true
            },
            bareEscape
        ))

[<Fact>]
let ``KeyShortcut 分派 Escape、Ctrl+K 与无快捷键`` () =
    Headless.run (fun () ->
        Assert.Equal(ShortcutAction.Escape, keyArgs Key.Escape KeyModifiers.None |> KeyShortcut.resolve)
        Assert.Equal(ShortcutAction.OpenCommandPalette, keyArgs Key.K KeyModifiers.Control |> KeyShortcut.resolve)
        Assert.Equal(ShortcutAction.NoShortcut, keyArgs Key.F5 KeyModifiers.None |> KeyShortcut.resolve))

[<Fact>]
let ``未回复失败卡是可重试的未知失败并带两段文案`` () =
    let e = UnansweredReply.error
    Assert.Equal(GenerationErrorKind.UnknownFailure, e.kind)
    Assert.Equal(ReplyTail.unansweredMessage, e.message)
    Assert.Equal(Some ReplyTail.unansweredDetail, e.detail)
    Assert.True(e.retryable)
