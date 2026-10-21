namespace Wanxiang2.ToastStrip

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions
open Wanxiang2.WorkspaceState

/// 提示条语气。Info 承载信息性进展播报，与 Neutral 的弱存在提示分开。
/// 直接复用 WorkspaceState.ToastTone
type ToastTone = Wanxiang2.WorkspaceState.ToastTone

/// 动态资源绑定辅助：主题管理者把活值写进 ResourceDictionary，控件绑定即随主题资源生效。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 提示条外观：画笔与静态布局参数由组合者填（普通记录，无活值字段）。
/// 主题管得住的尺寸走资源键声明式绑定：强调条宽 Metric.ToastAccentWidth、
/// 圆角 CornerRadius.RadiusPill、描边 Thickness.BorderThin、外壳 CornerRadius.RadiusMd、
/// 字号 Metric.FontBody。本库不再有 IObservable 字段与手工 Subscribe。
type ToastStripAppearance =
    {
        Surface: IBrush
        Edge: IBrush
        Text: IBrush
        /// 各语气的强调条画笔
        Accent: ToastTone -> IBrush
        /// 强调条不透明度
        AccentOpacity: float
        Padding: Thickness
        Shadow: BoxShadows
        LineHeight: float
        ColumnSpacing: float
        /// 条与条的间距
        Spacing: float
        /// 整条带离视口边的外边距
        Margin: Thickness
        MaxWidth: float
        BodyMaxHeight: float
        /// 进场淡入时长；减弱动效时主题给 0
        Fade: TimeSpan
        FadeEasing: Avalonia.Animation.Easings.Easing
    }

/// 提示条带（迁自旧 Controls/OverlayHost.fs 的提示条渲染部分）。
/// 只管画与移除；去重、上限、停留计时由组合者用 ToastQueue / ToastDwell 接。
type ToastStrip(look: ToastStripAppearance) =
    let stack =
        StackPanel(
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        )

    let toasts = Collections.Generic.Dictionary<string, Control * Control option>()
    let buttons = ResizeArray<string * Control>()
    let mutable maxWidth = Double.PositiveInfinity
    let mutable viewportMaxWidth = Double.PositiveInfinity
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

        for c in stack.Children do
            c.MaxWidth <- w

    do
        maxWidth <- look.MaxWidth
        stack.Spacing <- look.Spacing
        stack.Margin <- look.Margin
        applyWidth ()

    /// 整条带（组合者把它加进最外层 Grid）
    member _.View: StackPanel = stack

    /// 按视口钳宽高：宽留两侧 inset，高留底部外边距与顶部 inset。
    member _.Fit(viewportWidth: float, viewportHeight: float, inset: float) =
        let width = max 0.0 (viewportWidth - inset * 2.0)

        if width > 0.0 then
            viewportMaxWidth <- width
            applyWidth ()

        let height = max 0.0 (viewportHeight - stack.Margin.Bottom - inset)

        if height > 0.0 then
            stack.MaxHeight <- height

    /// 加一条。同文条目由组合者先移除；本方法不去重。
    member _.Add(message: string, tone: ToastTone, action: string option) =
        let bar = Border(Background = look.Accent tone)
        bar.Bind(Layoutable.WidthProperty, Dyn.res "Metric.ToastAccentWidth") |> ignore

        bar.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusPill")
        |> ignore

        bar.Opacity <- look.AccentOpacity

        let body =
            TextBlock(
                Text = message,
                Foreground = look.Text,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            )

        body.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
        body.LineHeight <- look.LineHeight

        let scroller =
            ScrollViewer(
                Content = body,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto
            )

        scroller.MaxHeight <- look.BodyMaxHeight
        let row = Grid()
        row.ColumnSpacing <- look.ColumnSpacing
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        Grid.SetColumn(bar, 0)
        Grid.SetColumn(scroller, 1)
        row.Children.Add bar
        row.Children.Add scroller

        let actionButton =
            action
            |> Option.map (fun (label: string) ->
                row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
                // 原生 Button 承载动作；外观走 secondary 样式类
                let b =
                    Button(
                        Content = label,
                        Focusable = true,
                        IsTabStop = true,
                        Name = "toast-action",
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = new Cursor(StandardCursorType.Hand)
                    )

                b.Classes.Add "secondary"
                b.Classes.Add "button-secondary"
                b.Classes.Add "hover-plain"
                b.Classes.Add "surface"
                AutomationProperties.SetName(b, label)
                // 读屏把快捷键念出来。
                if label = "撤销" then
                    AutomationProperties.SetAcceleratorKey(b, "Ctrl+Z")
                    AutomationProperties.SetHelpText(b, "按 Ctrl / ⌘ + Z 直接撤销；按 F6 或 Tab 可移到这里")

                let fire () = actionInvoked.Trigger message
                b.Click.Add(fun _ -> fire ())

                b.KeyDown.Add(fun e ->
                    if e.Key = Key.Enter || e.Key = Key.Space then
                        e.Handled <- true
                        fire ())

                b.GotFocus.Add(fun _ -> actionFocused.Trigger message)
                b.LostFocus.Add(fun _ -> actionUnfocused.Trigger message)
                Grid.SetColumn(b, 2)
                row.Children.Add b
                buttons.Add(message, b :> Control)
                b :> Control)
        // 瞬态提示条不进 Tab 序：读屏经由 live region 公告，键盘关闭走全局 Escape。
        let toast =
            Border(
                Background = look.Surface,
                BorderBrush = look.Edge,
                Child = row,
                Focusable = false,
                MaxWidth = stack.MaxWidth
            )

        toast.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.BorderThin")
        |> ignore

        toast.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusMd")
        |> ignore

        toast.Padding <- look.Padding
        toast.BoxShadow <- look.Shadow
        // 只补间 Opacity，不做位移/尺寸动画。
        let t =
            Avalonia.Animation.DoubleTransition(
                Property = Visual.OpacityProperty,
                Duration = look.Fade,
                Easing = look.FadeEasing
            )

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
            | _ -> AutomationLiveSetting.Polite
        )

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

            if hadFocus then
                focusHome ()
        | _ -> ()

    /// 契约单入口：幂等渲染当前活动 Toast 列表，增删由差异驱动
    member this.Render(toastsList: ToastVS list) =
        let activeTexts = toastsList |> List.map (fun t -> t.Text) |> Set.ofList
        // 移除不在列表里的旧条
        for msg in List.ofSeq toasts.Keys do
            if not (Set.contains msg activeTexts) then
                this.Remove msg
        // 添加尚未显示的新条
        for t in toastsList do
            if not (toasts.ContainsKey t.Text) then
                let actionParam = t.UndoAction |> Option.map (fun _ -> "撤销")
                this.Add(t.Text, t.Tone, actionParam)

    member _.Contains(message: string) = toasts.ContainsKey message

    member _.Messages =
        stack.Children
        |> Seq.choose (fun c ->
            toasts
            |> Seq.tryFind (fun kv -> obj.ReferenceEquals(fst kv.Value, c))
            |> Option.map (fun kv -> kv.Key))
        |> List.ofSeq

    /// F6：焦点移到最新一条的动作按钮；没有则 false。
    member _.FocusAction() : bool =
        match buttons |> Seq.tryLast with
        | Some(_, b) when b.IsEffectivelyVisible ->
            b.Focus(NavigationMethod.Tab) |> ignore
            true
        | _ -> false

    /// 动作按钮持焦的条被移除时，焦点回到归宿（组合者填）。
    member _.WireFocusHome(home: unit -> unit) = focusHome <- home
    member _.ActionButtons = buttons |> Seq.map snd |> List.ofSeq

    /// 点了条身（组合者关条）
    [<CLIEvent>]
    member _.Clicked = clicked.Publish

    /// 点了动作按钮（组合者先关条再执行动作）
    [<CLIEvent>]
    member _.ActionInvoked = actionInvoked.Publish

    [<CLIEvent>]
    member _.PointerEnteredToast = pointerEntered.Publish

    /// 指针离开；第二项为动作按钮此刻是否持有焦点
    [<CLIEvent>]
    member _.PointerExitedToast = pointerExited.Publish

    [<CLIEvent>]
    member _.ActionFocused = actionFocused.Publish

    [<CLIEvent>]
    member _.ActionUnfocused = actionUnfocused.Publish
