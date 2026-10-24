module Wanxiang2.UserBubble.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.UserBubble
open Wanxiang2.UserBubble.Tests

[<Fact>]
let ``气泡右下角收小、无边、没有头像、有读屏名`` () =
    Headless.run (fun () ->
        let b = UserBubble(Border())
        Assert.Equal(Avalonia.CornerRadius(12.0, 12.0, 6.0, 12.0), b.Bubble.CornerRadius)
        Assert.Equal(Avalonia.Thickness 0.0, b.Bubble.BorderThickness)
        Assert.False b.Bubble.Focusable
        Assert.Equal("用户消息", AutomationProperties.GetName b.Bubble)
        Assert.Same(b.Bubble :> Control, b.Host)
        Assert.False b.Avatar.IsVisible)

[<Fact>]
let ``正文用阅读行高与无flyout`` () =
    Headless.run (fun () ->
        let t = UserBubble.text "hi" 15.0 :?> SelectableTextBlock
        Assert.Equal(15.0 * 1.62, t.LineHeight)
        Assert.Null t.ContextFlyout)

[<Fact>]
let ``最大宽设定为 560`` () =
    Headless.run (fun () ->
        let b = UserBubble(Border())
        Assert.Equal(560.0, b.Bubble.MaxWidth))
