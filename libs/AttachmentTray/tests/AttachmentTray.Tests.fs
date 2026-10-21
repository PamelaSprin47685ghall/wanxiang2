module Wanxiang2.AttachmentTray.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.Threading
open Xunit
open Wanxiang2.AttachmentTray
open Wanxiang2.AttachmentTray.Tests

let private item name =
    {
        Id = Guid.NewGuid()
        FileName = name
        SizeText = "1 KB"
        StateText = "1 KB"
        Tip = "1 KB"
        Name = sprintf "附件：%s（1 KB）" name
        Failed = false
        Ready = true
        IsImage = false
        ShrinkNote = None
        Thumbnail = None
    }

let rec private all (c: Control) : Control list =
    c
    :: [
        for x in c.GetLogicalChildren() do
            match x with
            | :? Control as cc -> yield! all cc
            | _ -> ()
    ]

let private named (root: Control) n =
    all root |> List.find (fun c -> AutomationProperties.GetName c = n)

[<Fact>]
let ``移除按钮有读屏 Invoke；移除后发事件`` () =
    Headless.run (fun () ->
        let t = AttachmentTray()
        let a, b = item "a.txt", item "b.txt"
        let mutable gone = Guid.Empty

        t.OnRemove(fun id ->
            gone <- id
            t.Render([ b ], false))

        t.Render([ a; b ], false)
        Assert.True t.IsShown
        let w = Window(Width = 600.0, Height = 400.0, Content = t.Host)
        w.Show()
        Dispatcher.UIThread.RunJobs()
        let remove = named t.Host "移除附件 a.txt"
        let peer = ControlAutomationPeer.CreatePeerForElement remove
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(a.Id, gone)
        Assert.Equal(1, t.Count)
        w.Close())

[<Fact>]
let ``失败态与缩小提示；清空后隐藏`` () =
    Headless.run (fun () ->
        let t = AttachmentTray()

        let f =
            { item "x.png" with
                Failed = true
                Ready = false
                StateText = "失败"
                ShrinkNote = Some "已缩小到 1024px"
            }

        t.Render([ f ], false)
        Assert.NotNull(named t.Host "已缩小到 1024px")
        Assert.NotNull(named t.Host "附件：x.png（1 KB）")
        t.Render([], false)
        Assert.False t.IsShown)
