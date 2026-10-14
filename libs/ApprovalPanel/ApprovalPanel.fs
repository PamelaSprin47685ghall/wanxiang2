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

/// 审批卡外观。全部由主题管理者提供活的实例。
type ApprovalPanelLook =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      Warning: IBrush
      /// 扳手图标（插槽）
      Wrench: IBrush -> Control
      BodyFontSize: IObservable<float>
      CaptionFontSize: IObservable<float>
      MicroFontSize: IObservable<float>
      CaptionLineHeight: IObservable<float>
      MonoFont: IObservable<FontFamily>
      /// 触控命中下限（旧 Spacing.Hit.touch = 44）
      TouchTarget: IObservable<float>
      /// 按钮行最大宽度（旧 360）
      ButtonRowMaxWidth: IObservable<float>
      /// 卡内纵向间距、按钮间距、面板卡间距（旧 spaceMd）
      Gap: IObservable<float>
      /// 图标与正文的间距（旧 spaceXl）
      IconGap: IObservable<float>
      CardPadding: IObservable<Thickness>
      CardRadius: IObservable<CornerRadius> }

/// 审批卡（迁自旧 Views/ToolApprovalCard.fs 的 ToolApprovalCard 模块）。
module ApprovalCard =

    let private live (o: IObservable<'T>) f = o.Subscribe(fun v -> f v) |> ignore

    /// 渲染一张审批卡。`decide true` = 批准，`false` = 拒绝；只发一次，之后两键置灰并显示「已发送」。
    let build (look: ApprovalPanelLook) (texts: ApprovalTexts) (req: ApprovalAsk) (decide: bool -> unit) : Control =
        let icon = look.Wrench look.Warning
        icon.VerticalAlignment <- VerticalAlignment.Top
        let title = TextBlock(Text = "模型想使用工具，需要你批准", FontWeight = FontWeight.Medium, Foreground = look.Text, TextWrapping = TextWrapping.Wrap)
        let summary = TextBlock(Text = req.Summary, Foreground = look.Text, TextWrapping = TextWrapping.Wrap)
        for t in [ title; summary ] do live look.BodyFontSize (fun v -> t.FontSize <- v)
        AutomationProperties.SetAutomationId(summary, "tool-approval-summary")
        let note = TextBlock(Text = texts.PolicyNote req.Policy, Foreground = look.Muted, TextWrapping = TextWrapping.Wrap)
        live look.CaptionFontSize (fun v -> note.FontSize <- v)
        live look.CaptionLineHeight (fun v -> note.LineHeight <- v)
        let status = TextBlock(Text = "", Foreground = look.Muted, IsVisible = false)
        live look.CaptionFontSize (fun v -> status.FontSize <- v)
        AutomationProperties.SetAutomationId(status, "tool-approval-status")
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        let mutable decided = false
        let mutable buttons: Tap list = []
        let choose approved () =
            if not decided then
                decided <- true
                for b in buttons do b.SetEnabled false
                status.Text <- texts.SentText approved
                status.IsVisible <- true
                decide approved
        let approve = Tap("批准", TapTone.primary, choose true)
        let deny = Tap("拒绝", TapTone.secondary, choose false)
        buttons <- [ approve; deny ]
        AutomationProperties.SetAutomationId(approve, "tool-approval-approve")
        AutomationProperties.SetAutomationId(deny, "tool-approval-deny")
        AutomationProperties.SetName(approve, sprintf "批准工具调用：%s" req.Summary)
        AutomationProperties.SetName(deny, sprintf "拒绝工具调用：%s" req.Summary)
        // 触控优先：两键等宽平分一行，命中高度不低于触控下限。
        for b in buttons do
            b.HorizontalAlignment <- HorizontalAlignment.Stretch
            live look.TouchTarget (fun v -> b.MinHeight <- v)
        let row = Grid(HorizontalAlignment = HorizontalAlignment.Stretch)
        live look.Gap (fun v -> row.ColumnSpacing <- v)
        let cols = [ for _ in 1..2 -> ColumnDefinition(GridLength.Star) ]
        for c in cols do row.ColumnDefinitions.Add c
        live look.ButtonRowMaxWidth (fun v -> for c in cols do c.MaxWidth <- v / 2.0)
        Grid.SetColumn(approve, 0)
        Grid.SetColumn(deny, 1)
        row.Children.Add approve
        row.Children.Add deny
        // 原始参数只出现一处：默认收起，点「查看参数」展开全文。
        let hasArgs = not (String.IsNullOrWhiteSpace req.ArgumentsJson) && req.ArgumentsJson <> "{}"
        let args =
            TextBlock(Text = (if hasArgs then texts.DisplayArguments req.ArgumentsJson else ""), Foreground = look.Faint, TextWrapping = TextWrapping.Wrap, IsVisible = false)
        live look.MicroFontSize (fun v -> args.FontSize <- v)
        live look.MonoFont (fun v -> args.FontFamily <- v)
        AutomationProperties.SetAutomationId(args, "tool-approval-args")
        let mutable expanded = false
        let mutable toggle: Tap option = None
        let flip () =
            expanded <- not expanded
            args.IsVisible <- expanded
            toggle |> Option.iter (fun t -> t.SetText(texts.ArgsToggleText expanded))
        let t = Tap(texts.ArgsToggleText false, TapTone.ghost, flip)
        toggle <- Some t
        t.IsVisible <- hasArgs
        t.HorizontalAlignment <- HorizontalAlignment.Left
        live look.TouchTarget (fun v -> t.MinHeight <- v)
        AutomationProperties.SetAutomationId(t, "tool-approval-args-toggle")
        let column = StackPanel(Orientation = Orientation.Vertical)
        live look.Gap (fun v -> column.Spacing <- v)
        for c in [ title :> Control; summary; args; t; note; row; status ] do column.Children.Add c
        // Auto/Star 网格：窄屏上摘要换行而不是被裁掉。
        let body = Grid()
        live look.IconGap (fun v -> body.ColumnSpacing <- v)
        body.ColumnDefinitions.Add(ColumnDefinition(GridLength.Auto))
        body.ColumnDefinitions.Add(ColumnDefinition(GridLength.Star))
        Grid.SetColumn(icon, 0)
        Grid.SetColumn(column, 1)
        body.Children.Add icon
        body.Children.Add column
        let card = Border(Child = body)
        card.Classes.Add "grouping-card"
        live look.CardPadding (fun v -> card.Padding <- v)
        live look.CardRadius (fun v -> card.CornerRadius <- v)
        AutomationProperties.SetAutomationId(card, sprintf "tool-approval-%O" req.ApprovalId)
        AutomationProperties.SetName(card, sprintf "工具审批：%s" req.Summary)
        card :> Control

/// 对话内的审批卡宿主（迁自旧 ToolApprovalPanel）：只显示当前对话的待审批项，内容全由主机事件驱动。
type ApprovalPanel(look: ApprovalPanelLook, texts: ApprovalTexts, decide: Guid -> bool -> unit) as this =
    inherit StackPanel()

    let pending = Collections.Generic.List<ApprovalAsk>()
    let mutable conversation: Guid option = None

    let refresh () =
        this.Children.Clear()
        for req in pending do
            if conversation = Some req.ConversationId then
                this.Children.Add(ApprovalCard.build look texts req (fun ok -> decide req.ApprovalId ok))
        this.IsVisible <- this.Children.Count > 0

    do
        this.Orientation <- Orientation.Vertical
        look.Gap.Subscribe(fun v -> this.Spacing <- v) |> ignore
        this.VerticalAlignment <- VerticalAlignment.Bottom
        // 宽度与边距跟随宿主列，自己不定 MaxWidth。
        this.HorizontalAlignment <- HorizontalAlignment.Stretch
        this.IsVisible <- false

    member _.Pending: ApprovalAsk list = List.ofSeq pending
    member _.AwaitingCallIds: Set<string> = pending |> Seq.map _.CallId |> Set.ofSeq

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
