module Wanxiang2.ApprovalPanel.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ApprovalPanel

let rec descendants (control: Control) : seq<Control> =
    seq {
        yield control
        match control with
        | :? Panel as panel -> for child in panel.Children do yield! descendants child
        | :? Decorator as d when not (isNull d.Child) -> yield! descendants d.Child
        | :? ContentControl as host ->
            match host.Content with
            | :? Control as child -> yield! descendants child
            | _ -> ()
        | _ -> ()
    }

let byId (root: Control) id = descendants root |> Seq.find (fun c -> AutomationProperties.GetAutomationId c = id)

let invoke (control: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement control
    try
        (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    with :? Avalonia.Automation.ElementNotEnabledException -> ()
    Dispatcher.UIThread.RunJobs()

let textOf (c: Control) = match c with :? TextBlock as t when not (isNull t.Text) -> t.Text | _ -> ""

/// 与 ApprovalCardText 同文（组合者填的就是那个库）
let texts =
    { PolicyNote = (function "first" -> "批准后，这个对话里再用同一个工具不会再问。" | _ -> "每次调用这个工具都会询问。")
      SentText = (fun ok -> if ok then "已批准，正在继续…" else "已拒绝，正在通知模型…")
      DisplayArguments = id
      ArgsToggleText = (fun e -> if e then "收起参数" else "查看参数") }

let touch = 44.0

let request conv =
    { ApprovalId = Guid.NewGuid()
      ConversationId = conv
      CallId = "call-1"
      ToolName = "builtin_file_read"
      Summary = "读取文件 /home/a/notes.txt"
      ArgumentsJson = """{"path":"/home/a/notes.txt"}"""
      Policy = "always" }

let longRequest () =
    { request (Guid.NewGuid()) with
        Summary = "读取文件 /home/a/very/long/path/that/should/wrap/on/a/phone/screen/notes-and-more-notes.txt"
        ArgumentsJson = """{"path":"/home/a/very/long/path/that/should/wrap/on/a/phone/screen/notes-and-more-notes.txt"}"""
        Policy = "first" }

let layoutAt (width: float) (control: Control) =
    let host = Border(Width = width, Child = control)
    host.Measure(Size(width, infinity))
    host.Arrange(Rect(0.0, 0.0, width, host.DesiredSize.Height))
    Dispatcher.UIThread.RunJobs()

// 迁自旧 W3ToolApprovalUiTests。
[<Fact>]
let ``审批卡：展示人话摘要，批准与拒绝各只触发一次并立即置灰`` () =
    Headless.run (fun () ->
        for approve in [ true; false ] do
            let decisions = ResizeArray<bool>()
            let card = ApprovalCard.build None texts (request (Guid.NewGuid())) decisions.Add
            Assert.Equal("读取文件 /home/a/notes.txt", textOf (byId card "tool-approval-summary"))
            let button = byId card (if approve then "tool-approval-approve" else "tool-approval-deny")
            invoke button
            invoke button
            invoke (byId card (if approve then "tool-approval-deny" else "tool-approval-approve"))
            Assert.Equal<bool list>([ approve ], List.ofSeq decisions)
            Assert.False button.IsEnabled
            let status = byId card "tool-approval-status"
            Assert.True status.IsVisible
            Assert.Equal(texts.SentText approve, textOf status)
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting status))

// 迁自旧 W3ToolApprovalUiTests。
[<Fact>]
let ``审批面板：只显示当前对话的卡片，已处理即移除，断线清空`` () =
    Headless.run (fun () ->
        let sent = ResizeArray<Guid * bool>()
        let panel = ApprovalPanel(texts)
        panel.Decide.Add sent.Add
        let a, b = Guid.NewGuid(), Guid.NewGuid()
        let ra, rb = request a, request b
        panel.SetConversation(Some a)
        panel.Upsert ra
        panel.Upsert rb
        Assert.Equal(2, panel.Pending.Length)
        Assert.Single(panel.Children) |> ignore
        Assert.True panel.IsVisible
        invoke (byId panel "tool-approval-approve")
        Assert.Equal<(Guid * bool) list>([ ra.ApprovalId, true ], List.ofSeq sent)
        panel.Upsert ra
        Assert.Equal(2, panel.Pending.Length)
        Assert.Single(panel.Children) |> ignore
        panel.SetConversation(Some b)
        let shownIds =
            descendants panel
            |> Seq.map (fun c -> match AutomationProperties.GetAutomationId c with null -> "" | id -> id)
            |> Seq.filter (fun id -> id.StartsWith "tool-approval-" && id.Length > 40)
            |> List.ofSeq
        Assert.Equal<string list>([ sprintf "tool-approval-%O" rb.ApprovalId ], shownIds)
        panel.Remove rb.ApprovalId
        Assert.Empty(panel.Children)
        Assert.False panel.IsVisible
        panel.SetConversation(Some a)
        Assert.Single(panel.Children) |> ignore
        Assert.Equal<Set<string>>(set [ "call-1" ], panel.AwaitingCallIds)
        panel.Clear()
        Assert.Empty(panel.Pending)
        Assert.False panel.IsVisible)

// 迁自旧 PwaToolApprovalTests。
[<Fact>]
let ``审批卡：批准/拒绝/查看参数的命中高度不低于触控下限`` () =
    Headless.run (fun () ->
        let card = ApprovalCard.build None texts (longRequest ()) ignore
        layoutAt 360.0 card
        for id in [ "tool-approval-approve"; "tool-approval-deny"; "tool-approval-args-toggle" ] do
            let c = byId card id
            Assert.True(c.MinHeight >= touch, id)
            Assert.True(c.Bounds.Height >= touch, sprintf "%s height %f" id c.Bounds.Height))

[<Fact>]
let ``审批卡：手机宽度下摘要换行而不溢出，两个按钮等宽且都在卡内`` () =
    Headless.run (fun () ->
        let width = 320.0
        let card = ApprovalCard.build None texts (longRequest ()) ignore
        layoutAt width card
        let summary = byId card "tool-approval-summary"
        Assert.True(summary.Bounds.Width <= width, sprintf "summary width %f" summary.Bounds.Width)
        Assert.True(summary.Bounds.Height > 30.0, "长摘要应换成多行")
        let approve = byId card "tool-approval-approve"
        let deny = byId card "tool-approval-deny"
        Assert.Equal(approve.Bounds.Width, deny.Bounds.Width, 1)
        let right (c: Control) = (c.TranslatePoint(Point(c.Bounds.Width, 0.0), card)).Value.X
        Assert.True(right deny <= width + 0.5, sprintf "deny right %f" (right deny)))

[<Fact>]
let ``审批卡：触屏没有悬停提示，点「查看参数」展开完整参数，再点收起`` () =
    Headless.run (fun () ->
        let card = ApprovalCard.build None texts (longRequest ()) ignore
        let args = byId card "tool-approval-args" :?> TextBlock
        let toggle = byId card "tool-approval-args-toggle"
        Assert.False args.IsVisible
        Assert.Equal("查看参数", AutomationProperties.GetName toggle)
        invoke toggle
        Assert.True args.IsVisible
        Assert.Equal(TextWrapping.Wrap, args.TextWrapping)
        Assert.Equal("收起参数", AutomationProperties.GetName toggle)
        invoke toggle
        Assert.False args.IsVisible)

[<Fact>]
let ``审批卡：没有参数时不显示「查看参数」`` () =
    Headless.run (fun () ->
        let card = ApprovalCard.build None texts { longRequest () with ArgumentsJson = "{}" } ignore
        Assert.False((byId card "tool-approval-args-toggle").IsVisible))

[<Fact>]
let ``审批面板：放在输入框正上方的附属位，与输入框同列同宽，不浮层遮挡消息`` () =
    Headless.run (fun () ->
        let panel = ApprovalPanel(texts)
        Assert.Equal(HorizontalAlignment.Stretch, panel.HorizontalAlignment)
        Assert.True(Double.IsPositiveInfinity panel.MaxWidth))

[<Fact>]
let ``card carries theme classes and the policy note`` () =
    Headless.run (fun () ->
        let card = ApprovalCard.build None texts (longRequest ()) ignore
        Assert.True(card.Classes.Contains "grouping-card")
        Assert.True((byId card "tool-approval-approve").Classes.Contains "primary")
        Assert.True((byId card "tool-approval-deny").Classes.Contains "secondary")
        Assert.True((byId card "tool-approval-args-toggle").Classes.Contains "ghost")
        Assert.Contains("不会再问", descendants card |> Seq.map textOf |> String.concat "|"))
