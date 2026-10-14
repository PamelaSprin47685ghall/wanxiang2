namespace Wanxiang2.ComposerBox

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform.Storage
open Avalonia.Threading
open Avalonia.VisualTree

/// 用到的笔刷
[<RequireQualifiedAccess>]
type BoxInk =
    | Text
    | TextMuted
    | Accent
    | AccentSoft
    | AccentFaint
    | TextOnAccent
    | Surface
    | Hairline
    | SurfaceContainer
    | HairlineStrong
    | Warning

/// 用到的活度量
[<RequireQualifiedAccess>]
type BoxGauge =
    | FontReading
    | FontCaption
    | FontSmall
    | InputMinHeight
    | InputMaxHeight
    | ShellPadX
    | ShellPadY
    | RadiusMd
    | RadiusLg
    | BorderWidth
    | SpaceXs
    | SpaceMd
    | SpaceXl
    | Space2xl
    | IconButton
    | CompactTarget
    | NestedGutter
    | OpacityDisabled
    | OpacityComposerDisabled
    | CaptionLineHeight
    | ReadingWidth
    | StrokeThick

/// 用到的图标
[<RequireQualifiedAccess>]
type BoxGlyph =
    | Send
    | Stop
    | Paperclip

/// 「发送 / 停止 / 排队」一拍的呈现（宿主用 ComposerSendState.decide 折出）
type SendView =
    { VisionBlocked: bool
      VisionMessage: string
      SendEnabled: bool
      QueueEnabled: bool
      QueueVisible: bool
      SendTip: string
      SendLabel: string
      QueueTip: string option }

/// 外观
type ComposerBoxLook =
    { Ink: BoxInk -> IBrush
      Gauge: BoxGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      Glyph: BoxGlyph -> IBrush -> Control
      DressSend: Border -> unit
      DressQueue: Border -> unit
      DressAttach: Border -> unit
      DressGhost: Border -> unit }

/// 输入壳（旧 Composer 的 textbox、shell、发送/停止/排队/附件键、拖入、禁用说明、看图提示）。
/// 附件托盘与模型芯片由宿主塞进插槽；发送门控由宿主经 ApplySend 写入。发送/附件键是本库 Spot。
type ComposerBox(look: ComposerBoxLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let input =
        TextBox(
            PlaceholderText = "输入消息…",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = Thickness 0.0,
            Background = Brushes.Transparent,
            Foreground = ink BoxInk.Text,
            CaretBrush = ink BoxInk.Accent,
            PlaceholderForeground = ink BoxInk.TextMuted,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center)
    let shell =
        Border(Background = ink BoxInk.Surface, BorderBrush = ink BoxInk.Hairline)
    let send =
        Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center)
    let queue =
        Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center, IsVisible = false)
    let attach =
        Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center)
    let context =
        Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center, IsVisible = false)
    let contextLabel = TextBlock(Text = "上下文", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
    let traySlot = ContentControl()
    let modelSlot = ContentControl()
    let accessory = StackPanel(Orientation = Orientation.Vertical)
    let disabledText =
        TextBlock(Foreground = ink BoxInk.TextMuted, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap)
    let disabledNotice =
        Border(
            Background = ink BoxInk.SurfaceContainer,
            BorderBrush = ink BoxInk.HairlineStrong,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = disabledText,
            Opacity = 1.0,
            IsVisible = false)
    let visionText =
        TextBlock(Foreground = ink BoxInk.Text, TextWrapping = TextWrapping.Wrap)
    let visionNotice =
        Border(
            Background = ink BoxInk.SurfaceContainer,
            BorderBrush = ink BoxInk.Warning,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = visionText,
            IsVisible = false)
    let dropHint =
        Border(Background = ink BoxInk.AccentFaint, BorderThickness = Thickness 0.0, IsHitTestVisible = false, IsVisible = false)
    let dropText =
        TextBlock(
            Text = "释放文件以添加到当前对话附件",
            FontWeight = FontWeight.Medium,
            Foreground = ink BoxInk.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center)
    let host = Border(Background = Brushes.Transparent)
    let submitted = Event<string>()
    let stopped = Event<unit>()
    let attachAsked = Event<unit>()
    let dropped = Event<IStorageItem list>()
    let mutable paste: unit -> bool = fun () -> false
    let contextOpened = Event<unit>()
    let mutable generating = false
    let mutable enabled = false
    let mutable disabledReason = ""
    let mutable enterSends = true
    let mutable compact = false
    let mutable dragging = false
    let mutable wasActive = false
    let mutable canSubmit = false
    /// 输入法组合中（候选选择）时 Enter / 上下键归输入法；探针由宿主给（如 ImeShield）
    let mutable composing: TextBox -> bool = fun _ -> false
    /// 宿主先看一眼按键（历史召回、Escape 链）；返回 true 表示已处理
    let mutable keyHook: KeyEventArgs -> bool = fun _ -> false

    let tryFocus () =
        let act () =
            try
                if VisualExtensions.IsAttachedToVisualTree input && input.IsEffectivelyVisible && input.IsEnabled then
                    input.Focus() |> ignore
            with _ -> ()
        if Dispatcher.UIThread.CheckAccess() then act () else Dispatcher.UIThread.Post act

    let setIcon (b: Border) (g: BoxGlyph) (brush: IBrush) =
        let glyph = look.Glyph g brush
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        glyph.VerticalAlignment <- VerticalAlignment.Center
        b.Child <- glyph

    let setSquare (b: Border) (size: float) =
        b.Width <- size
        b.Height <- size
        b.MinWidth <- size
        b.MinHeight <- size

    let setEnabledBtn (b: Border) on =
        b.IsEnabled <- on
        b.Opacity <- if on then 1.0 else Follow.now (gauge BoxGauge.OpacityDisabled)
        b.IsHitTestVisible <- on
        b.Focusable <- on

    let updateShell () =
        if dragging && enabled then
            shell.Background <- ink BoxInk.Surface
            shell.BorderBrush <- ink BoxInk.Accent
            let thick = Follow.now (gauge BoxGauge.StrokeThick)
            let c = (ink BoxInk.Accent :?> ISolidColorBrush).Color
            shell.BoxShadow <- BoxShadows(BoxShadow(Spread = thick, Color = c))
            dropHint.IsVisible <- true
        elif input.IsFocused then
            shell.Background <- ink BoxInk.Surface
            shell.BorderBrush <- ink BoxInk.Accent
            let thick = Follow.now (gauge BoxGauge.StrokeThick)
            let c = (ink BoxInk.AccentSoft :?> ISolidColorBrush).Color
            shell.BoxShadow <- BoxShadows(BoxShadow(Spread = thick, Color = c))
            dropHint.IsVisible <- false
        else
            shell.Background <- ink BoxInk.Surface
            shell.BorderBrush <- ink BoxInk.Hairline
            shell.BoxShadow <- BoxShadows()
            dropHint.IsVisible <- false
        if not enabled then
            shell.BorderBrush <- ink BoxInk.Hairline
            shell.BoxShadow <- BoxShadows()
        input.PlaceholderText <-
            if not enabled && not (String.IsNullOrWhiteSpace disabledReason) then ""
            elif dragging && enabled then "释放文件以添加到当前对话附件"
            else "输入消息…"

    let applySizes () =
        let size = Follow.now (gauge (if compact then BoxGauge.CompactTarget else BoxGauge.IconButton))
        setSquare attach size
        setSquare send size
        setSquare queue size

    do
        // TextBox 透明底资源，避免 Fluent 聚焦白底
        let transparent = Brushes.Transparent :> IBrush
        for key in
            [ "TextControlBackground"; "TextControlBackgroundPointerOver"; "TextControlBackgroundFocused"
              "TextControlBackgroundDisabled"; "TextControlBorderBrush"; "TextControlBorderBrushPointerOver"
              "TextControlBorderBrushFocused"; "TextControlBorderBrushDisabled" ] do
            input.Resources[key] <- transparent
        for key in [ "TextControlForeground"; "TextControlForegroundPointerOver"; "TextControlForegroundFocused" ] do
            input.Resources[key] <- ink BoxInk.Text
        for key in
            [ "TextControlPlaceholderForeground"; "TextControlPlaceholderForegroundPointerOver"
              "TextControlPlaceholderForegroundFocused" ] do
            input.Resources[key] <- ink BoxInk.TextMuted
        Follow.live input (gauge BoxGauge.FontReading) (fun s -> input.FontSize <- s)
        Follow.live input look.BodyFont (fun f -> input.FontFamily <- f)
        Follow.live2 input (gauge BoxGauge.SpaceXs) (gauge BoxGauge.NestedGutter) (fun y g ->
            input.Padding <- Thickness(0.0, y, g, y))
        Follow.live input (gauge BoxGauge.InputMinHeight) (fun h -> input.MinHeight <- h)
        Follow.live input (gauge BoxGauge.InputMaxHeight) (fun h -> input.MaxHeight <- h)
        ScrollViewer.SetHorizontalScrollBarVisibility(input, ScrollBarVisibility.Disabled)
        ScrollViewer.SetVerticalScrollBarVisibility(input, ScrollBarVisibility.Auto)
        look.DressSend send
        look.DressQueue queue
        look.DressAttach attach
        look.DressGhost context
        setIcon send BoxGlyph.Send (ink BoxInk.TextOnAccent)
        setIcon queue BoxGlyph.Send (ink BoxInk.TextMuted)
        setIcon attach BoxGlyph.Paperclip (ink BoxInk.TextMuted)
        context.Child <- contextLabel
        Follow.live contextLabel (gauge BoxGauge.FontSmall) (fun s -> contextLabel.FontSize <- s)
        Follow.live contextLabel look.BodyFont (fun f -> contextLabel.FontFamily <- f)
        AutomationProperties.SetName(send, "发送")
        AutomationProperties.SetName(queue, "排队发送")
        AutomationProperties.SetName(attach, "添加附件")
        AutomationProperties.SetName(context, "对话上下文")
        ToolTip.SetTip(attach, "添加附件，也可以将文件拖入输入区")
        AutomationProperties.SetHelpText(attach, "添加附件，也可以将文件拖入输入区")
        send.OnInvoke(fun () ->
            if generating then
                stopped.Trigger()
                tryFocus ()
            elif canSubmit then submitted.Trigger(if isNull input.Text then "" else input.Text))
        queue.OnInvoke(fun () -> if canSubmit then submitted.Trigger(if isNull input.Text then "" else input.Text))
        attach.OnInvoke(fun () -> attachAsked.Trigger())
        context.OnInvoke(fun () -> contextOpened.Trigger())
        Follow.live2 accessory (gauge BoxGauge.SpaceMd) (gauge BoxGauge.SpaceMd) (fun s _ -> accessory.Spacing <- s)
        Follow.live disabledText (gauge BoxGauge.FontCaption) (fun s -> disabledText.FontSize <- s)
        Follow.live disabledText (gauge BoxGauge.CaptionLineHeight) (fun lh -> disabledText.LineHeight <- lh)
        Follow.live disabledText look.BodyFont (fun f -> disabledText.FontFamily <- f)
        Follow.live2 disabledNotice (gauge BoxGauge.SpaceXl) (gauge BoxGauge.SpaceMd) (fun x y -> disabledNotice.Padding <- Thickness(x, y))
        Follow.live disabledNotice (gauge BoxGauge.RadiusMd) (fun r -> disabledNotice.CornerRadius <- CornerRadius r)
        Follow.live disabledNotice (gauge BoxGauge.BorderWidth) (fun w -> disabledNotice.BorderThickness <- Thickness w)
        Follow.live visionText (gauge BoxGauge.FontCaption) (fun s -> visionText.FontSize <- s)
        Follow.live visionText (gauge BoxGauge.CaptionLineHeight) (fun lh -> visionText.LineHeight <- lh)
        Follow.live visionText look.BodyFont (fun f -> visionText.FontFamily <- f)
        Follow.live2 visionNotice (gauge BoxGauge.SpaceXl) (gauge BoxGauge.SpaceMd) (fun x y -> visionNotice.Padding <- Thickness(x, y))
        Follow.live visionNotice (gauge BoxGauge.RadiusMd) (fun r -> visionNotice.CornerRadius <- CornerRadius r)
        Follow.live visionNotice (gauge BoxGauge.BorderWidth) (fun w -> visionNotice.BorderThickness <- Thickness w)
        Follow.live dropText (gauge BoxGauge.FontSmall) (fun s -> dropText.FontSize <- s)
        Follow.live dropText look.BodyFont (fun f -> dropText.FontFamily <- f)
        Follow.live dropHint (gauge BoxGauge.RadiusLg) (fun r -> dropHint.CornerRadius <- CornerRadius r)
        let dropInner =
            Border(Background = ink BoxInk.AccentSoft, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = dropText)
        Follow.live dropInner (gauge BoxGauge.RadiusMd) (fun r -> dropInner.CornerRadius <- CornerRadius r)
        Follow.live2 dropInner (gauge BoxGauge.SpaceXl) (gauge BoxGauge.SpaceMd) (fun x y -> dropInner.Padding <- Thickness(x, y))
        dropHint.Child <- dropInner
        Follow.live shell (gauge BoxGauge.RadiusLg) (fun r -> shell.CornerRadius <- CornerRadius r)
        Follow.live shell (gauge BoxGauge.BorderWidth) (fun w -> shell.BorderThickness <- Thickness w)
        Follow.live2 shell (gauge BoxGauge.ShellPadX) (gauge BoxGauge.ShellPadY) (fun x y -> shell.Padding <- Thickness(x, y, x, y))
        let actionRow = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right)
        Follow.live actionRow (gauge BoxGauge.SpaceMd) (fun s -> actionRow.Spacing <- s)
        actionRow.Children.Add attach
        actionRow.Children.Add queue
        actionRow.Children.Add send
        let request = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live request (gauge BoxGauge.SpaceMd) (fun s -> request.Spacing <- s)
        request.Children.Add modelSlot
        request.Children.Add context
        let toolbar = Grid()
        Follow.live toolbar (gauge BoxGauge.SpaceMd) (fun s -> toolbar.ColumnSpacing <- s)
        Follow.live toolbar (gauge BoxGauge.SpaceXs) (fun s -> toolbar.RowSpacing <- s)
        toolbar.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        toolbar.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        toolbar.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
        toolbar.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
        toolbar.Children.Add request
        toolbar.Children.Add actionRow
        Grid.SetColumn(request, 0)
        Grid.SetRow(request, 0)
        Grid.SetColumn(actionRow, 1)
        Grid.SetRow(actionRow, 0)
        let column = StackPanel(Orientation = Orientation.Vertical)
        Follow.live column (gauge BoxGauge.SpaceMd) (fun s -> column.Spacing <- s)
        column.Children.Add traySlot
        column.Children.Add input
        column.Children.Add toolbar
        let shellGrid = Grid()
        shellGrid.Children.Add column
        shellGrid.Children.Add dropHint
        shell.Child <- shellGrid
        let outer = StackPanel(Orientation = Orientation.Vertical)
        Follow.live outer (gauge BoxGauge.SpaceMd) (fun s -> outer.Spacing <- s)
        Follow.live outer (gauge BoxGauge.ReadingWidth) (fun w -> outer.MaxWidth <- w)
        outer.Children.Add accessory
        outer.Children.Add disabledNotice
        outer.Children.Add visionNotice
        outer.Children.Add shell
        host.Child <- outer
        applySizes ()
        Follow.live host (gauge BoxGauge.IconButton) (fun _ -> applySizes ())
        Follow.live host (gauge BoxGauge.CompactTarget) (fun _ -> applySizes ())
        input.GotFocus.Add(fun _ ->
            wasActive <- true
            updateShell ())
        input.LostFocus.Add(fun _ ->
            if enabled then wasActive <- false
            updateShell ())
        input.AddHandler(
            InputElement.KeyDownEvent,
            EventHandler<KeyEventArgs>(fun _ e ->
                if composing input && (e.Key = Key.Enter || e.Key = Key.Up || e.Key = Key.Down) then ()
                elif e.Key = Key.Escape && generating then
                    e.Handled <- true
                    stopped.Trigger()
                elif e.Key = Key.Enter then
                    let shift = e.KeyModifiers.HasFlag KeyModifiers.Shift
                    let ctrl = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
                    let sends = if enterSends then not shift else ctrl
                    if sends then
                        e.Handled <- true
                        if canSubmit then submitted.Trigger(if isNull input.Text then "" else input.Text)
                elif e.Key = Key.V then
                    let ctrl = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
                    if ctrl && paste () then e.Handled <- true
                elif keyHook e then e.Handled <- true),
            RoutingStrategies.Tunnel)
        DragDrop.SetAllowDrop(shell, true)
        DragDrop.SetAllowDrop(host, true)
        let onOver (e: DragEventArgs) =
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
                    updateShell ()
        let onLeave (_: DragEventArgs) =
            if dragging then
                dragging <- false
                updateShell ()
        let onDrop (e: DragEventArgs) =
            if dragging then
                dragging <- false
                updateShell ()
            if e.DataTransfer <> null then
                let files =
                    let multi = e.DataTransfer.TryGetFiles()
                    if multi <> null then List.ofSeq multi
                    else
                        let single = e.DataTransfer.TryGetFile()
                        if single <> null then [ single ] else []
                if not (List.isEmpty files) then
                    e.Handled <- true
                    dropped.Trigger files
                    tryFocus ()
        for target in [ shell :> Interactive; host :> Interactive ] do
            target.AddHandler(DragDrop.DragOverEvent, EventHandler<DragEventArgs>(fun _ e -> onOver e))
            target.AddHandler(DragDrop.DragLeaveEvent, EventHandler<DragEventArgs>(fun _ e -> onLeave e))
            target.AddHandler(DragDrop.DropEvent, EventHandler<DragEventArgs>(fun _ e -> onDrop e))
        updateShell ()
        setEnabledBtn send false
        setEnabledBtn queue false
        setEnabledBtn attach false

    member _.Host: Control = host
    member _.Input = input
    member _.SendButton: Border = send
    member _.QueueButton: Border = queue
    member _.AttachButton: Border = attach
    member _.Text = if isNull input.Text then "" else input.Text
    member _.IsGenerating = generating
    member _.Submitted = submitted.Publish
    member _.Stopped = stopped.Publish
    member _.AttachAsked = attachAsked.Publish
    member _.Dropped = dropped.Publish
    /// Ctrl/⌘+V：宿主先试剪贴板里的图片或文件，接管了就返回 true（旧 pasteFromClipboard）
    member _.SetPaste(f: unit -> bool) = paste <- f
    member _.ContextOpened = contextOpened.Publish

    member _.SetTray(c: Control) = traySlot.Content <- c
    member _.SetModel(c: Control) = modelSlot.Content <- c
    member _.SetAccessory(c: Control) =
        accessory.Children.Clear()
        accessory.Children.Add c

    member _.SetEnterSends(value: bool) = enterSends <- value
    member _.SetImeProbe(f: TextBox -> bool) = composing <- f
    /// Escape（非生成中）、上下键等交给宿主（旧 PromptHistory 召回）
    member _.SetKeyHook(f: KeyEventArgs -> bool) = keyHook <- f
    member _.SetCompact(value: bool) =
        compact <- value
        applySizes ()

    member _.SetText(text: string) =
        input.Text <- text
        input.CaretIndex <- if isNull text then 0 else text.Length
        tryFocus ()

    member _.Focus() = tryFocus ()

    member _.ClearAfterSubmit(submittedText: string) =
        if input.Text = submittedText then
            input.Text <- ""
            input.CaretIndex <- 0
        tryFocus ()

    /// 宿主写入发送门控结果
    member _.ApplySend(view: SendView) =
        canSubmit <- view.SendEnabled && not generating || (view.QueueEnabled && generating)
        visionText.Text <- if view.VisionBlocked then view.VisionMessage else ""
        visionNotice.IsVisible <- view.VisionBlocked
        AutomationProperties.SetName(visionNotice, visionText.Text)
        setEnabledBtn send view.SendEnabled
        setEnabledBtn queue view.QueueEnabled
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
        let hint =
            if generating then
                if enterSends then "正在回复 · Enter 排队 / Esc 停止" else "正在回复 · Ctrl / ⌘ + Enter 排队 / Esc 停止"
            elif enterSends then "Enter 发送 / Shift + Enter 换行"
            else "Ctrl / ⌘ + Enter 发送 / Enter 换行"
        AutomationProperties.SetHelpText(input, hint)
        ToolTip.SetTip(input, hint)

    member this.SetGenerating(value: bool) =
        let home =
            not value
            && (match TopLevel.GetTopLevel host with
                | null -> false
                | top ->
                    let focused = top.FocusManager.GetFocusedElement()
                    not (isNull focused)
                    && (Object.ReferenceEquals(focused, send) || Object.ReferenceEquals(focused, queue)))
        generating <- value
        if value then setIcon send BoxGlyph.Stop (ink BoxInk.TextOnAccent)
        else setIcon send BoxGlyph.Send (ink BoxInk.TextOnAccent)
        if home then tryFocus ()

    member _.SetEnabled(value: bool, reason: string) =
        let was = enabled
        if not value && (input.IsFocused || wasActive) then wasActive <- true
        enabled <- value
        disabledReason <- reason
        input.IsEnabled <- true
        setEnabledBtn attach value
        shell.Opacity <- if value then 1.0 else Follow.now (gauge BoxGauge.OpacityComposerDisabled)
        disabledText.Text <- reason
        disabledNotice.IsVisible <- not value && not (String.IsNullOrWhiteSpace reason)
        updateShell ()
        if not was && value && wasActive then
            wasActive <- false
            tryFocus ()

    member _.ShowContext(label: string, detail: string, canEdit: bool) =
        contextLabel.Text <- label
        context.IsVisible <- true
        setEnabledBtn context canEdit
        ToolTip.SetTip(context, detail)
        AutomationProperties.SetHelpText(context, detail)

    member _.HideContext() = context.IsVisible <- false
