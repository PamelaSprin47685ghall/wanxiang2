module Wanxiang2.UserBubble.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.UserBubble
open Wanxiang2.UserBubble.Tests

let private maxW = Cell 560.0
let private look =
    { Ink = (function BubbleInk.Fill -> Brushes.Black :> IBrush | BubbleInk.Text -> Brushes.White | _ -> Brushes.Gray)
      Gauge = (function BubbleGauge.MaxWidth -> maxW :> IObservable<float> | BubbleGauge.RadiusLg -> Cell 12.0 | BubbleGauge.RadiusSm -> Cell 4.0 | _ -> Cell 8.0)
      Dress = fun b -> b.Classes.Add "surface"
      LineHeight = fun s -> s * 1.62 }

[<Fact>]
let ``气泡右下角收小、可聚焦、有读屏名，头像在右`` () =
    Headless.run (fun () ->
        let b = UserBubble(look, Border())
        Assert.Equal(Avalonia.CornerRadius(12.0, 12.0, 4.0, 12.0), b.Bubble.CornerRadius)
        Assert.True b.Bubble.Focusable
        Assert.Equal("用户消息", AutomationProperties.GetName b.Bubble)
        let row = b.Host :?> StackPanel
        Assert.Same(b.Avatar, row.Children[1])
        Assert.Equal("你", (b.Avatar.Child :?> TextBlock).Text)
        Assert.Contains("surface", b.Bubble.Classes))

[<Fact>]
let ``正文用气泡文字色与阅读行高`` () =
    Headless.run (fun () ->
        let t = UserBubble.text look "hi" 15.0 :?> SelectableTextBlock
        Assert.Same(Brushes.White, t.Foreground)
        Assert.Equal(15.0 * 1.62, t.LineHeight)
        Assert.Null t.ContextFlyout)

[<Fact>]
let ``最大宽是活的`` () =
    Headless.run (fun () ->
        let b = UserBubble(look, Border())
        let w = Window(Content = b.Host)
        w.Show()
        maxW.Set 600.0
        Assert.Equal(600.0, b.Bubble.MaxWidth)
        w.Content <- null
        Assert.Equal(0, maxW.Count)
        maxW.Set 560.0
        w.Close())
