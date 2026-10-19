module Wanxiang2.UserBubble.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.UserBubble
open Wanxiang2.UserBubble.Tests

[<Fact>]
let ``气泡右下角收小、可聚焦、有读屏名，头像在右`` () =
    Headless.run (fun () ->
        let b = UserBubble(Border())
        Assert.Equal(Avalonia.CornerRadius(12.0, 12.0, 6.0, 12.0), b.Bubble.CornerRadius)
        Assert.True b.Bubble.Focusable
        Assert.Equal("用户消息", AutomationProperties.GetName b.Bubble)
        let row = b.Host :?> StackPanel
        Assert.Same(b.Avatar, row.Children[1])
        Assert.Equal("你", (b.Avatar.Child :?> TextBlock).Text)
        Assert.Contains("hover-plain", b.Bubble.Classes))

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
