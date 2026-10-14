module Wanxiang2.ThinkingBlock.Tests.Tests

open System
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ThinkingBlock
open Wanxiang2.ThinkingBlock.Tests

let private edge = Cell 2.0
let private look =
    { Ink = (fun _ -> Brushes.Gray :> IBrush)
      Gauge = (function ThinkingGauge.EdgeWidth -> edge :> IObservable<float> | _ -> Cell 4.0)
      LineHeight = fun s -> s * 1.56 }

[<Fact>]
let ``外框把头部和正文叠起来，左缘竖线宽是活的`` () =
    Headless.run (fun () ->
        let header = Border()
        let body = Border()
        let b = ThinkingBlock(look, header, body)
        let w = Window(Content = b.Host)
        w.Show()
        let card = b.Host :?> Border
        let inner = card.Child :?> Border
        Assert.Equal(2.0, inner.BorderThickness.Left)
        edge.Set 3.0
        Assert.Equal(3.0, inner.BorderThickness.Left)
        Assert.Same(header, (inner.Child :?> StackPanel).Children[0])
        Assert.Equal(4.0, body.Margin.Top)
        w.Content <- null
        Assert.Equal(0, edge.Count)
        edge.Set 2.0
        w.Close())

[<Fact>]
let ``正文比阅读字号小一号半，标题随流式与开合`` () =
    Headless.run (fun () ->
        let t = ThinkingBlock.text look "想" 15.0 :?> SelectableTextBlock
        Assert.Equal(13.5, t.FontSize)
        Assert.Equal(13.5 * 1.56, t.LineHeight)
        Assert.Null t.ContextFlyout
        Assert.Equal("正在思考（收起）", ThinkingBlock.title true true)
        Assert.Equal("已深度思考（展开）", ThinkingBlock.title false false))
