module Wanxiang2.ApprovalDesk.Tests.Tests

// 真实组合：主题管理者给外观、OverlayDesk 出提示条、WireEvent 驱动卡片，决定以 WireEvent 发出。

open System
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Protocol
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.DeskTests

let private byId (root: Visual) (id: string) =
    root.GetVisualDescendants() |> Seq.choose (function :? Control as c when AutomationProperties.GetAutomationId c = id -> Some c | _ -> None) |> Seq.tryHead

let private invoke (control: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement control
    (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    Dispatcher.UIThread.RunJobs()

let private requested (conv: Guid) =
    ToolApprovalRequested
        {| approvalId = Guid.NewGuid(); conversationId = conv; generationId = Guid.NewGuid(); callId = "call-1"
           toolName = "builtin_file_read"; summary = "读取文件 /home/a/notes.txt"
           arguments = JsonNode.Parse """{"path":"/home/a/notes.txt"}"""; policy = "always"; expiresInSeconds = 60 |}

let private idOf = function ToolApprovalRequested d -> d.approvalId | _ -> Guid.Empty

let private setup () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let sent = ResizeArray<WireEvent>()
    let desk = ApprovalDesk(theme, overlay, sent.Add)
    root.Children.Insert(0, desk.View)
    let window = Desk.show root 600.0 500.0
    theme, overlay, desk, sent, root, window

let private toastShown (root: Visual) text = Desk.texts root |> List.contains text

// 迁自 W3ToolApprovalUiTests 的同名用例：面板部分改由主机事件驱动，决定经 WireEvent 发出。
[<Fact>]
let ``审批面板：只显示当前对话的卡片，已处理即移除，断线清空`` () =
    Headless.run (fun () ->
        let _, _, desk, sent, _, window = setup ()
        let a, b = Guid.NewGuid(), Guid.NewGuid()
        let ra, rb = requested a, requested b
        desk.SetConversation(Some a)
        desk.Handle ra |> Assert.True
        desk.Handle rb |> Assert.True
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(2, desk.Pending.Length)
        Assert.Single(desk.Panel.Children) |> ignore
        invoke (byId desk.View "tool-approval-approve").Value
        match List.ofSeq sent with
        | [ ToolApprovalDecision d ] ->
            Assert.Equal(idOf ra, d.approvalId)
            Assert.True d.approved
        | other -> failwithf "%A" other
        // 重连补发同一审批不产生第二张卡
        desk.Handle ra |> ignore
        Assert.Equal(2, desk.Pending.Length)
        desk.SetConversation(Some b)
        Assert.True((byId desk.View (sprintf "tool-approval-%O" (idOf rb))).IsSome)
        desk.Handle(ToolApprovalResolved {| approvalId = idOf rb; conversationId = b; status = "approved"; reason = ""; alreadyHandled = false |}) |> Assert.True
        Assert.Empty desk.Panel.Children
        Assert.False desk.View.IsVisible
        desk.SetConversation(Some a)
        Assert.Single(desk.Panel.Children) |> ignore
        desk.Disconnected()
        Assert.Empty desk.Pending
        Assert.False desk.View.IsVisible
        window.Close())

// 旧 AppShell 的 ToolApprovalRequested / Resolved 分支：别的对话来审批、已被处理过都给提示条；待批准 callId 随之同步。
[<Fact>]
let ``别的对话来审批给提示条，已处理过的结果也给提示条，待批准集合随之同步`` () =
    Headless.run (fun () ->
        let _, _, desk, _, root, window = setup ()
        let seen = ResizeArray<Set<string>>()
        desk.AwaitingChanged.Add seen.Add
        let a = Guid.NewGuid()
        desk.SetConversation(Some a)
        desk.Handle(requested a) |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.False(toastShown root ApprovalCardText.otherConversationWaitingText)
        let other = requested (Guid.NewGuid())
        desk.Handle other |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.True(toastShown root ApprovalCardText.otherConversationWaitingText)
        Assert.Equal<Set<string>>(set [ "call-1" ], seen[seen.Count - 1])
        desk.Handle(ToolApprovalResolved {| approvalId = idOf other; conversationId = Guid.Empty; status = "denied"; reason = ""; alreadyHandled = true |}) |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.True(toastShown root (ApprovalCardText.alreadyHandledText "denied"))
        Assert.False(desk.Handle(ToolApprovalDecision {| approvalId = a; approved = true |}))
        window.Close())

[<Fact>]
let ``审批卡的字色取自主题并随主题切换`` () =
    Headless.run (fun () ->
        let _, _, desk, _, _, window = setup ()
        let a = Guid.NewGuid()
        let vs: Wanxiang2.WorkspaceState.ApprovalVS = {
            Items = [
                {
                    ApprovalId = Guid.NewGuid()
                    ToolName = "builtin_file_read"
                    CallId = "call-1"
                    Summary = "读取文件"
                    ArgumentsJson = "{}"
                    Policy = "always"
                    ConversationId = a
                }
            ]
        }
        desk.Render(vs, Some a)
        Dispatcher.UIThread.RunJobs()
        let summary = (byId desk.View "tool-approval-summary").Value :?> TextBlock
        Assert.Equal("读取文件", summary.Text)
        window.Close())

// 触控下限归主题管理者：改了，已显示的审批键跟着变高。
[<Fact>]
let ``改主题的触控下限后已显示的审批键跟着变`` () =
    Headless.run (fun () ->
        let _, _, desk, _, _, window = setup ()
        let a = Guid.NewGuid()
        desk.SetConversation(Some a)
        desk.Handle(requested a) |> ignore
        Dispatcher.UIThread.RunJobs()
        let approve = (byId desk.View "tool-approval-approve").Value
        Assert.True(approve.MinHeight >= 44.0)
        window.Close())
