module Wanxiang2.ThinkingBlock.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ThinkingBlock
open Wanxiang2.ThinkingBlock.Tests

[<Fact>]
let ``外框把头部和正文叠起来，边框与边距正确`` () =
    Headless.run (fun () ->
        let header = Border()
        let body = Border()
        let b = ThinkingBlock(header, body)
        let w = Window(Content = b.Host)
        w.Show()
        let card = b.Host :?> Border
        let inner = card.Child :?> Border
        Assert.Equal(Thickness 0.0, card.BorderThickness)
        Assert.Equal(Thickness 0.0, inner.BorderThickness)
        Assert.False(card.Classes.Contains "grouping-card")
        Assert.Same(header, (inner.Child :?> StackPanel).Children[0])
        Assert.Equal(4.0, body.Margin.Top)
        w.Close())

[<Fact>]
let ``正文比阅读字号小一号半，标题随流式与开合`` () =
    Headless.run (fun () ->
        let t = ThinkingBlock.text "想" 15.0 :?> SelectableTextBlock
        Assert.Equal(13.5, t.FontSize)
        Assert.Equal(13.5 * 1.56, t.LineHeight)
        Assert.Null t.ContextFlyout
        Assert.Equal("正在思考（收起）", ThinkingBlock.title true true)
        Assert.Equal("已深度思考（展开）", ThinkingBlock.title false false))
