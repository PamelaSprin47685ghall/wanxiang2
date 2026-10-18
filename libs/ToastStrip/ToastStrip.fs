namespace Wanxiang2.ToastStrip

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.UiKit

/// 提示条语气。Info 承载信息性进展播报，与 Neutral 的弱存在提示分开。
[<RequireQualifiedAccess>]
type ToastTone =
    | Neutral
    | Success
    | Warning
    | Failure
    | Info

/// 提示条外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type ToastStripLook =
    { Surface: IBrush
      Edge: IBrush
      Text: IBrush
      /// 各语气的强调条画笔
      Accent: ToastTone -> IBrush
      AccentWidth: IObservable<float>
      AccentRadius: IObservable<CornerRadius>
      AccentOpacity: IObservable<float>
      EdgeWidth: IObservable<Thickness>
      Radius: IObservable<CornerRadius>
      Padding: IObservable<Thickness>
      Shadow: IObservable<BoxShadows>
      FontSize: IObservable<float>
      LineHeight: IObservable<float>
      ColumnSpacing: IObservable<float>
      /// 条与条的间距
      Spacing: IObservable<float>
      /// 整条带离视口边的外边距
      Margin: IObservable<Thickness>
      MaxWidth: IObservable<float>
      BodyMaxHeight: IObservable<float>
      /// 进场淡入时长；减弱动效时主题给 0
      Fade: IObservable<TimeSpan>
      FadeEasing: Avalonia.Animation.Easings.Easing }

/// 提示条带（迁自旧 Controls/OverlayHost.fs 的提示条渲染部分）。
/// 只管画与移除；去重、上限、停留计时由组合者用 ToastQueue / ToastDwell 接。
type ToastStrip(look: ToastStripLook) =
    let stack = StackPanel(Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom)
    let toasts = Collections.Generic.Dictionary<string, Control * Control option>()
    let buttons = ResizeArray<string * Control>()
    let mutable maxWidth = Double.PositiveInfinity
    let mutable viewportMaxWidth = Double.PositiveInfinity
    let mutable fade = TimeSpan.Zero
    let mutable focusHome: unit -> unit = ignore
    let clicked = Event<string>()
    let actionInvoked = Event<string>()
    let pointerEntered = Event<string>()
    let pointerExited = Event<string * bool>()
    let actionFocused = Event<string>()
    let actionUnfocused = Event<string>()

    let applyWidth () =
        let w = min maxWidth viewportMaxWidth
        stack.MaxWidth <- w
        for c in stack.Children do c.MaxWidth <- w

    do
        look.Spacing.Subscribe(fun v -> stack.Spacing <- v) |> ignore
        look.Margin.Subscribe(fun v -> stack.Margin <- v) |> ignore
        look.MaxWidth.Subscribe(fun v -> maxWidth <- v; applyWidth ()) |> ignore
        look.Fade.Subscribe(fun v -> fade <- v) |> ignore

    /// 整条带（组合者把它加进最外层 Grid）
    member _.View: StackPanel = stack

    /// 按视口钳宽高：宽留两侧 inset，高留底部外边距与顶部 inset。
    member _.Fit(viewportWidth: float, viewportHeight: float, inset: float) =
        let width = max 0.0 (viewportWidth - inset * 2.0)
        if width > 0.0 then
            viewportMaxWidth <- width
            applyWidth ()
        let height = max 0.0 (viewportHeight - stack.Margin.Bottom - inset)
        if height > 0.0 then stack.MaxHeight <- height

    /// 加一条。同文条目由组合者先移除；本方法不去重。
    member _.Add(message: string, tone: ToastTone, action: string option) =
        let bar = Border(Background = look.Accent tone)
        look.AccentWidth.Subscribe(fun v -> bar.Width <- v) |> ignore
        look.AccentRadius.Subscribe(fun v -> bar.CornerRadius <- v) |> ignore
        look.AccentOpacity.Subscribe(fun v -> bar.Opacity <- v) |> ignore
        let body =
            TextBlock(Text = message, Foreground = look.Text, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center)
        look.FontSize.Subscribe(fun v -> body.FontSize <- v) |> ignore
        look.LineHeight.Subscribe(fun v -> body.LineHeight <- v) |> ignore
        let scroller =
            ScrollViewer(
                Content = body,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
        look.BodyMaxHeight.Subscribe(fun v -> scroller.MaxHeight <- v) |> ignore
        let row = Grid()
        look.ColumnSpacing.Subscribe(fun v -> row.ColumnSpacing <- v) |> ignore
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        Grid.SetColumn(bar, 0)
        Grid.SetColumn(scroller, 1)
        row.Children.Add bar
        row.Children.Add scroller
        let actionButton =
            action
            |> Option.map (fun label ->
                row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
                // 外观由主题的 surface + button-secondary + hover-plain 类给（旧 Ui.button Secondary）。
                let b = InvokeBorder(Child = TextBlock(Text = label), Focusable = true, IsTabStop = true, Name = "toast-action", VerticalAlignment = VerticalAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand))
                b.SetAction(fun () -> actionInvoked.Trigger message)
                for c in [ "surface"; "button-secondary"; "hover-plain" ] do b.Classes.Add c
                AutomationProperties.SetControlTypeOverride(b, Nullable Avalonia.Automation.Peers.AutomationControlType.Button)
                AutomationProperties.SetName(b, label)
                // 读屏把快捷键念出来。
                if label = "撤销" then
                    AutomationProperties.SetAcceleratorKey(b, "Ctrl+Z")
                    AutomationProperties.SetHelpText(b, "按 Ctrl / ⌘ + Z 直接撤销；按 F6 或 Tab 可移到这里")
                b.PointerReleased.Add(fun e ->
                    if e.InitialPressMouseButton = MouseButton.Left then
                        e.Handled <- true
                        actionInvoked.Trigger message)
                b.KeyDown.Add(fun e ->
                    if e.Key = Key.Enter || e.Key = Key.Space then
                        e.Handled <- true
                        actionInvoked.Trigger message)
                b.GotFocus.Add(fun _ -> actionFocused.Trigger message)
                b.LostFocus.Add(fun _ -> actionUnfocused.Trigger message)
                Grid.SetColumn(b, 2)
                row.Children.Add b
                buttons.Add(message, b :> Control)
                b :> Control)
        // 瞬态提示条不进 Tab 序：读屏经由 live region 公告，键盘关闭走全局 Escape。
        let toast = Border(Background = look.Surface, BorderBrush = look.Edge, Child = row, Focusable = false, MaxWidth = stack.MaxWidth)
        look.EdgeWidth.Subscribe(fun v -> toast.BorderThickness <- v) |> ignore
        look.Radius.Subscribe(fun v -> toast.CornerRadius <- v) |> ignore
        look.Padding.Subscribe(fun v -> toast.Padding <- v) |> ignore
        look.Shadow.Subscribe(fun v -> toast.BoxShadow <- v) |> ignore
        // 只补间 Opacity，不做位移/尺寸动画。
        let t = Avalonia.Animation.DoubleTransition(Property = Visual.OpacityProperty, Duration = fade, Easing = look.FadeEasing)
        let ts = Avalonia.Animation.Transitions()
        ts.Add t
        toast.Transitions <- ts
        toast.Opacity <- 0.0
        toast.Cursor <- new Cursor(StandardCursorType.Hand)
        ToolTip.SetTip(toast, "点击关闭")
        AutomationProperties.SetName(toast, message)
        AutomationProperties.SetLiveSetting(
            toast,
            match tone with
            | ToastTone.Failure
            | ToastTone.Warning -> AutomationLiveSetting.Assertive
            | _ -> AutomationLiveSetting.Polite)
        toast.PointerReleased.Add(fun e ->
            if not e.Handled then
                e.Handled <- true
                clicked.Trigger message)
        toast.PointerEntered.Add(fun _ -> pointerEntered.Trigger message)
        toast.PointerExited.Add(fun _ ->
            pointerExited.Trigger(message, actionButton |> Option.exists (fun b -> b.IsFocused)))
        toasts[message] <- (toast :> Control, actionButton)
        stack.Children.Add toast
        toast.Opacity <- 1.0

    /// 同步移除（与旧代码一致：退场不做异步动画）。被移除条的动作按钮持有焦点时，焦点回到归宿。
    member _.Remove(message: string) =
        match toasts.TryGetValue message with
        | true, (toast, button) ->
            toasts.Remove message |> ignore
            buttons.RemoveAll(fun (m, _) -> m = message) |> ignore
            let hadFocus = button |> Option.exists (fun b -> b.IsFocused)
            stack.Children.Remove toast |> ignore
            if hadFocus then focusHome ()
        | _ -> ()

    member _.Contains(message: string) = toasts.ContainsKey message
    member _.Messages = stack.Children |> Seq.choose (fun c -> toasts |> Seq.tryFind (fun kv -> obj.ReferenceEquals(fst kv.Value, c)) |> Option.map (fun kv -> kv.Key)) |> List.ofSeq

    /// F6：焦点移到最新一条的动作按钮；没有则 false。
    member _.FocusAction() : bool =
        match buttons |> Seq.tryLast with
        | Some(_, b) when b.IsEffectivelyVisible -> b.Focus(NavigationMethod.Tab) |> ignore; true
        | _ -> false

    member _.SetFocusHome(home: unit -> unit) = focusHome <- home
    member _.ActionButtons = buttons |> Seq.map snd |> List.ofSeq

    /// 点了条身（组合者关条）
    [<CLIEvent>] member _.Clicked = clicked.Publish
    /// 点了动作按钮（组合者先关条再执行动作）
    [<CLIEvent>] member _.ActionInvoked = actionInvoked.Publish
    [<CLIEvent>] member _.PointerEnteredToast = pointerEntered.Publish
    /// 指针离开；第二项为动作按钮此刻是否持有焦点
    [<CLIEvent>] member _.PointerExitedToast = pointerExited.Publish
    [<CLIEvent>] member _.ActionFocused = actionFocused.Publish
    [<CLIEvent>] member _.ActionUnfocused = actionUnfocused.Publish
