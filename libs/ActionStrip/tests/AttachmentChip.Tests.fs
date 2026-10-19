module Wanxiang2.AttachmentChip.Tests.Tests

open System
open Avalonia
open Avalonia.Layout
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.AttachmentChip
open Wanxiang2.AttachmentChip.Tests
open Wanxiang2.WorkspaceState
let private data : AttachmentChipVS = { FileName = "a.png"; SizeText = "1.2 KB"; IsImage = true; Missing = false }

[<Fact>]
let ``图片附件可点，提示是查看或下载`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(data)
        let mutable hits = 0
        c.Invoked.Add(fun () -> hits <- hits + 1)
        c.Invoke()
        Assert.Equal(1, hits)
        Assert.Equal("查看或下载图片 a.png", AutomationProperties.GetName c.Host)
        Assert.Contains("hover-raised", c.Host.Classes)
        let row =
            match c.Host with
            | :? Button as b -> b.Content :?> StackPanel
            | :? Border as b -> b.Child :?> StackPanel
            | _ -> failwith "unexpected host type"
        Assert.Equal(4, row.Children.Count)
        Assert.Equal(box ChipGlyph.Image, row.Children[0].Tag))

[<Fact>]
let ``普通文件提示下载附件`` () =
    Headless.run (fun () ->
        let c = AttachmentChip({ data with IsImage = false; FileName = "b.pdf" })
        Assert.Equal("下载附件 b.pdf", AutomationProperties.GetName c.Host))

[<Fact>]
let ``丢失的附件变灰、不可点，仍有自描述`` () =
    Headless.run (fun () ->
        let c = AttachmentChip({ data with Missing = true })
        let mutable hits = 0
        c.Invoked.Add(fun () -> hits <- hits + 1)
        c.Invoke()
        Assert.Equal(0, hits)
        Assert.Equal("附件「a.png」内容已丢失", AutomationProperties.GetName c.Host)
        Assert.DoesNotContain("hover-raised", c.Host.Classes)
        let row = (c.Host :?> Border).Child :?> StackPanel
        Assert.Equal(3, row.Children.Count)
        Assert.Equal("内容已丢失", (row.Children[2] :?> TextBlock).Text))

[<Fact>]
let ``附件条有正确边距和圆角`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(data)
        Assert.Equal(Thickness(0.0, 4.0, 0.0, 0.0), c.Host.Margin)
        match c.Host with
        | :? Button as b -> Assert.Equal(CornerRadius 6.0, b.CornerRadius)
        | :? Border as b -> Assert.Equal(CornerRadius 6.0, b.CornerRadius)
        | _ -> failwith "unexpected host type")

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``附件条有读屏 Invoke 且触发下载`` () =
    Headless.run (fun () ->
        let c = AttachmentChip(data)
        let mutable hits = 0
        c.Invoked.Add(fun () -> hits <- hits + 1)
        invokeVia c.Host
        Assert.Equal(1, hits))
