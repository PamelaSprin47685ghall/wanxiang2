namespace Wanxiang2.ComposerBox

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Avalonia.Platform.Storage
open Wanxiang2.WorkspaceState

/// 「发送 / 停止 / 排队」呈现视图：由门控决策（ComposerSendState.ComposerSendView 的派生成员）
/// 在呈现边界落成的一次性记录，无判定逻辑。
type SendView =
    {
        VisionBlocked: bool
        VisionMessage: string
        SendEnabled: bool
        QueueEnabled: bool
        QueueVisible: bool
        SendTip: string
        SendLabel: string
        QueueTip: string option
    }

/// 输入区根 Border
type ComposerRoot() =
    inherit Border()

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 输入壳（原生 TextBox / Button 实现）：
/// 单入口幂等 Render(ComposerVS)；
/// 废除 Look 记录、Spot/Tap、已取消逐属性活值订阅与散落的 Dispatcher.UIThread.Post。
type ComposerBox() =
    let input =
        TextBox(
            PlaceholderText = "写下一句",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 30.0,
            MaxHeight = 240.0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        )

    do input.Classes.Add "text-area"
    do input.BorderThickness <- Thickness 0.0
    do input.Background <- Brushes.Transparent
    do input.Padding <- Thickness(0.0, 8.0, 0.0, 8.0)
    do AutomationProperties.SetName(input, "输入消息")

    let shell =
        Border(CornerRadius = CornerRadius 16.0, BorderThickness = Thickness 1.0, Padding = Thickness(12.0, 8.0))

    do
        shell.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceRaised") |> ignore

        shell.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Border") |> ignore

    let send =
        let b =
            Button(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "primary"
        b.Width <- 32.0
        b.Height <- 32.0
        b.Padding <- Thickness 0.0
        b

    let queue =
        let b =
            Button(
                Content = "排队发送",
                FontSize = 12.5,
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Height = 34.0,
                MinHeight = 34.0,
                Padding = Thickness(8.0, 0.0),
                CornerRadius = CornerRadius 8.0,
                BorderThickness = Thickness 0.0,
                IsVisible = false
            )

        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b.Bind(Button.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        b

    let attach =
        let b =
            Button(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.Width <- 32.0
        b.Height <- 32.0
        b.Padding <- Thickness 0.0
        b

    let context =
        let b =
            Button(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false
            )

        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b

    let contextLabel =
        TextBlock(
            Text = "上下文",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        )

    let mutable trayCtl: Control option = None
    let mutable modelCtl: Control option = None
    let mutable pendingCtl: Control option = None

    let outer =
        StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0, MaxWidth = 748.0)

    do outer.Bind(Layoutable.MaxWidthProperty, Dyn.res "Metric.ReadingWidth") |> ignore

    // 外层列 8.0 是输入壳内部纵向节奏，不是消息间距（Metric.MessageGap 24），不绑。

    let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 6.0)

    let request =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, VerticalAlignment = VerticalAlignment.Center)

    let accessory = StackPanel(Orientation = Orientation.Vertical)

    let disabledText =
        TextBlock(TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 12.0)

    do
        disabledText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

    let disabledNotice =
        Border(
            CornerRadius = CornerRadius 6.0,
            BorderThickness = Thickness 1.0,
            Padding = Thickness(12.0, 6.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = disabledText,
            IsVisible = false
        )

    do
        disabledNotice.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer")
        |> ignore

        disabledNotice.Bind(Border.BorderBrushProperty, Dyn.res "Palette.HairlineStrong")
        |> ignore

    let visionText = TextBlock(TextWrapping = TextWrapping.Wrap, FontSize = 12.0)
    do visionText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

    let visionNotice =
        Border(
            CornerRadius = CornerRadius 6.0,
            BorderThickness = Thickness 1.0,
            Padding = Thickness(12.0, 6.0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = visionText,
            IsVisible = false
        )

    do
        visionNotice.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer")
        |> ignore

        visionNotice.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Warning")
        |> ignore

    let dropText =
        TextBlock(
            Text = "释放文件以添加到当前对话附件",
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        )

    do dropText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Accent") |> ignore

    let dropHint =
        Border(
            CornerRadius = CornerRadius 8.0,
            BorderThickness = Thickness 0.0,
            IsHitTestVisible = false,
            IsVisible = false,
            Child = dropText
        )

    do
        dropHint.Bind(Border.BackgroundProperty, Dyn.res "Palette.AccentFaint")
        |> ignore

    let host = ComposerRoot(Background = Brushes.Transparent)

    let submitted = Event<string>()
    let stopped = Event<unit>()
    let attachAsked = Event<unit>()
    let dropped = Event<IStorageItem list>()
    let contextOpened = Event<unit>()

    let mutable paste: unit -> bool = fun () -> false
    let mutable generating = false
    let mutable enabled = true
    let mutable disabledReason = ""
    let mutable modelReady = false
    let mutable enterSends = true
    let mutable compact = false
    let mutable contextShown = false
    let mutable dragging = false
    let mutable composing: TextBox -> bool = fun _ -> false
    let mutable keyHook: KeyEventArgs -> bool = fun _ -> false

    let submitText () =
        submitted.Trigger(if isNull input.Text then "" else input.Text)

    let setSendIcon isStop =
        if isStop then
            let p =
                Path(
                    Data = Geometry.Parse "M 4 4 L 12 4 L 12 12 L 4 12 Z",
                    Fill = Brushes.White,
                    Width = 14.0,
                    Height = 14.0
                )

            send.Content <- p
            AutomationProperties.SetName(send, "停止生成")
        else
            let p =
                Path(
                    Data = Geometry.Parse "M 3 8 L 13 8 M 9 4 L 13 8 L 9 12",
                    StrokeThickness = 1.6,
                    Stroke = Brushes.White,
                    Fill = Brushes.Transparent,
                    Width = 14.0,
                    Height = 14.0
                )

            send.Content <- p
            AutomationProperties.SetName(send, "发送")

    /// 排队键是文字钮（react .text-btn「排队发送」）：常名与提示固定挂，内容不再换图标
    let setQueueCaption () =
        AutomationProperties.SetName(queue, "排队发送")
        ToolTip.SetTip(queue, "排队发送")

    let setAttachIcon () =
        let p =
            Path(
                Data = Geometry.Parse "M 8 3 L 4 7 A 3 3 0 0 0 8 11 L 13 6 A 2 2 0 0 0 10 3 L 5 8 A 1 1 0 0 0 6 9",
                StrokeThickness = 1.2,
                Fill = Brushes.Transparent,
                Width = 14.0,
                Height = 14.0
            )

        p.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        attach.Content <- p
        AutomationProperties.SetName(attach, "添加附件")
        ToolTip.SetTip(attach, "添加附件，也可以将文件拖入输入区")
        AutomationProperties.SetHelpText(attach, "添加附件，也可以将文件拖入输入区")

    let updateShell () =
        if dragging && enabled then
            shell.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Accent") |> ignore
            dropHint.IsVisible <- true
        elif input.IsFocused then
            shell.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Accent") |> ignore
            dropHint.IsVisible <- false
        else
            shell.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Border") |> ignore

            dropHint.IsVisible <- false

        input.PlaceholderText <-
            if dragging && enabled then "释放文件以添加到当前对话附件"
            elif modelReady then "写下一句"
            else "先连接一个模型，草稿会留在这里"

    let applySizes () =
        let size = if compact then 44.0 else 30.0
        attach.Width <- size
        attach.Height <- size
        send.Width <- size
        send.Height <- size
        // 排队键固定 34 高文字钮，不随紧凑档换尺寸（react .text-btn）
        context.IsVisible <- contextShown && not compact

    // 以下四个投影动作只在 Render 内部使用（测试改走 Render(vs)），不作为对外入口。
    let setEnterSends (value: bool) = enterSends <- value

    let setCompact (value: bool) =
        compact <- value
        applySizes ()

    let setGenerating (value: bool) =
        generating <- value
        setSendIcon value

    let setEnabled (value: bool) (reason: string) =
        enabled <- value
        disabledReason <- reason
        attach.IsEnabled <- value
        disabledText.Text <- reason
        disabledNotice.IsVisible <- not value && not (String.IsNullOrWhiteSpace reason)
        updateShell ()

    do
        setSendIcon false
        setQueueCaption ()
        setAttachIcon ()

        context.Content <- contextLabel
        AutomationProperties.SetName(context, "对话上下文")

        send.Click.Add(fun _ -> if generating then stopped.Trigger() else submitText ())

        queue.Click.Add(fun _ -> submitText ())
        attach.Click.Add(fun _ -> attachAsked.Trigger())
        context.Click.Add(fun _ -> contextOpened.Trigger())

        let actionRow =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 6.0,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right
            )

        actionRow.Children.Add queue
        actionRow.Children.Add send

        request.Children.Add attach
        request.Children.Add context

        let toolbar = Grid(ColumnSpacing = 8.0, RowSpacing = 4.0)
        toolbar.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        toolbar.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        toolbar.Children.Add request
        toolbar.Children.Add actionRow
        Grid.SetColumn(request, 0)
        Grid.SetColumn(actionRow, 1)

        column.Children.Add input
        column.Children.Add toolbar

        let shellGrid = Grid()
        shellGrid.Children.Add column
        shellGrid.Children.Add dropHint
        shell.Child <- shellGrid

        outer.Children.Add accessory
        outer.Children.Add disabledNotice
        outer.Children.Add visionNotice
        outer.Children.Add shell
        host.Child <- outer

        applySizes ()

        input.GotFocus.Add(fun _ -> updateShell ())
        input.LostFocus.Add(fun _ -> updateShell ())

        input.AddHandler(
            InputElement.KeyDownEvent,
            EventHandler<KeyEventArgs>(fun _ e ->
                if composing input && (e.Key = Key.Enter || e.Key = Key.Up || e.Key = Key.Down) then
                    ()
                elif keyHook e then
                    e.Handled <- true
                elif e.Key = Key.Escape && generating then
                    e.Handled <- true
                    stopped.Trigger()
                elif e.Key = Key.Enter then
                    let shift = e.KeyModifiers.HasFlag KeyModifiers.Shift

                    let ctrl =
                        e.KeyModifiers.HasFlag KeyModifiers.Control
                        || e.KeyModifiers.HasFlag KeyModifiers.Meta

                    let sends = not shift && (enterSends <> ctrl)

                    if sends then
                        e.Handled <- true
                        submitText ()
                elif e.Key = Key.V then
                    let ctrl =
                        e.KeyModifiers.HasFlag KeyModifiers.Control
                        || e.KeyModifiers.HasFlag KeyModifiers.Meta

                    if ctrl && paste () then
                        e.Handled <- true),
            RoutingStrategies.Tunnel
        )

        DragDrop.SetAllowDrop(host, true)

        host.AddHandler(
            DragDrop.DragOverEvent,
            EventHandler<DragEventArgs>(fun _ e ->
                let has =
                    e.DataTransfer <> null
                    && (e.DataTransfer.Contains DataFormat.File || e.DataTransfer.TryGetFiles() <> null)

                if not has then
                    e.DragEffects <- DragDropEffects.None

                    if dragging then
                        dragging <- false
                        updateShell ()
                else
                    e.DragEffects <- DragDropEffects.Copy

                    if not dragging then
                        dragging <- true
                        updateShell ()),
            RoutingStrategies.Bubble
        )

        host.AddHandler(
            DragDrop.DragLeaveEvent,
            EventHandler<DragEventArgs>(fun _ _ ->
                if dragging then
                    dragging <- false
                    updateShell ()),
            RoutingStrategies.Bubble
        )

        host.AddHandler(
            DragDrop.DropEvent,
            EventHandler<DragEventArgs>(fun _ e ->
                if dragging then
                    dragging <- false
                    updateShell ()

                if e.DataTransfer <> null then
                    let files =
                        let multi = e.DataTransfer.TryGetFiles()

                        if multi <> null then
                            List.ofSeq multi
                        else
                            let single = e.DataTransfer.TryGetFile()
                            if single <> null then [ single ] else []

                    if not (List.isEmpty files) then
                        e.Handled <- true
                        dropped.Trigger files),
            RoutingStrategies.Bubble
        )

    /// 布局宿主
    member _.Host: Control = host :> Control
    member _.Input = input
    member _.SendButton: Button = send
    member _.QueueButton: Button = queue
    member _.ContextButton: Button = context
    member _.AttachButton: Button = attach
    member _.Text = if isNull input.Text then "" else input.Text
    member _.IsGenerating = generating

    member _.Submitted = submitted.Publish
    member _.Stopped = stopped.Publish
    member _.AttachAsked = attachAsked.Publish
    member _.Dropped = dropped.Publish
    member _.ContextOpened = contextOpened.Publish

    /// 宿主注入的剪贴板回调（非每帧投影）
    member _.WirePaste(f: unit -> bool) = paste <- f

    /// 附件托盘控件插槽（宿主直注，非每帧投影）
    member _.SlotTray(c: Control) =
        trayCtl |> Option.iter (column.Children.Remove >> ignore)
        column.Children.Insert(0, c)
        trayCtl <- Some c

    /// 模型芯片控件插槽（宿主直注，非每帧投影）
    member _.SlotModel(c: Control) =
        modelCtl |> Option.iter (request.Children.Remove >> ignore)
        let at = if request.Children.Contains attach then 1 else 0
        request.Children.Insert(at, c)
        modelCtl <- Some c

    /// 未确认消息面板插槽（宿主直注，非每帧投影）
    member _.SlotPending(c: Control) =
        pendingCtl |> Option.iter (outer.Children.Remove >> ignore)
        outer.Children.Insert(outer.Children.IndexOf shell, c)
        pendingCtl <- Some c

    /// 附属视图插槽（审批卡等，宿主直注，非每帧投影）
    member _.SlotAccessory(c: Control) =
        accessory.Children.Clear()
        accessory.Children.Add c

    /// 宿主注入的输入法组合探针（回调注入，非每帧投影）
    member _.WireImeProbe(f: TextBox -> bool) = composing <- f
    /// 宿主注入的按键钩子（回调注入，非每帧投影）
    member _.WireKeyHook(f: KeyEventArgs -> bool) = keyHook <- f

    /// 草稿回填命令（宿主一次性命令，非每帧投影）
    member _.ApplyDraftText(text: string) =
        input.Text <- text
        input.CaretIndex <- if isNull text then 0 else text.Length

    member _.Focus() = input.Focus() |> ignore

    member _.ClearAfterSubmit(submittedText: string) =
        if input.Text = submittedText then
            input.Text <- ""
            input.CaretIndex <- 0

    member _.ApplySend(view: SendView) =
        visionText.Text <- if view.VisionBlocked then view.VisionMessage else ""
        visionNotice.IsVisible <- view.VisionBlocked
        AutomationProperties.SetName(visionNotice, visionText.Text)
        send.IsEnabled <- view.SendEnabled
        queue.IsEnabled <- view.QueueEnabled
        queue.IsVisible <- view.QueueVisible
        ToolTip.SetTip(send, view.SendTip)
        AutomationProperties.SetName(send, view.SendLabel)
        AutomationProperties.SetHelpText(send, view.SendTip)

        match view.QueueTip with
        | Some tip ->
            ToolTip.SetTip(queue, tip)
            AutomationProperties.SetName(queue, "排队发送")
            AutomationProperties.SetHelpText(queue, tip)
        | None -> ()

    member _.ShowContext(label: string, detail: string, canEdit: bool) =
        contextLabel.Text <- label
        contextShown <- true
        context.IsVisible <- not compact
        context.IsEnabled <- canEdit
        ToolTip.SetTip(context, detail)
        AutomationProperties.SetHelpText(context, detail)

    member _.HideContext() =
        contextShown <- false
        context.IsVisible <- false

    member _.Submit() = submitText ()

    /// 单入口幂等渲染：由 ComposerVS 投影全量刷新输入区
    member this.Render(vs: ComposerVS) =
        modelReady <- vs.ModelChip.IsSome
        setEnabled vs.Enabled (vs.DisabledReason |> Option.defaultValue "")

        if input.Text <> vs.Text then
            input.Text <- vs.Text

            match vs.Caret with
            | Some c -> input.CaretIndex <- c
            | None -> input.CaretIndex <- if isNull vs.Text then 0 else vs.Text.Length

        setEnterSends vs.EnterSends
        setCompact vs.Compact

        // 发送按钮态
        match vs.Send.State with
        | SendButtonState.GeneratingStop ->
            setGenerating true
            send.IsEnabled <- true
            ToolTip.SetTip(send, "停止生成（Esc）")
            AutomationProperties.SetName(send, vs.Send.Label)
        | SendButtonState.Sending ->
            setGenerating false
            send.IsEnabled <- false
            ToolTip.SetTip(send, vs.Send.Label)
            AutomationProperties.SetName(send, vs.Send.Label)
        | SendButtonState.Idle ->
            setGenerating false
            send.IsEnabled <- vs.Enabled
            ToolTip.SetTip(send, vs.Send.Label)
            AutomationProperties.SetName(send, vs.Send.Label)

        // 已选模型只留芯片。对话上下文是同一份模型的第二入口，选中后不再并列。
        match vs.Context with
        | Some ctx when vs.ModelChip.IsNone -> this.ShowContext(ctx.Model, ctx.ContextSummary, true)
        | _ -> this.HideContext()
