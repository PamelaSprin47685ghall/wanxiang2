module Wanxiang2.AttachmentChip.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.AttachmentChip
open Wanxiang2.AttachmentChip.Tests

let private nameSize = Cell 13.0
let private look =
    { Ink = (function ChipInk.TextFaint -> Brushes.Silver :> IBrush | ChipInk.Text -> Brushes.Black | _ -> Brushes.Gray)
      Gauge = (function ChipGauge.NameSize -> nameSize :> IObservable<float> | _ -> Cell 4.0)
      Glyph = fun g b -> Border(Tag = g, Background = b)
      Dress = fun b -> b.Classes.Add "raised" }

let private data = { FileName = "a.png"; SizeText = "1.2 KB"; IsImage = true; Missing = false }

[<Fact>]
let ``图片附件可点，提示是查看或下载`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(look, data)
        let mutable hits = 0
        c.Invoked.Add(fun () -> hits <- hits + 1)
        c.Invoke()
        Assert.Equal(1, hits)
        Assert.Equal("查看或下载图片 a.png", AutomationProperties.GetName c.Host)
        Assert.Contains("raised", c.Host.Classes)
        let row = (c.Host :?> Border).Child :?> StackPanel
        Assert.Equal(4, row.Children.Count)
        Assert.Equal(box ChipGlyph.Image, row.Children[0].Tag))

[<Fact>]
let ``普通文件提示下载附件`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(look, { data with IsImage = false; FileName = "b.pdf" })
        Assert.Equal("下载附件 b.pdf", AutomationProperties.GetName c.Host))

[<Fact>]
let ``丢失的附件变灰、不可点，仍有自描述`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(look, { data with Missing = true })
        let mutable hits = 0
        c.Invoked.Add(fun () -> hits <- hits + 1)
        c.Invoke()
        Assert.Equal(0, hits)
        Assert.Equal("附件「a.png」内容已丢失", AutomationProperties.GetName c.Host)
        Assert.DoesNotContain("raised", c.Host.Classes)
        let row = (c.Host :?> Border).Child :?> StackPanel
        Assert.Equal(3, row.Children.Count)
        Assert.Equal("内容已丢失", (row.Children[2] :?> TextBlock).Text)
        Assert.Same(Brushes.Silver, (row.Children[1] :?> TextBlock).Foreground))

[<Fact>]
let ``字号是活的且摘下退订`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(look, data)
        let w = Window(Content = c.Host)
        w.Show()
        nameSize.Set 15.0
        let row = (c.Host :?> Border).Child :?> StackPanel
        Assert.Equal(15.0, (row.Children[1] :?> TextBlock).FontSize)
        w.Content <- null
        Assert.Equal(0, nameSize.Count)
        nameSize.Set 13.0
        w.Close())
