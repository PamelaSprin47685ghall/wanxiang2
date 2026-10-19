namespace Wanxiang2.ErrorPanel

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 使用方提供的件
type ErrorPanelParts =
    { Copy: string -> string -> (unit -> string) -> Control
      Reveal: Control -> bool -> Control * (bool -> unit)
      CountdownText: int -> string }

/// 生成失败卡（旧 MessageCard.errorCardWith）：标题、说明、可选倒计时、继续 / 重试 / 恢复与技术细节披露。
type ErrorPanel(parts: ErrorPanelParts, title: string, hint: string) =
    let tick = Event<unit>()
    let mutable attached = false
    let mutable wanted = false
    let mutable left = 0
    let mutable onLeft: int -> unit = ignore

    let icon =
        Path(
            Data = Geometry.Parse "M 7 2 L 12 12 L 2 12 Z M 7 5 L 7 8 M 7 10 L 7 10.01",
            StrokeThickness = 1.3,
            Fill = Brushes.Transparent,
            Width = 16.0,
            Height = 16.0,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Thickness(0.0, 0.0, 12.0, 0.0))
    let titleBlock =
        TextBlock(Text = title, FontWeight = FontWeight.Medium, FontSize = 14.0, TextWrapping = TextWrapping.Wrap)
    let hintBlock =
        TextBlock(
            Text = hint,
            FontSize = 12.0,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18.0)
    let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
    let top = DockPanel(LastChildFill = true)
    let countdown =
        TextBlock(TextWrapping = TextWrapping.Wrap, FontSize = 12.0, IsVisible = false)
    let actionRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, Margin = Thickness(0.0, 12.0, 0.0, 0.0))
    let detailContainer = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let layout = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let card =
        Border(
            BorderThickness = Thickness 1.0,
            CornerRadius = CornerRadius 8.0,
            Padding = Thickness(16.0, 12.0),
            Margin = Thickness(0.0, 8.0, 0.0, 0.0),
            MaxWidth = 640.0,
            Child = layout)

    let textButton (label: string) (secondary: bool) (name: string) (help: string option) (action: unit -> unit) =
        let b = Button(Content = label, Focusable = true, Cursor = new Cursor(StandardCursorType.Hand), Margin = Thickness 0.0, VerticalAlignment = VerticalAlignment.Center)
        b.Classes.Add(if secondary then "button-secondary" else "button-ghost")
        b.Classes.Add(if secondary then "secondary" else "ghost")
        AutomationProperties.SetName(b, name)
        help |> Option.iter (fun h -> AutomationProperties.SetHelpText(b, h))
        b.Click.Add(fun _ -> action ())
        b

    do
        icon.Bind(Path.StrokeProperty, Dyn.res "Palette.Danger") |> ignore
        titleBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        hintBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Warning") |> ignore
        card.Classes.Add "grouping-card"
        card.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer") |> ignore
        card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
        column.Children.Add titleBlock
        column.Children.Add hintBlock
        DockPanel.SetDock(icon, Dock.Left)
        top.Children.Add icon
        top.Children.Add column
        AutomationProperties.SetName(countdown, "重试倒计时")
        layout.Children.Add top
        card.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            if wanted then tick.Trigger())
        card.DetachedFromVisualTree.Add(fun _ ->
            attached <- false)

    member _.Host: Control = card
    member _.Title = titleBlock
    member _.Hint = hintBlock
    member _.Countdown = countdown
    member _.Actions = actionRow
    member _.Left = left
    member _.Tick = tick.Publish
    member _.IsTicking = wanted && attached

    member _.SetTicking(value: bool) =
        wanted <- value

    member _.SetHint(text: string) = hintBlock.Text <- text

    member _.SetSeconds(seconds: int, ?announce: bool) =
        left <- max 0 seconds
        let ready = left = 0
        countdown.Text <- parts.CountdownText left
        AutomationProperties.SetName(countdown, sprintf "重试倒计时：%s" countdown.Text)
        if defaultArg announce false then
            AutomationProperties.SetLiveSetting(countdown, AutomationLiveSetting.Polite)
        if ready then
            countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Success") |> ignore
        else
            countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Warning") |> ignore
        countdown.IsVisible <- ready
        if not (column.Children.Contains countdown) then column.Children.Add countdown
        onLeft left

    member _.AddContinue(action: unit -> unit) =
        let b = textButton "继续生成" true "继续生成" (Some "保留已写出的部分，从中断处接着生成") action
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b

    member this.AddRetry(hasContinue: bool, action: unit -> unit) =
        let b =
            textButton "重试" (not hasContinue) "重试生成"
                (Some(if hasContinue then "丢弃已写出的部分，重新生成" else "重新尝试生成"))
                action
        b.MinWidth <- 80.0
        let apply left =
            let waiting = left > 0
            b.IsEnabled <- not waiting
            b.Content <- (if waiting then sprintf "重试（%d 秒）" left else "重试")
            AutomationProperties.SetName(b, (if waiting then sprintf "重试生成，%d 秒后可用" left else "重试生成"))
        onLeft <- apply
        apply left
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b

    member _.AddRecovery(label: string, action: unit -> unit) =
        let b = textButton label true label None action
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b

    member _.AddDetail(detail: string) =
        let content =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = detail,
                FontSize = 11.0,
                LineHeight = 11.0 * 1.52,
                TextWrapping = TextWrapping.Wrap)
        content.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.CodeMuted") |> ignore
        content.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore
        content.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft") |> ignore
        let copy = parts.Copy "复制错误细节" "复制错误技术细节" (fun () -> detail)
        let header = DockPanel(LastChildFill = false)
        DockPanel.SetDock(copy, Dock.Right)
        header.Children.Add copy
        let box =
            Border(
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius 4.0,
                Padding = Thickness(12.0, 8.0),
                Child = content)
        box.Bind(Border.BackgroundProperty, Dyn.res "Palette.CodeBg") |> ignore
        box.Bind(Border.BorderBrushProperty, Dyn.res "Palette.CodeBorder") |> ignore
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        stack.Children.Add header
        stack.Children.Add box
        let host, setVisible = parts.Reveal stack false
        host.Margin <- Thickness(0.0, 8.0, 0.0, 0.0)

        let rotate = RotateTransform(Angle = 0.0)
        let rotation = DoubleTransition(Property = RotateTransform.AngleProperty, Duration = TimeSpan.FromMilliseconds 160.0, Easing = Avalonia.Animation.Easings.CubicEaseOut())
        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions
        let chevronHost = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 14.0, Height = 14.0)
        let chevron =
            Path(
                Data = Geometry.Parse "M 4 2 L 9 7 L 4 12",
                StrokeThickness = 1.3,
                Fill = Brushes.Transparent,
                Width = 10.0,
                Height = 10.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center)
        chevron.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron
        let caption = TextBlock(Text = "技术细节", FontWeight = FontWeight.Medium, FontSize = 12.0, VerticalAlignment = VerticalAlignment.Center)
        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, VerticalAlignment = VerticalAlignment.Center)
        row.Children.Add chevronHost
        row.Children.Add caption
        let toggle =
            Button(
                Cursor = new Cursor(StandardCursorType.Hand),
                Focusable = true,
                Margin = Thickness 0.0,
                Padding = Thickness(4.0, 4.0),
                CornerRadius = CornerRadius 4.0,
                BorderThickness = Thickness 1.0,
                Content = row)
        toggle.Classes.Add "ghost"
        toggle.Classes.Add "hover-bordered"
        let mutable open' = false
        let sync () =
            rotate.Angle <- (if open' then 90.0 else 0.0)
            caption.Text <- (if open' then "收起技术细节" else "技术细节")
            AutomationProperties.SetName(toggle, caption.Text)
            AutomationProperties.SetHelpText(toggle, (if open' then "收起错误技术细节" else "展开查看技术细节"))
            AutomationProperties.SetItemStatus(toggle, (if open' then "已展开" else "已折叠"))
        sync ()
        toggle.Click.Add(fun _ ->
            open' <- not open'
            setVisible open'
            sync ())
        actionRow.Children.Add toggle
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        detailContainer.Children.Add host
        if not (layout.Children.Contains detailContainer) then layout.Children.Add detailContainer
        toggle

    /// 单入口：从 ErrorPanelVS 渲染
    member this.Render(vs: ErrorPanelVS, onRetry: unit -> unit, ?onContinue: unit -> unit, ?onRecovery: string -> unit) =
        this.SetHint vs.Hint
        if vs.CountdownSeconds >= 0 then
            this.SetSeconds(vs.CountdownSeconds, vs.CountdownSeconds = 0)
        if vs.CanContinue then
            onContinue |> Option.iter (fun f -> this.AddContinue f |> ignore)
        if vs.CanRetry then
            this.AddRetry(vs.CanContinue, onRetry) |> ignore
        match vs.RecoveryLabel, onRecovery with
        | Some label, Some sink -> this.AddRecovery(label, fun () -> sink label) |> ignore
        | _ -> ()
        vs.Detail |> Option.iter (this.AddDetail >> ignore)
