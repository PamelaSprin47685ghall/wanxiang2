namespace Wanxiang2.ApprovalPanel

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

/// 一条等待用户决定的工具审批（运行时状态，只在内存里）。
type ApprovalAsk =
    { ApprovalId: Guid
      ConversationId: Guid
      ToolName: string
      /// 对应的工具调用
      CallId: string
      /// 人话摘要
      Summary: string
      ArgumentsJson: string
      /// "first"（首次询问）或 "always"（每次询问）
      Policy: string }

/// 卡上的文案（插槽）：组合者填 ApprovalCardText 的同名函数。
type ApprovalTexts =
    { PolicyNote: string -> string
      SentText: bool -> string
      DisplayArguments: string -> string
      ArgsToggleText: bool -> string }

/// 审批卡（迁自旧 Views/ToolApprovalCard.fs 的 ToolApprovalCard 模块）。
module ApprovalCard =

    /// 渲染一张审批卡。`decide true` = 批准，`false` = 拒绝；只发一次，之后两键置灰并显示「已发送」。
    let build (wrench: Control option) (texts: ApprovalTexts) (req: ApprovalAsk) (decide: bool -> unit) : Control =
        let icon =
            match wrench with
            | Some w -> w
            | None ->
                let b = Border(Width = 16.0, Height = 16.0)
                b.Classes.Add "warning-icon"
                b :> Control
        icon.VerticalAlignment <- VerticalAlignment.Top
        let title = TextBlock(Text = "模型想使用工具，需要你批准", FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap)
        let summary = TextBlock(Text = req.Summary, TextWrapping = TextWrapping.Wrap)
        AutomationProperties.SetAutomationId(summary, "tool-approval-summary")
        let note = TextBlock(Text = texts.PolicyNote req.Policy, TextWrapping = TextWrapping.Wrap)
        note.Classes.Add "muted"
        let status = TextBlock(Text = "", IsVisible = false)
        status.Classes.Add "muted"
        AutomationProperties.SetAutomationId(status, "tool-approval-status")
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        let mutable decided = false
        let mutable buttons: Button list = []
        let choose approved () =
            if not decided then
                decided <- true
                for b in buttons do b.IsEnabled <- false
                status.Text <- texts.SentText approved
                status.IsVisible <- true
                decide approved
        let approve = Button(Content = "批准")
        approve.Classes.Add "primary"
        approve.Click.Add(fun _ -> choose true ())
        let deny = Button(Content = "拒绝")
        deny.Classes.Add "secondary"
        deny.Click.Add(fun _ -> choose false ())
        buttons <- [ approve; deny ]
        AutomationProperties.SetAutomationId(approve, "tool-approval-approve")
        AutomationProperties.SetAutomationId(deny, "tool-approval-deny")
        AutomationProperties.SetName(approve, sprintf "批准工具调用：%s" req.Summary)
        AutomationProperties.SetName(deny, sprintf "拒绝工具调用：%s" req.Summary)
        // 触控优先：两键等宽平分一行，命中高度不低于触控下限。
        for b in buttons do
            b.HorizontalAlignment <- HorizontalAlignment.Stretch
            b.MinHeight <- 44.0
        let row = Grid(HorizontalAlignment = HorizontalAlignment.Stretch, ColumnSpacing = 8.0)
        let cols = [ for _ in 1..2 -> ColumnDefinition(GridLength.Star) ]
        for c in cols do row.ColumnDefinitions.Add c
        for c in cols do c.MaxWidth <- 180.0
        Grid.SetColumn(approve, 0)
        Grid.SetColumn(deny, 1)
        row.Children.Add approve
        row.Children.Add deny
        // 原始参数只出现一处：默认收起，点「查看参数」展开全文。
        let hasArgs = not (String.IsNullOrWhiteSpace req.ArgumentsJson) && req.ArgumentsJson <> "{}"
        let args =
            TextBlock(Text = (if hasArgs then texts.DisplayArguments req.ArgumentsJson else ""), TextWrapping = TextWrapping.Wrap, IsVisible = false)
        args.Classes.Add "code-box"
        AutomationProperties.SetAutomationId(args, "tool-approval-args")
        let mutable expanded = false
        let mutable toggle: Button option = None
        let flip () =
            expanded <- not expanded
            args.IsVisible <- expanded
            let txt = texts.ArgsToggleText expanded
            toggle |> Option.iter (fun t ->
                t.Content <- txt
                AutomationProperties.SetName(t, txt))
        let initialToggleText = texts.ArgsToggleText false
        let t = Button(Content = initialToggleText)
        t.Classes.Add "ghost"
        t.Click.Add(fun _ -> flip ())
        AutomationProperties.SetName(t, initialToggleText)
        toggle <- Some t
        t.IsVisible <- hasArgs
        t.HorizontalAlignment <- HorizontalAlignment.Left
        t.MinHeight <- 44.0
        AutomationProperties.SetAutomationId(t, "tool-approval-args-toggle")
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
        for c in [ title :> Control; summary; args; t; note; row; status ] do column.Children.Add c
        // Auto/Star 网格：窄屏上摘要换行而不是被裁掉。
        let body = Grid(ColumnSpacing = 12.0)
        body.ColumnDefinitions.Add(ColumnDefinition(GridLength.Auto))
        body.ColumnDefinitions.Add(ColumnDefinition(GridLength.Star))
        Grid.SetColumn(icon, 0)
        Grid.SetColumn(column, 1)
        body.Children.Add icon
        body.Children.Add column
        let card = Border(Child = body)
        card.Classes.Add "grouping-card"
        card.Padding <- Thickness(16.0, 12.0)
        card.CornerRadius <- CornerRadius 8.0
        AutomationProperties.SetAutomationId(card, sprintf "tool-approval-%O" req.ApprovalId)
        AutomationProperties.SetName(card, sprintf "工具审批：%s" req.Summary)
        card :> Control

/// 对话内的审批卡宿主：单入口 Render(ApprovalVS)，单出口意图事件 Decide(Guid * bool)。
type ApprovalPanel(texts: ApprovalTexts, wrenchIcon: (unit -> Control) option) as this =
    inherit StackPanel()

    let pending = Collections.Generic.List<ApprovalAsk>()
    let mutable conversation: Guid option = None
    let decideEvent = Event<Guid * bool>()

    let refresh () =
        this.Children.Clear()
        for req in pending do
            if conversation = Some req.ConversationId then
                let icon = wrenchIcon |> Option.map (fun f -> f ())
                this.Children.Add(ApprovalCard.build icon texts req (fun ok -> decideEvent.Trigger(req.ApprovalId, ok)))
        this.IsVisible <- this.Children.Count > 0

    do
        this.Orientation <- Orientation.Vertical
        this.Spacing <- 8.0
        this.VerticalAlignment <- VerticalAlignment.Bottom
        // 宽度与边距跟随宿主列，自己不定 MaxWidth。
        this.HorizontalAlignment <- HorizontalAlignment.Stretch
        this.IsVisible <- false

    new(texts: ApprovalTexts) = ApprovalPanel(texts, None)

    [<CLIEvent>] member _.Decide = decideEvent.Publish

    member _.Pending: ApprovalAsk list = List.ofSeq pending
    member _.AwaitingCallIds: Set<string> = pending |> Seq.map _.CallId |> Set.ofSeq

    /// 单入口幂等渲染：以当前对话的审批列表投影为准
    member _.Render(items: ApprovalAsk list, currentConversation: Guid option) =
        conversation <- currentConversation
        pending.Clear()
        pending.AddRange items
        refresh ()

    /// 新增（或按 ApprovalId 覆盖）一条
    member _.Upsert(req: ApprovalAsk) =
        let i = pending.FindIndex(fun r -> r.ApprovalId = req.ApprovalId)
        if i >= 0 then pending[i] <- req else pending.Add req
        refresh ()

    member _.Remove(approvalId: Guid) =
        if pending.RemoveAll(fun r -> r.ApprovalId = approvalId) > 0 then refresh ()

    /// 断线：先清空，重连后主机重发
    member _.Clear() =
        pending.Clear()
        refresh ()

    member _.SetConversation(convId: Guid option) =
        if conversation <> convId then
            conversation <- convId
            refresh ()
