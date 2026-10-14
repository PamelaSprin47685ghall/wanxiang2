namespace Wanxiang2.ErrorPanel

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

[<RequireQualifiedAccess>]
type ErrorInk =
    | Text
    | TextMuted
    | Warning
    | Success
    | Danger
    | SurfaceContainer
    | Hairline
    | CodeBg
    | CodeBorder
    | CodeMuted
    | Selection

[<RequireQualifiedAccess>]
type ErrorGauge =
    | BorderWidth
    | SpaceXs
    | SpaceSm
    | SpaceMd
    | SpaceXl
    | Space2xl
    | RadiusSm
    | RadiusMd
    | FontBody
    | FontCaption
    | FontSmall
    | FontMicro
    | TextButtonPadY
    | RetryMinWidth
    | IconGlyph
    | RotateMs
    /// 旧 readingWidth
    | ReadingWidth
    /// 倒计时刷新间隔，毫秒（旧 CountdownTicker.IntervalMs）
    | TickMs

[<RequireQualifiedAccess>]
type ErrorGlyph =
    | Alert
    | Chevron

type ErrorPanelLook =
    { Ink: ErrorInk -> IBrush
      Gauge: ErrorGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      MonoFont: IObservable<FontFamily>
      Glyph: ErrorGlyph -> IBrush -> Control
      /// 次要 / 幽灵按钮样式类（旧 Ui.Secondary / Ghost）
      Dress: bool -> Border -> unit
      /// 折叠披露头的状态反馈
      DressFold: Border -> unit
      TechLineHeight: float -> float
      /// 旧 ReadingRhythm.helperLineHeight
      HelperLineHeight: float
      Reduced: IObservable<bool> }

/// 使用方提供的件
type ErrorPanelParts =
    { Copy: string -> string -> (unit -> string) -> Control
      Reveal: Control -> bool -> Control * (bool -> unit)
      /// 剩余秒数 → 读屏文案（旧 ProviderRateLimit.countdownText）
      CountdownText: int -> string }

/// 可点文字按钮（本库自带，不依赖别的库）
type private TextSpot(label: string) as this =
    inherit Spot()
    let text = TextBlock(Text = label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
    do this.Child <- text
    member _.Label = text

/// 生成失败卡（旧 MessageCard.errorCardWith）：标题、说明、可选倒计时、继续 / 重试 / 恢复与技术细节披露。
/// 倒计时由 `SetSeconds` 驱动；本库的计时器只在挂在树上且 `SetTicking true` 时按 TickMs 发 `Tick`，组合者据此问宿主剩余秒数。
type ErrorPanel(look: ErrorPanelLook, parts: ErrorPanelParts, title: string, hint: string) =
    let ink = look.Ink
    let gauge = look.Gauge
    let tick = Event<unit>()
    let timer = DispatcherTimer()
    let mutable attached = false
    let mutable wanted = false
    let mutable left = 0
    let mutable onLeft: int -> unit = ignore

    let icon = look.Glyph ErrorGlyph.Alert (ink ErrorInk.Danger)
    let titleBlock =
        TextBlock(Text = title, FontWeight = FontWeight.Medium, Foreground = ink ErrorInk.Text, TextWrapping = TextWrapping.Wrap)
    let hintBlock =
        TextBlock(
            Text = hint,
            Foreground = ink ErrorInk.TextMuted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = look.HelperLineHeight)
    let column = StackPanel(Orientation = Orientation.Vertical)
    let top = DockPanel(LastChildFill = true)
    let countdown =
        TextBlock(Foreground = ink ErrorInk.Warning, TextWrapping = TextWrapping.Wrap, IsVisible = false)
    let actionRow = StackPanel(Orientation = Orientation.Horizontal)
    let detailContainer = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let layout = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let card = Border(Background = ink ErrorInk.SurfaceContainer, BorderBrush = ink ErrorInk.Hairline, Child = layout)

    let sized (t: TextBlock) g = Follow.live t (gauge g) (fun s -> t.FontSize <- s)
    let textButton (label: string) (secondary: bool) (name: string) (help: string option) (action: unit -> unit) =
        let b = TextSpot label
        b.Focusable <- true
        b.Cursor <- new Cursor(StandardCursorType.Hand)
        b.Margin <- Thickness 0.0
        b.VerticalAlignment <- VerticalAlignment.Center
        look.Dress secondary b
        Follow.live2 b (gauge ErrorGauge.Space2xl) (gauge ErrorGauge.TextButtonPadY) (fun x y -> b.Padding <- Thickness(x, y))
        AutomationProperties.SetName(b, name)
        help |> Option.iter (fun h -> AutomationProperties.SetHelpText(b, h))
        b.OnInvoke action
        b

    do
        icon.VerticalAlignment <- VerticalAlignment.Top
        Follow.live icon (gauge ErrorGauge.SpaceXl) (fun xl -> icon.Margin <- Thickness(0.0, 0.0, xl, 0.0))
        sized titleBlock ErrorGauge.FontBody
        sized hintBlock ErrorGauge.FontCaption
        Follow.live column (gauge ErrorGauge.SpaceSm) (fun s -> column.Spacing <- s)
        column.Children.Add titleBlock
        column.Children.Add hintBlock
        DockPanel.SetDock(icon, Dock.Left)
        top.Children.Add icon
        top.Children.Add column
        AutomationProperties.SetName(countdown, "重试倒计时")
        sized countdown ErrorGauge.FontCaption
        Follow.live2 actionRow (gauge ErrorGauge.SpaceMd) (gauge ErrorGauge.SpaceXl) (fun md xl ->
            actionRow.Spacing <- md
            actionRow.Margin <- Thickness(0.0, xl, 0.0, 0.0))
        layout.Children.Add top
        Follow.live2 card (gauge ErrorGauge.BorderWidth) (gauge ErrorGauge.RadiusMd) (fun w r ->
            card.BorderThickness <- Thickness w
            card.CornerRadius <- CornerRadius r)
        Follow.live2 card (gauge ErrorGauge.Space2xl) (gauge ErrorGauge.SpaceXl) (fun x y -> card.Padding <- Thickness(x, y))
        Follow.live2 card (gauge ErrorGauge.SpaceMd) (gauge ErrorGauge.ReadingWidth) (fun md w ->
            card.Margin <- Thickness(0.0, md, 0.0, 0.0)
            card.MaxWidth <- w)
        Follow.live card (gauge ErrorGauge.TickMs) (fun ms -> timer.Interval <- TimeSpan.FromMilliseconds ms)
        timer.Tick.Add(fun _ -> if attached then tick.Trigger())
        card.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            if wanted then timer.Start())
        card.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            timer.Stop())

    member _.Host: Control = card
    member _.Title = titleBlock
    member _.Hint = hintBlock
    member _.Countdown = countdown
    member _.Actions = actionRow
    member _.Left = left
    /// 挂在树上时按 TickMs 触发（组合者据此向宿主问剩余秒数）
    member _.Tick = tick.Publish
    member _.IsTicking = timer.IsEnabled

    /// 要不要按 TickMs 回调（有倒计时才要；走完由组合者关掉）
    member _.SetTicking(value: bool) =
        wanted <- value
        if value && attached then timer.Start()
        elif not value then timer.Stop()

    /// 改写说明（额度用尽时旧代码会换同一行）
    member _.SetHint(text: string) = hintBlock.Text <- text

    /// 推送剩余秒数：>0 时重试不可按并改文案；=0 时显示「现在可以重试了」并以 polite 播报一次
    member _.SetSeconds(seconds: int, ?announce: bool) =
        left <- max 0 seconds
        let ready = left = 0
        countdown.Text <- parts.CountdownText left
        AutomationProperties.SetName(countdown, sprintf "重试倒计时：%s" countdown.Text)
        if defaultArg announce false then
            AutomationProperties.SetLiveSetting(countdown, AutomationLiveSetting.Polite)
        countdown.Foreground <- (if ready then ink ErrorInk.Success else ink ErrorInk.Warning)
        // 等待中秒数只在「重试（N 秒）」按钮上说一遍；走完才显示「现在可以重试了」
        countdown.IsVisible <- ready
        if not (column.Children.Contains countdown) then column.Children.Add countdown
        onLeft left

    /// 「继续生成」排在「重试」之前（有半截回复时）
    member _.AddContinue(action: unit -> unit) =
        let b =
            textButton "继续生成" true "继续生成" (Some "保留已写出的部分，从中断处接着生成") action
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b :> Border

    /// 「重试」：有继续时用幽灵档，否则用次要档；倒计时未结束时不可按
    member this.AddRetry(hasContinue: bool, action: unit -> unit) =
        let b =
            textButton "重试" (not hasContinue) "重试生成"
                (Some(if hasContinue then "丢弃已写出的部分，重新生成" else "重新尝试生成"))
                action
        Follow.live b (gauge ErrorGauge.RetryMinWidth) (fun w -> b.MinWidth <- w)
        let apply left =
            let waiting = left > 0
            b.IsEnabled <- not waiting
            b.Label.Text <- (if waiting then sprintf "重试（%d 秒）" left else "重试")
            AutomationProperties.SetName(b, (if waiting then sprintf "重试生成，%d 秒后可用" left else "重试生成"))
        onLeft <- apply
        apply left
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b :> Border

    /// 恢复动作（打开设置等）
    member _.AddRecovery(label: string, action: unit -> unit) =
        let b = textButton label true label None action
        actionRow.Children.Add b
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        b :> Border

    /// 技术细节披露（折叠头 + 限高视口 + 复制）
    member _.AddDetail(detail: string) =
        let content =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = detail,
                Foreground = ink ErrorInk.CodeMuted,
                TextWrapping = TextWrapping.Wrap,
                SelectionBrush = ink ErrorInk.Selection)
        Follow.live content look.MonoFont (fun f -> content.FontFamily <- f)
        Follow.live content (gauge ErrorGauge.FontMicro) (fun size ->
            content.FontSize <- size
            content.LineHeight <- look.TechLineHeight size)
        let copy = parts.Copy "复制错误细节" "复制错误技术细节" (fun () -> detail)
        let header = DockPanel(LastChildFill = false)
        DockPanel.SetDock(copy, Dock.Right)
        header.Children.Add copy
        let box = Border(Background = ink ErrorInk.CodeBg, BorderBrush = ink ErrorInk.CodeBorder, Child = content)
        Follow.live2 box (gauge ErrorGauge.BorderWidth) (gauge ErrorGauge.RadiusSm) (fun w r ->
            box.BorderThickness <- Thickness w
            box.CornerRadius <- CornerRadius r)
        Follow.live2 box (gauge ErrorGauge.SpaceXl) (gauge ErrorGauge.SpaceMd) (fun xl md -> box.Padding <- Thickness(xl, md))
        let stack = StackPanel(Orientation = Orientation.Vertical)
        Follow.live stack (gauge ErrorGauge.SpaceXs) (fun s -> stack.Spacing <- s)
        stack.Children.Add header
        stack.Children.Add box
        let host, setVisible = parts.Reveal stack false
        Follow.live host (gauge ErrorGauge.SpaceMd) (fun md -> host.Margin <- Thickness(0.0, md, 0.0, 0.0))
        // 折叠头：与思考过程同一套箭头语法，文案「技术细节 / 收起技术细节」
        let fold =
            let rotate = Avalonia.Media.RotateTransform 0.0
            let rotation = Avalonia.Animation.DoubleTransition(Property = Avalonia.Media.RotateTransform.AngleProperty, Easing = Avalonia.Animation.Easings.CubicEaseOut())
            let transitions = Avalonia.Animation.Transitions()
            transitions.Add rotation
            rotate.Transitions <- transitions
            let chevronHost = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
            let chevron = look.Glyph ErrorGlyph.Chevron (ink ErrorInk.TextMuted)
            chevron.HorizontalAlignment <- HorizontalAlignment.Center
            chevron.VerticalAlignment <- VerticalAlignment.Center
            chevron.RenderTransform <- rotate
            chevron.RenderTransformOrigin <- RelativePoint.Center
            chevronHost.Child <- chevron
            Follow.live chevronHost (gauge ErrorGauge.IconGlyph) (fun g ->
                chevronHost.Width <- g
                chevronHost.Height <- g
                chevronHost.MinWidth <- g
                chevronHost.MinHeight <- g)
            Follow.live2 chevronHost (gauge ErrorGauge.RotateMs) look.Reduced (fun ms reduced ->
                rotation.Duration <- (if reduced then TimeSpan.Zero else TimeSpan.FromMilliseconds ms))
            let caption = TextBlock(Text = "技术细节", FontWeight = FontWeight.Medium, Foreground = ink ErrorInk.TextMuted, VerticalAlignment = VerticalAlignment.Center)
            sized caption ErrorGauge.FontSmall
            let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
            Follow.live row (gauge ErrorGauge.SpaceMd) (fun s -> row.Spacing <- s)
            row.Children.Add chevronHost
            row.Children.Add caption
            let toggle =
                Spot(
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Focusable = true,
                    Child = row)
            Follow.live3 toggle (gauge ErrorGauge.SpaceXs) (gauge ErrorGauge.SpaceSm) (gauge ErrorGauge.RadiusSm) (fun xs sm r ->
                toggle.Padding <- Thickness(xs, sm)
                toggle.CornerRadius <- CornerRadius r)
            Follow.live toggle (gauge ErrorGauge.BorderWidth) (fun w -> toggle.BorderThickness <- Thickness w)
            look.DressFold toggle
            AutomationProperties.SetControlTypeOverride(toggle, Nullable Avalonia.Automation.Peers.AutomationControlType.Button)
            let mutable open' = false
            let sync () =
                rotate.Angle <- (if open' then 90.0 else 0.0)
                caption.Text <- (if open' then "收起技术细节" else "技术细节")
                AutomationProperties.SetName(toggle, caption.Text)
                AutomationProperties.SetHelpText(toggle, (if open' then "收起错误技术细节" else "展开查看技术细节"))
                AutomationProperties.SetItemStatus(toggle, (if open' then "已展开" else "已折叠"))
            sync ()
            toggle.OnInvoke(fun () ->
                open' <- not open'
                setVisible open'
                sync ())
            toggle
        actionRow.Children.Add fold
        if not (layout.Children.Contains actionRow) then layout.Children.Add actionRow
        detailContainer.Children.Add host
        if not (layout.Children.Contains detailContainer) then layout.Children.Add detailContainer
        fold :> Border
