namespace Wanxiang2.MessageCardView

open System
open System.Diagnostics
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.CopyButton
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView
open Wanxiang2.RevealPane
open Wanxiang2.FoldHeader
open Wanxiang2.ThinkingBlock
open Wanxiang2.ToolCallCard
open Wanxiang2.AttachmentChip
open Wanxiang2.ActionStrip
open Wanxiang2.ErrorPanel
open Wanxiang2.UserBubble
open Wanxiang2.StreamCaret
open Wanxiang2.UsageFoot

/// 宿主给消息卡的动作（旧 MessageActions + 旧模块级 linkSink / recoverySink）
type CardActions =
    {
        CopyText: string -> unit
        Regenerate: unit -> unit
        EditAndFork: MessageView -> unit
        DeleteMessage: uint64 -> unit
        DownloadAttachment: string -> unit
        OpenLink: string -> unit
        /// 「复制消息链接」；None 时菜单不出这一项
        CopyLink: (uint64 -> unit) option
        /// 错误卡的恢复动作（打开设置等）；None 时不出恢复按钮
        Recover: (ErrorRecovery -> unit) option
    }

/// 一张消息卡的上下文（旧 MessageContext）
type CardContext =
    {
        FontSize: float
        AutoCollapseReasoning: bool
        /// 是否允许转圈动效（默认 true；减弱动效开启时由宿主填 false 保持静态）
        Spin: bool
        /// 流式进行中的临时消息
        Streaming: bool
        /// 最后一条助手消息：出用量与「重新生成」
        IsLastAssistant: bool
        Usage: GenerationUsage option
        /// 内容已丢失的附件 sha256
        MissingAttachments: Set<string>
        /// 正等用户批准的工具调用 callId
        AwaitingApproval: Set<string>
        /// 品牌头像（BrandMark 归宿主）
        BrandAvatar: unit -> Control
    }

module private CardUi =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

    /// 动作时刻读减弱动效资源（无订阅）；缺省按不减弱。
    let reduced (c: Control) : bool =
        match c.TryFindResource(box "Motion.Reduced") with
        | true, (:? bool as r) -> r
        | _ -> false

    /// 原生加载转圈（12x12 弧形 Path + 声明式 anim-spin 样式类，无 DispatcherTimer）
    let spinner (spin: bool) (tint: IBrush) : Control =
        let p =
            Path(
                Data = Geometry.Parse "M 6 1 A 5 5 0 0 1 11 6",
                Stroke = tint,
                StrokeThickness = 1.4,
                StrokeLineCap = PenLineCap.Round,
                Width = 12.0,
                Height = 12.0
            )

        if spin then
            p.Classes.Add "anim-spin"

        p :> Control

/// 流式光标的呼吸时钟：一段呼吸运行的原点跨卡片重建持久，流式卡每次重建也接得上同一段呼吸
/// （旧 MessageCard 模块级 breathOriginTicks / breathRunLive）。时钟可注入，测试不靠墙钟。
type CaretClock(ticks: unit -> int64, frequency: int64) =
    let mutable origin = 0L
    let mutable live = false
    new() = CaretClock(Stopwatch.GetTimestamp, Stopwatch.Frequency)

    /// 开启一段新的呼吸运行（以峰值相位起步）
    member _.Begin() =
        origin <- ticks ()
        live <- true

    /// 开启一段「仿佛已流逝 ms 毫秒」的运行（测试接缝）
    member _.BeginAtElapsedMs(ms: float) =
        origin <- ticks () - int64 (ms * float frequency / 1000.0)
        live <- true

    member _.IsLive = live
    member _.Origin = origin

    /// 自原点逝去的时间；没有运行中的段落时为零
    member _.Elapsed =
        if live then
            TimeSpan.FromTicks((ticks () - origin) * TimeSpan.TicksPerSecond / frequency)
        else
            TimeSpan.Zero

/// 触屏「点选」的当前消息：点另一条时上一条收起（旧模块级 touchSelectedRelease）
type CardTouch() =
    member val Release: (unit -> unit) option = None with get, set

/// 消息卡共享的宿主件
type CardHost =
    {
        Tint: CodeTint
        MathHtml: string * bool -> string option
        Wrap: CodeWrapMemory
        Clock: CaretClock
        Touch: CardTouch
        /// 当前时刻（脚注时间与倒计时；测试可注入）
        Now: unit -> DateTimeOffset
    }

/// 消息卡渲染（旧 Views/MessageCard.fs）：把 MessageView 折成各控件库的数据，拼成一张卡。
type MessageCards(looks: CardAppearance, host: CardHost) =
    let brush = looks.Brush
    let hand () = new Cursor(StandardCursorType.Hand)

    /// 复制按钮（旧 createCopyButton）：图标按钮，确认时换对勾与「已复制」，淡出走 CopyButton
    member _.CopyButton(copy: string -> unit, tip: string, name: string, getText: unit -> string) : Control =
        let button =
            ActionSpot(
                Focusable = true,
                Margin = Thickness 0.0,
                Padding = Thickness 0.0,
                Cursor = hand (),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        looks.DressIcon button
        let target = LayoutPolicy.inlineActionTarget
        button.Width <- target
        button.Height <- target
        button.MinWidth <- target
        button.MinHeight <- target

        let setIcon glyph ink =
            let g = looks.Icon glyph ink
            // 图标字形与脚注文本基线对齐：纯垂直居中
            g.HorizontalAlignment <- HorizontalAlignment.Center
            g.VerticalAlignment <- VerticalAlignment.Center
            g.Margin <- Thickness 0.0
            button.Content <- g

        let normal () =
            setIcon Glyph.Copy (brush PaletteKey.TextMuted)
            ToolTip.SetTip(button, tip)
            AutomationProperties.SetName(button, name)
            AutomationProperties.SetHelpText(button, name)

        normal ()
        AutomationProperties.SetLiveSetting(button, AutomationLiveSetting.Polite)
        let confirm = CopyButton(button, looks.Copy)

        confirm.StateChanged.Add(fun state ->
            match state with
            | CopyState.Default -> normal ()
            | CopyState.Confirmed
            | CopyState.Fading ->
                setIcon Glyph.Check (brush PaletteKey.Success)
                ToolTip.SetTip(button, "已复制")
                AutomationProperties.SetName(button, "已复制")
                AutomationProperties.SetHelpText(button, "已复制到剪贴板"))

        let fade = Avalonia.Animation.DoubleTransition(Property = Visual.OpacityProperty)

        if isNull button.Transitions then
            button.Transitions <- Avalonia.Animation.Transitions()

        button.Transitions.Add fade
        // 淡出时长是主题的静态时长档；减弱动效在激活时刻读资源键（无订阅）
        fade.Duration <- looks.Copy.Fade

        button.OnInvoke(fun () ->
            let payload = getText ()

            if not (String.IsNullOrEmpty payload) then
                copy payload
                confirm.Copied(CardUi.reduced button))

        button :> Control

    member private _.Reveal(maxHeight: float) =
        fun (content: Control) (visible: bool) ->
            let pane = RevealPane(content, maxHeight, visible)
            pane.Host, (fun v -> pane.ApplyVisible v)

    /// 思考过程（旧 reasoningBlock）
    member this.Thinking
        (ctx: CardContext, actions: CardActions, reasoning: string, durationMs: int64 option)
        : Control =
        let expanded = not (not ctx.Streaming && ctx.AutoCollapseReasoning)

        let pane =
            RevealPane(ThinkingBlock.text reasoning ctx.FontSize, LayoutPolicy.expandedDetailMaxHeight, expanded)

        let fold = FoldHeader(expanded)
        let muted = brush PaletteKey.TextMuted
        let titleRun = FoldHeader.run "" muted
        fold.Caption.Inlines.Add titleRun

        let duration =
            match durationMs with
            | Some ms ->
                let f = DurationText.format ms

                if String.IsNullOrWhiteSpace f then
                    ""
                else
                    sprintf " · %s" f
            | None -> ""

        if not (String.IsNullOrWhiteSpace duration) then
            fold.Caption.Inlines.Add(FoldHeader.run duration muted)
        // 思考过程头带独立复制入口：底部复制只取正文
        fold.AddTrailing(this.CopyButton(actions.CopyText, "复制思考过程", "复制完整思考过程", (fun () -> reasoning)))

        let sync (open': bool) =
            let header = fold.Header
            AutomationProperties.SetName(header, (if open' then "收起思考过程" else "展开思考过程"))
            AutomationProperties.SetHelpText(header, sprintf "%s思考过程" (if open' then "点击折叠" else "点击展开"))
            ToolTip.SetTip(header, (if open' then "收起思考过程" else "展开思考过程"))
            titleRun.Text <- ThinkingBlock.title ctx.Streaming open'

        fold.Toggled.Add(fun open' ->
            pane.ApplyVisible open'
            sync open')

        sync expanded
        ThinkingBlock(fold.Header, pane.Host).Host

    member private _.Status(ctx: CardContext, call: ToolCallView) =
        let outcome =
            call.result |> Option.map (fun r -> ToolErrorSummary.tryExtract (Some r))

        match ToolCallState.resolve ctx.AwaitingApproval ctx.Streaming call.callId outcome with
        | ToolCallState.Failed e -> ToolCallStatus.Failed e
        | ToolCallState.Success -> ToolCallStatus.Success
        | ToolCallState.AwaitingApproval -> ToolCallStatus.AwaitingApproval
        | ToolCallState.Running -> ToolCallStatus.Running
        | ToolCallState.Unknown -> ToolCallStatus.Unknown

    /// 一张工具卡（旧 toolCallCard）
    member this.ToolCard(ctx: CardContext, actions: CardActions, call: ToolCallView) : Control =
        let status = this.Status(ctx, call)

        let tone =
            match status with
            | ToolCallStatus.Running -> ToolTone.Running
            | ToolCallStatus.AwaitingApproval -> ToolTone.Awaiting
            | ToolCallStatus.Success -> ToolTone.Success
            | ToolCallStatus.Failed _ -> ToolTone.Failed
            | ToolCallStatus.Unknown -> ToolTone.Unknown

        let argCount, preview = ToolArgsPreview.preview call.argumentsJson

        let args =
            try
                (if String.IsNullOrWhiteSpace call.argumentsJson then
                     null
                 else
                     System.Text.Json.Nodes.JsonNode.Parse call.argumentsJson)
            with _ ->
                null

        let data =
            {
                CallName = call.name
                DisplayName = ToolSummary.displayNameOfFunction call.name
                Tone = tone
                Badge = ToolCallStatus.badgeText status
                ArgCount = argCount
                Preview = preview
                HumanSummary = ToolSummary.describe call.name args
                Error =
                    (match status with
                     | ToolCallStatus.Failed e -> Some(defaultArg e "执行失败")
                     | _ -> None)
                Sections = ToolCallCard.sections ToolArgsPreview.prettyJson call.argumentsJson call.result tone
                RawPayload = ToolCallCard.rawPayload call.argumentsJson call.result tone
            }

        let parts =
            {
                Copy = fun tip name get -> this.CopyButton(actions.CopyText, tip, name, get)
                Reveal = this.Reveal LayoutPolicy.expandedDetailMaxHeight
            }

        ToolCallCard(data, parts).Host

    /// 工具活动块（旧 toolActivityBlock）：概览头 + 工具卡列表
    member this.ToolActivity(ctx: CardContext, actions: CardActions, calls: ToolCallView list) : Control =
        let statuses = calls |> List.map (fun c -> this.Status(ctx, c))

        let count f =
            statuses |> List.filter f |> List.length

        let counts =
            {
                running =
                    count (function
                        | ToolCallStatus.Running -> true
                        | _ -> false)
                awaiting =
                    count (function
                        | ToolCallStatus.AwaitingApproval -> true
                        | _ -> false)
                success =
                    count (function
                        | ToolCallStatus.Success -> true
                        | _ -> false)
                failed =
                    count (function
                        | ToolCallStatus.Failed _ -> true
                        | _ -> false)
                unknown =
                    count (function
                        | ToolCallStatus.Unknown -> true
                        | _ -> false)
            }

        let text = ToolOverview.text counts
        // 等待批准优先于执行中：需要用户动手的状态最先被看到；绝不显示「完成」对勾配色
        let tint =
            match ToolOverview.tone counts with
            | OverviewTone.Warning -> brush PaletteKey.Warning
            | OverviewTone.Accent -> brush PaletteKey.Accent
            | OverviewTone.Danger -> brush PaletteKey.Danger
            | OverviewTone.Muted -> brush PaletteKey.TextMuted

        let icon =
            match ToolOverview.icon counts with
            | OverviewIcon.Info -> looks.Icon Glyph.Info tint
            | OverviewIcon.Spinner -> CardUi.spinner ctx.Spin tint
            | OverviewIcon.Alert -> looks.Icon Glyph.Alert tint
            | OverviewIcon.Check -> looks.Icon Glyph.Check tint

        icon.VerticalAlignment <- VerticalAlignment.Center
        let cards = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)

        for call in calls do
            cards.Children.Add(this.ToolCard(ctx, actions, call))

        let fold = FoldHeader(true)
        // 旧工具活动头不报展开状态
        fold.ReportState <- false
        fold.AddLeading icon
        let stateRun = FoldHeader.run "（收起）" (brush PaletteKey.TextMuted)
        fold.Caption.Inlines.Add(FoldHeader.run text tint)
        fold.Caption.Inlines.Add stateRun

        fold.Header.Bind(Layoutable.MarginProperty, CardUi.res "Margin.GapBottomMd")
        |> ignore

        AutomationProperties.SetName(fold.Header, text)
        AutomationProperties.SetHelpText(fold.Header, "点击收起工具活动列表")

        fold.Toggled.Add(fun open' ->
            cards.IsVisible <- open'
            stateRun.Text <- (if open' then "（收起）" else "（展开）")
            AutomationProperties.SetName(fold.Header, (if open' then "收起工具活动列表" else "展开工具活动列表"))
            AutomationProperties.SetHelpText(fold.Header, sprintf "%s工具活动" (if open' then "点击收起" else "点击展开")))

        let container = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        container.Children.Add fold.Header
        container.Children.Add cards
        container :> Control

    /// 附件条（旧 attachmentRow）
    member _.Attachment(ctx: CardContext, actions: CardActions, a: AttachmentRef) : Control =
        let chip =
            AttachmentChip(
                {
                    FileName = a.fileName
                    SizeText = AttachmentRef.formatSize a.size
                    IsImage = AttachmentRef.isImage a
                    Missing = ctx.MissingAttachments.Contains a.sha256
                }
            )

        chip.Invoked.Add(fun () -> actions.DownloadAttachment a.sha256)
        chip.Host

    /// 半截回复下方的「未完成」标记（旧 incompleteMarker）
    member _.IncompleteMarker() : Control =
        let warn = brush PaletteKey.Warning
        let icon = looks.Icon Glyph.Alert warn
        icon.VerticalAlignment <- VerticalAlignment.Center

        let label =
            TextBlock(
                Text = "回复未完成：生成中途中断，以上是已写出的部分。",
                Foreground = warn,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            )

        label.Bind(TextBlock.FontSizeProperty, CardUi.res "Metric.FontCaption")
        |> ignore

        let row = StackPanel(Orientation = Orientation.Horizontal)
        row.Bind(StackPanel.SpacingProperty, CardUi.res "Space.Sm") |> ignore
        row.Children.Add icon
        row.Children.Add label
        AutomationProperties.SetName(row, "回复未完成")
        row :> Control

    /// 生成失败卡（旧 errorCardWith）：有半截回复时「继续生成」排在「重试」之前
    member this.ErrorCard
        (error: GenerationError, onRetry: unit -> unit, actions: CardActions, onContinue: (unit -> unit) option)
        : Control =
        let parts =
            {
                Copy = fun tip name get -> this.CopyButton(actions.CopyText, tip, name, get)
                Reveal = this.Reveal LayoutPolicy.expandedDetailMaxHeight
                CountdownText = ProviderRateLimit.countdownText
            }

        let panel = ErrorPanel(parts)
        // 等待建议只跟可重试错误走；截止时刻挂在错误实例上，卡片重建不会让倒计时重来
        let mutable countdownLeft = -1
        let mutable countdownTicking = false
        let mutable countdownAnnounce = false
        let mutable hint = GenerationError.hint error

        match RetryCountdown.tryGet error (host.Now()) with
        | Some info when error.retryable ->
            match info.limit with
            | Some limit when ProviderRateLimit.isExhausted limit -> hint <- "这个模型服务当前时段的额度已用完：等倒计时结束再试，或换个模型继续。"
            | _ -> ()

            let ticker = CountdownTicker()

            let refresh () =
                let frame = ticker.Refresh(RetryCountdown.remainingSeconds info (host.Now()))
                countdownLeft <- frame.Left
                countdownTicking <- frame.KeepTicking
                countdownAnnounce <- frame.Announce

                panel.Render(
                    this.ErrorPanelVS
                        error
                        hint
                        countdownLeft
                        countdownTicking
                        countdownAnnounce
                        onRetry
                        actions
                        onContinue
                )

            refresh ()
            panel.Tick.Add(fun () -> refresh ())
            panel.Host.AttachedToVisualTree.Add(fun _ -> refresh ())
        | _ -> ()

        if countdownLeft < 0 then
            panel.Render(
                this.ErrorPanelVS error hint countdownLeft countdownTicking countdownAnnounce onRetry actions onContinue
            )

        panel.Host

    /// 错误卡的视图状态（单入口 Render 的输入）：按钮族、倒计时与回调都在这里。
    member private _.ErrorPanelVS
        (error: GenerationError)
        (hint: string)
        (countdownLeft: int)
        (countdownTicking: bool)
        (countdownAnnounce: bool)
        (onRetry: unit -> unit)
        (actions: CardActions)
        (onContinue: (unit -> unit) option)
        : Wanxiang2.WorkspaceState.ErrorPanelVS =
        let canContinue = error.retryable && onContinue.IsSome

        let recovery =
            match ErrorRecovery.forKind error.kind, actions.Recover with
            | Some kind, Some sink -> Some(kind, sink)
            | _ -> None

        let row =
            [
                if canContinue then
                    Wanxiang2.WorkspaceState.ErrorPanelAction.Continue
                if error.retryable then
                    Wanxiang2.WorkspaceState.ErrorPanelAction.Retry
                match recovery with
                | Some(kind, _) -> Wanxiang2.WorkspaceState.ErrorPanelAction.Recovery(ErrorRecovery.label kind)
                | None -> ()
            ]

        {
            Title = GenerationErrorTitle.ofMessage error.message
            Hint = hint
            Detail = error.detail
            CountdownSeconds = countdownLeft
            Ticking = countdownTicking
            Announce = countdownAnnounce
            Actions = row
            OnRetry = onRetry
            OnContinue = onContinue
            OnRecovery =
                (match recovery with
                 | Some(kind, sink) -> Some(fun _ -> sink kind)
                 | None -> None)
        }

    /// 右键菜单（旧 buildMessageContextMenu）：与脚注行按钮逐项同源
    member _.Menu(message: MessageView, ctx: CardContext, actions: CardActions) : ContextMenu =
        let menu = ContextMenu()

        let facts =
            {
                generation =
                    (if ctx.Streaming then
                         MenuGeneration.Streaming
                     else
                         MenuGeneration.Settled)
                role = MessageMenu.roleOfText message.role
                position =
                    (if ctx.IsLastAssistant then
                         ReplyPosition.LastAssistant
                     else
                         ReplyPosition.Other)
                text = message.text
                commitId = message.commitId
                linkSink =
                    (if actions.CopyLink.IsSome then
                         LinkSink.Available
                     else
                         LinkSink.Absent)
            }

        let items =
            MessageMenu.entries facts
            |> List.map (fun item ->
                let action, glyph =
                    match item with
                    | MessageMenuItem.Copy -> (fun () -> actions.CopyText message.text), Glyph.Copy
                    | MessageMenuItem.EditAndFork -> (fun () -> actions.EditAndFork message), Glyph.Pencil
                    | MessageMenuItem.Regenerate -> (fun () -> actions.Regenerate()), Glyph.Refresh
                    | MessageMenuItem.CopyLink id ->
                        (fun () -> actions.CopyLink |> Option.iter (fun f -> f id)), Glyph.Link
                    | MessageMenuItem.Delete id -> (fun () -> actions.DeleteMessage id), Glyph.Trash

                let danger = MessageMenu.isDanger item
                let entry = MenuItem(Header = MessageMenu.label item)

                entry.Icon <-
                    looks.Icon
                        glyph
                        (if danger then
                             brush PaletteKey.Danger
                         else
                             brush PaletteKey.TextMuted)

                if danger then
                    entry.Foreground <- brush PaletteKey.Danger

                let hint = MessageMenu.hint facts item

                if not (String.IsNullOrWhiteSpace hint) then
                    try
                        entry.InputGesture <- KeyGesture.Parse hint
                    with _ ->
                        ()

                entry.Click.Add(fun _ -> action ())
                entry :> obj)

        menu.ItemsSource <- ResizeArray<obj>(items)
        menu

    /// 脚注操作条（旧 actionButtons）
    member this.Actions(message: MessageView, ctx: CardContext, actions: CardActions, index: int option) : ActionStrip =
        let isUser = MessageView.isUser message

        let rest =
            if ctx.IsLastAssistant && not isUser then
                StripRest.Subtle
            else
                StripRest.Hidden

        let strip = ActionStrip(rest)
        // 复制与编辑 / 重新生成 / 删除同门：流式进行中一律不挂
        if not (String.IsNullOrWhiteSpace message.text) && not ctx.Streaming then
            let name = ActionStrip.copyName isUser index
            let mutable confirm: CopyButton option = None
            let mutable hostButton: Control = null

            let button =
                strip.Add(
                    StripGlyph.Copy,
                    "复制",
                    name,
                    "复制消息正文",
                    fun () ->
                        actions.CopyText message.text
                        confirm |> Option.iter (fun c -> c.Copied(CardUi.reduced hostButton))
                )

            hostButton <- button
            AutomationProperties.SetLiveSetting(button, AutomationLiveSetting.Polite)
            let c = CopyButton(button, looks.Copy)

            c.StateChanged.Add(fun state ->
                match state with
                | CopyState.Default ->
                    strip.ApplyIcon(button, StripGlyph.Copy, brush PaletteKey.TextMuted)
                    strip.ApplyCaption(button, "复制")
                    ToolTip.SetTip(button, "复制")
                    AutomationProperties.SetName(button, name)
                    AutomationProperties.SetHelpText(button, "复制消息正文")
                | CopyState.Confirmed
                | CopyState.Fading ->
                    strip.ApplyIcon(button, StripGlyph.Check, brush PaletteKey.Success)
                    strip.ApplyCaption(button, "已复制")
                    ToolTip.SetTip(button, "已复制")
                    AutomationProperties.SetName(button, "已复制")
                    AutomationProperties.SetHelpText(button, "已复制消息正文"))

            let fade = Avalonia.Animation.DoubleTransition(Property = Visual.OpacityProperty)

            if isNull button.Transitions then
                button.Transitions <- Avalonia.Animation.Transitions()

            button.Transitions.Add fade
            fade.Duration <- looks.Copy.Fade
            confirm <- Some c

        if isUser && not ctx.Streaming then
            strip.Add(StripGlyph.Pencil, "编辑并分叉", "编辑并分叉", "以编辑后的消息创建分叉对话", fun () -> actions.EditAndFork message)
            |> ignore

        if not isUser && ctx.IsLastAssistant && not ctx.Streaming then
            strip.Add(StripGlyph.Refresh, "重新生成", "重新生成", "丢弃最后一次回复并重新生成", fun () -> actions.Regenerate())
            |> ignore

        match message.commitId with
        | Some id when not ctx.Streaming ->
            strip.Add(StripGlyph.Trash, "删除这条消息", "删除这条消息", "从对话历史中删除这条消息", fun () -> actions.DeleteMessage id)
            |> ignore
        | _ -> ()

        strip

    /// 脚注时间与用量（旧 footer）
    member _.Footer(message: MessageView, usage: GenerationUsage option) : Control option =
        let stamp =
            message.committedAt
            |> Option.map (fun at ->
                let local = at.ToLocalTime()

                {
                    Short = MessageTimestamp.format (host.Now().ToLocalTime()) local
                    Full = local.ToString("yyyy-MM-dd HH:mm:ss")
                })

        let figures =
            usage
            |> Option.map (fun u ->
                {
                    Prompt = u.promptTokens
                    Completion = u.completionTokens
                    Total = u.totalTokens
                    DurationMs = u.durationMs
                    Cached = GenerationUsage.cachedCount u
                    Detail = GenerationUsage.formatDetail u
                })

        UsageFoot.line stamp figures (MessageView.isUser message)

    /// 渲染一条消息（旧 renderWithStream）。第二项是流式正文的原位更新入口；非流式或没有正文时为 None
    member this.Render
        (message: MessageView, ctx: CardContext, actions: CardActions, index: int option)
        : Control * MarkdownStream option =
        let isUser = MessageView.isUser message

        let view =
            MarkdownView(
                looks.Markdown,
                {
                    CopyText = actions.CopyText
                    OpenLink = actions.OpenLink
                    MathHtml = host.MathHtml
                    Tint = host.Tint
                    Wrap = host.Wrap
                },
                ctx.FontSize,
                not ctx.Streaming
            )

        let body = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        let mutable stream = None

        if not (String.IsNullOrWhiteSpace message.reasoning) then
            body.Children.Add(
                this.Thinking(ctx, actions, message.reasoning, ctx.Usage |> Option.bind (fun u -> u.durationMs))
            )

        if not (List.isEmpty message.toolCalls) then
            body.Children.Add(this.ToolActivity(ctx, actions, message.toolCalls))

        if not (String.IsNullOrWhiteSpace message.text) then
            if isUser then
                // 长输入先收进小阅读窗：粘贴的大段提示词不把同屏回复推走
                let pane =
                    RevealPane(
                        UserBubble.text message.text ctx.FontSize,
                        LayoutPolicy.expandedLongMessageMaxHeight,
                        true
                    )

                body.Children.Add pane.Host
            elif ctx.Streaming then
                let live = MarkdownStream view
                live.Update message.text
                stream <- Some live
                body.Children.Add live.Panel
            else
                body.Children.Add(view.RenderText message.text)

        for a in message.attachments do
            body.Children.Add(this.Attachment(ctx, actions, a))

        if ctx.Streaming then
            body.Children.Add(StreamCaret().Host)

        let row: Control =
            if isUser then
                UserBubble(body).Host
            else
                // 助手是正文，不再在左边挂头像
                Border(
                    Background = Brushes.Transparent,
                    Padding = Thickness 0.0,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = body
                )

        let strip = this.Actions(message, ctx, actions, index)

        let meta =
            if ctx.Streaming then
                None
            else
                this.Footer(message, (if ctx.IsLastAssistant then ctx.Usage else None))

        let line =
            StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)

        line.Bind(StackPanel.SpacingProperty, CardUi.res "Space.Md") |> ignore
        line.Margin <- Thickness(0.0, 8.0, 0.0, 0.0)

        if isUser then
            line.HorizontalAlignment <- HorizontalAlignment.Right
            meta |> Option.iter line.Children.Add
            line.Children.Add strip.Host
        else
            line.HorizontalAlignment <- HorizontalAlignment.Left
            line.Children.Add strip.Host
            meta |> Option.iter line.Children.Add

        let card =
            StackPanel(
                Orientation = Orientation.Vertical,
                Spacing = 0.0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            )

        card.Margin <- Thickness(0.0, 0.0, 0.0, 24.0)
        card.Children.Add row
        card.Children.Add line
        let mutable touched = false

        let sync () =
            strip.ApplyShown(card.IsPointerOver || strip.Host.IsKeyboardFocusWithin || touched)
        // 触屏没有 hover：点一下消息就选中它、露出操作；点另一条时上一条收起
        card.AddHandler(
            InputElement.PointerPressedEvent,
            (fun (_: obj) (e: PointerPressedEventArgs) ->
                if e.Pointer.Type = PointerType.Touch || e.Pointer.Type = PointerType.Pen then
                    host.Touch.Release |> Option.iter (fun release -> release ())
                    touched <- true
                    strip.ApplyShown true

                    host.Touch.Release <-
                        Some(fun () ->
                            touched <- false
                            host.Touch.Release <- None
                            sync ())),
            Avalonia.Interactivity.RoutingStrategies.Tunnel,
            true
        )

        let show () = strip.ApplyShown true
        let dim () = sync ()
        card.PointerEntered.Add(fun _ -> show ())
        card.PointerExited.Add(fun _ -> dim ())

        card.PointerPressed.Add(fun e ->
            if e.GetCurrentPoint(card).Properties.IsRightButtonPressed then
                card.Focus(NavigationMethod.Pointer) |> ignore)

        card.GotFocus.Add(fun _ -> sync ())
        card.LostFocus.Add(fun _ -> dim ())
        strip.Host.GotFocus.Add(fun _ -> show ())
        strip.Host.LostFocus.Add(fun _ -> dim ())
        strip.Host.PointerEntered.Add(fun _ -> show ())
        strip.Host.PointerExited.Add(fun _ -> dim ())
        // 定位高亮的承载面：零边距圆角 Border，平时透明，不改列表占位
        let highlight =
            Border(
                Name = MessageCards.HighlightName,
                Background = Brushes.Transparent,
                Child = card,
                ContextMenu = this.Menu(message, ctx, actions)
            )

        highlight.Bind(Border.CornerRadiusProperty, CardUi.res "CornerRadius.RadiusLg")
        |> ignore

        (highlight :> Control), stream

    /// 单入口：从 MessageRowVS 渲染
    member this.Render
        (row: Wanxiang2.WorkspaceState.MessageRowVS, ctx: CardContext, actions: CardActions)
        : Control * MarkdownStream option =
        let msg: MessageView =
            {
                role = row.Role
                text = row.Text
                reasoning = row.Reasoning
                toolCalls = row.ToolCalls
                toolResults = []
                attachments = row.Attachments
                commitId = row.CommitId
                committedAt = row.CommittedAt
            }

        let effCtx =
            { ctx with
                Streaming = row.Streaming
                IsLastAssistant = row.IsLastAssistant
                AutoCollapseReasoning = row.AutoCollapseReasoning
                Usage =
                    (match row.Usage with
                     | Some u -> Some u
                     | None -> ctx.Usage)
                MissingAttachments =
                    (if not (Set.isEmpty row.MissingAttachments) then
                         row.MissingAttachments
                     else
                         ctx.MissingAttachments)
                AwaitingApproval =
                    (if not (Set.isEmpty row.AwaitingApproval) then
                         row.AwaitingApproval
                     else
                         ctx.AwaitingApproval)
            }

        this.Render(
            msg,
            effCtx,
            actions,
            (match row.Index with
             | Some i -> Some i
             | None -> None)
        )

    /// 消息卡外层高亮承载面的名字（列表定位高亮时按名查找）
    static member HighlightName = "reveal-highlight"
