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

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 使用方提供的件
type ErrorPanelParts =
    {
        Copy: string -> string -> (unit -> string) -> Control
        Reveal: Control -> bool -> Control * (bool -> unit)
        CountdownText: int -> string
    }

/// 生成失败卡（旧 MessageCard.errorCardWith）：标题、说明、可选倒计时、继续 / 重试 / 恢复与技术细节披露。
/// 单入口 `Render(vs)`：标题、说明、按钮族、倒计时与回调都在 ErrorPanelVS 里。
/// 幂等：同一实例重复渲染时按钮不被重建（控件身份稳定），剩余秒数只往下走（卡片重建不回跳），读屏播报只发生一次。
type ErrorPanel(parts: ErrorPanelParts) =
    let tick = Event<unit>()
    let announced = Event<unit>()
    let mutable attached = false
    let mutable wanted = false
    let mutable left = 0
    /// 当前是否处在倒计时这一段（false = 没有倒计时）
    let mutable countdownActive = false
    /// 这一段倒计时是否已经播报过（只播一次）
    let mutable announceDone = false
    /// 重试按钮的剩余秒数回写
    let mutable onLeft: int -> unit = ignore
    /// 三个动作的最新回调（按钮建好后随 Render 更新，控件身份不变）
    let mutable continueSink: unit -> unit = ignore
    let mutable retrySink: unit -> unit = ignore
    let mutable recoverySink: string -> unit = fun _ -> ()
    /// 已建好的按钮（按动作族缓存，形状不变就不重建）
    let mutable continueButton: Button option = None
    let mutable retryButton: Button option = None
    /// 重试按钮当前是不是次要档（有「继续生成」时是幽灵档）
    let mutable retrySecondary: bool option = None
    let mutable recoveryButtons: Map<string, Button> = Map.empty
    let mutable detailButton: Button option = None
    let mutable detailText: string option = None

    let icon =
        Path(
            Data = Geometry.Parse "M 7 2 L 12 12 L 2 12 Z M 7 5 L 7 8 M 7 10 L 7 10.01",
            StrokeThickness = 1.3,
            Fill = Brushes.Transparent,
            Width = 16.0,
            Height = 16.0,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Thickness(0.0, 0.0, 12.0, 0.0)
        )

    let titleBlock =
        TextBlock(Text = "", FontWeight = FontWeight.Medium, FontSize = 14.0, TextWrapping = TextWrapping.Wrap)

    let hintBlock =
        TextBlock(Text = "", FontSize = 12.0, TextWrapping = TextWrapping.Wrap, LineHeight = 18.0)

    let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
    let top = DockPanel(LastChildFill = true)

    let countdown =
        TextBlock(TextWrapping = TextWrapping.Wrap, FontSize = 12.0, IsVisible = false)

    let actionRow =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, Margin = Thickness(0.0, 12.0, 0.0, 0.0))

    let detailContainer = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let layout = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)

    let card =
        Border(
            BorderThickness = Thickness 1.0,
            CornerRadius = CornerRadius 12.0,
            Padding = Thickness(16.0, 12.0),
            Margin = Thickness(0.0, 8.0, 0.0, 0.0),
            MaxWidth = 640.0,
            Child = layout
        )

    let textButton (label: string) (secondary: bool) (name: string) (help: string option) (action: unit -> unit) =
        let b =
            Button(
                Content = label,
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = Thickness 0.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        b.Classes.Add(if secondary then "button-secondary" else "button-ghost")
        b.Classes.Add(if secondary then "secondary" else "ghost")
        AutomationProperties.SetName(b, name)
        help |> Option.iter (fun h -> AutomationProperties.SetHelpText(b, h))
        b.Click.Add(fun _ -> action ())
        b

    /// 技术细节披露（旧 AddDetail）：限高视口 + 复制 + 折叠切换。
    let buildDetail (detail: string) : Button =
        let content =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = detail,
                FontSize = 11.0,
                LineHeight = 11.0 * 1.52,
                TextWrapping = TextWrapping.Wrap
            )

        content.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.CodeMuted")
        |> ignore

        content.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

        content.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft")
        |> ignore

        let copy = parts.Copy "复制错误细节" "复制错误技术细节" (fun () -> detail)
        let header = DockPanel(LastChildFill = false)
        DockPanel.SetDock(copy, Dock.Right)
        header.Children.Add copy

        let box =
            Border(
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius 4.0,
                Padding = Thickness(12.0, 8.0),
                Child = content
            )

        box.Bind(Border.BackgroundProperty, Dyn.res "Palette.CodeBg") |> ignore
        box.Bind(Border.BorderBrushProperty, Dyn.res "Palette.CodeBorder") |> ignore
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        stack.Children.Add header
        stack.Children.Add box
        let host, setVisible = parts.Reveal stack false
        host.Margin <- Thickness(0.0, 8.0, 0.0, 0.0)

        let rotate = RotateTransform(Angle = 0.0)

        let rotation =
            DoubleTransition(
                Property = RotateTransform.AngleProperty,
                Duration = TimeSpan.FromMilliseconds 160.0,
                Easing = Avalonia.Animation.Easings.CubicEaseOut()
            )

        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions

        let chevronHost =
            Border(
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 14.0,
                Height = 14.0
            )

        let chevron =
            Path(
                Data = Geometry.Parse "M 4 2 L 9 7 L 4 12",
                StrokeThickness = 1.3,
                Fill = Brushes.Transparent,
                Width = 10.0,
                Height = 10.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        chevron.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron

        let caption =
            TextBlock(
                Text = "技术细节",
                FontWeight = FontWeight.Medium,
                FontSize = 12.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        let row =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 8.0,
                VerticalAlignment = VerticalAlignment.Center
            )

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
                Content = row
            )

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

        detailContainer.Children.Add host
        toggle

    /// 倒计时呈现：剩余秒数写进读屏名，走完时改成功色并播报一次。
    let applyCountdown (announce: bool) =
        let ready = left = 0
        countdown.Text <- parts.CountdownText left
        AutomationProperties.SetName(countdown, sprintf "重试倒计时：%s" countdown.Text)

        if ready then
            countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Success")
            |> ignore
        else
            countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Warning")
            |> ignore

        countdown.IsVisible <- ready

        if not (column.Children.Contains countdown) then
            column.Children.Add countdown

        if announce && ready && not announceDone then
            announceDone <- true
            AutomationProperties.SetLiveSetting(countdown, AutomationLiveSetting.Polite)
            announced.Trigger()

        onLeft left

    do
        icon.Bind(Path.StrokeProperty, Dyn.res "Palette.Danger") |> ignore
        titleBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

        hintBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        countdown.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Warning")
        |> ignore

        card.Bind(Border.BackgroundProperty, Dyn.res "Palette.DangerSoft") |> ignore

        card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Danger") |> ignore
        column.Children.Add titleBlock
        column.Children.Add hintBlock
        DockPanel.SetDock(icon, Dock.Left)
        top.Children.Add icon
        top.Children.Add column
        AutomationProperties.SetName(countdown, "重试倒计时")
        layout.Children.Add top

        card.AttachedToVisualTree.Add(fun _ ->
            attached <- true

            if wanted then
                tick.Trigger())

        card.DetachedFromVisualTree.Add(fun _ -> attached <- false)

    member _.Host: Control = card
    member _.Title = titleBlock
    member _.Hint = hintBlock
    member _.Countdown = countdown
    member _.Actions = actionRow
    /// 当前剩余秒数（没有倒计时时为 0）
    member _.Left = left
    /// 挂上且要求时按间隔发 `Tick`，组合者据此用 CountdownTicker 与宿主的 RetryCountdown 算下一帧。
    member _.Tick = tick.Publish
    /// 倒计时走完时发一次（读屏 polite 播报的同一时刻）。
    member _.Announced = announced.Publish
    member _.IsTicking = wanted && attached

    /// 单入口：从 ErrorPanelVS 渲染。
    /// 幂等：重复渲染不重建按钮（控件身份稳定），倒计时只往下走（卡片重建不回跳），播报只发生一次。
    member this.Render(vs: ErrorPanelVS) =
        titleBlock.Text <- vs.Title
        hintBlock.Text <- vs.Hint

        // 倒计时：只在更小的时候往下走，绝不回跳到更大的剩余秒数
        let announce = vs.Announce

        if vs.CountdownSeconds >= 0 then
            if not countdownActive then
                countdownActive <- true
                announceDone <- false
                left <- vs.CountdownSeconds
            elif vs.CountdownSeconds < left then
                left <- vs.CountdownSeconds
        else
            countdownActive <- false
            announceDone <- false
            left <- 0

        // 回调更新（按钮已建好时不重建控件）
        continueSink <- vs.OnContinue |> Option.defaultValue ignore
        retrySink <- vs.OnRetry
        recoverySink <- vs.OnRecovery |> Option.defaultValue (fun _ -> ())

        let hasContinue =
            vs.Actions
            |> List.exists (function
                | ErrorPanelAction.Continue -> true
                | _ -> false)
        // 按钮区：形状不变就只换回调，形状变了才重建（幂等）
        actionRow.Children.Clear()

        for action in vs.Actions do
            match action with
            | ErrorPanelAction.Continue ->
                let b =
                    match continueButton with
                    | Some b -> b
                    | None ->
                        let b =
                            textButton "继续生成" true "继续生成" (Some "保留已写出的部分，从中断处接着生成") (fun () -> continueSink ())

                        continueButton <- Some b
                        b

                actionRow.Children.Add b
            | ErrorPanelAction.Retry ->
                // 动作形状变了（有/无「继续生成」决定重试是幽灵档还是次要档）才重建按钮
                let secondary = not hasContinue

                let b =
                    match retryButton with
                    | Some b when retrySecondary = Some secondary -> b
                    | _ ->
                        let b =
                            textButton
                                "重试"
                                secondary
                                "重试生成"
                                (Some(if hasContinue then "丢弃已写出的部分，重新生成" else "重新尝试生成"))
                                (fun () -> retrySink ())

                        b.MinWidth <- 80.0

                        let apply (l: int) =
                            let waiting = l > 0
                            b.IsEnabled <- not waiting
                            b.Content <- (if waiting then sprintf "重试（%d 秒）" l else "重试")
                            AutomationProperties.SetName(b, (if waiting then sprintf "重试生成，%d 秒后可用" l else "重试生成"))

                        onLeft <- apply
                        retrySecondary <- Some secondary
                        retryButton <- Some b
                        b

                actionRow.Children.Add b
            | ErrorPanelAction.Recovery label ->
                let b =
                    match Map.tryFind label recoveryButtons with
                    | Some b -> b
                    | None ->
                        let b = textButton label true label None (fun () -> recoverySink label)
                        recoveryButtons <- Map.add label b recoveryButtons
                        b

                actionRow.Children.Add b

        // 技术细节：文本不变就沿用旧按钮（展开状态不丢）。
        // 没有原文时不显示按钮，避免点了没内容。
        match vs.Detail with
        | Some d when not (String.IsNullOrWhiteSpace d) ->
            match detailText with
            | Some prev when prev = d -> detailButton |> Option.iter (fun b -> actionRow.Children.Add b)
            | _ ->
                detailContainer.Children.Clear()
                let b = buildDetail d
                detailButton <- Some b
                detailText <- Some d
                actionRow.Children.Add b
        | _ ->
            detailContainer.Children.Clear()
            detailButton <- None
            detailText <- None

        if actionRow.Children.Count > 0 then
            if not (layout.Children.Contains actionRow) then
                layout.Children.Add actionRow |> ignore
        elif layout.Children.Contains actionRow then
            layout.Children.Remove actionRow |> ignore

        if detailContainer.Children.Count > 0 then
            if not (layout.Children.Contains detailContainer) then
                layout.Children.Add detailContainer |> ignore
        elif layout.Children.Contains detailContainer then
            layout.Children.Remove detailContainer |> ignore

        // 倒计时呈现（在按钮区之后，让重试按钮拿到最新剩余秒数）
        if vs.CountdownSeconds >= 0 then
            applyCountdown announce
        else
            countdown.Text <- ""
            countdown.IsVisible <- false
            onLeft 0

        // 是否走表：只在从「不要求」变成「要求」时催一次，避免与组合者的回写互相递归
        let wasWanted = wanted
        wanted <- vs.Ticking

        if wanted && not wasWanted && attached then
            tick.Trigger()
