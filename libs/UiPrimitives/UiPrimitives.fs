namespace Wanxiang2.UiPrimitives

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 可激活宿主：Border 子类，自动化层提供 Invoke 模式（迁自旧 Controls/Primitives.fs 的 ActionBorder）。
/// 状态视觉（焦点环、悬停、按压、禁用）由主题按 `surface` 类名提供，本类型只把「按钮」语义暴露给自动化层。
/// StyleKeyOverride = typeof<Border> 是必需的（实测）：Avalonia 默认返回自身类型，于是 OfType<Border>() 选不中，
/// 所有状态样式静默失配。声明「我就是一个 Border」让状态样式对本类型与原生 Border 一视同仁。
type InvokeBorder() as this =
    inherit Border()

    let mutable invokeAction: unit -> unit = ignore

    override _.StyleKeyOverride = typeof<Border>

    member _.SetAction(action: unit -> unit) = invokeAction <- action

    member internal _.InvokeFromAutomation() =
        if this.IsEnabled then invokeAction ()

    override _.OnCreateAutomationPeer() = InvokeBorderAutomationPeer(this) :> AutomationPeer

and internal InvokeBorderAutomationPeer(owner: InvokeBorder) =
    inherit ControlAutomationPeer(owner)

    override _.GetAutomationControlTypeCore() = AutomationControlType.Button

    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.InvokeFromAutomation())

/// 开关宿主：Border 子类，自动化层提供 CheckBox + Toggle 模式（迁自旧 ToggleBorder）。
/// on/off 底色是值而非状态，因此本类型只把开关语义暴露给自动化层，视觉由主题的类名给。
type ToggleBorder() as this =
    inherit Border()

    let mutable toggleAction: unit -> unit = ignore
    let mutable value = false

    let stateOf v = if v then ToggleState.On else ToggleState.Off

    override _.StyleKeyOverride = typeof<Border>

    member _.SetToggleAction(action: unit -> unit) = toggleAction <- action
    member _.AutomationValue = value

    member _.SetAutomationValue(next: bool) =
        if value <> next then
            let previous = stateOf value
            value <- next
            match ControlAutomationPeer.FromElement(this) with
            | null -> ()
            | peer ->
                peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, previous, stateOf next)

    member internal _.ToggleFromAutomation() =
        if this.IsEnabled then toggleAction ()

    override _.OnCreateAutomationPeer() = ToggleBorderAutomationPeer(this) :> AutomationPeer

and internal ToggleBorderAutomationPeer(owner: ToggleBorder) =
    inherit ControlAutomationPeer(owner)

    override _.GetAutomationControlTypeCore() = AutomationControlType.CheckBox

    interface IToggleProvider with
        member _.ToggleState = if owner.AutomationValue then ToggleState.On else ToggleState.Off
        member _.Toggle() = Dispatcher.UIThread.Post(fun () -> owner.ToggleFromAutomation())

/// 文字按钮的语义层级（迁自旧 Ui.ButtonTone）。层级决定视觉重量，不由调用方自由涂色。
[<RequireQualifiedAccess>]
type ButtonTone =
    /// 页面主操作，一屏最多一个
    | Primary
    /// 常规操作
    | Secondary
    /// 弱操作，无边框
    | Ghost
    /// 破坏性操作
    | Danger

/// 无样式原语的外观。全部由主题管理者提供活的实例；本库不写任何字面样式值。
/// 静止底、悬停、按压、禁用、焦点环一律由主题按类名给（compose/Theme 的 ThemeStyles），
/// 这里只列类名给不出的量：文字阶梯、输入壳的几何与色、忙指示器的笔画与帧。
type PrimitivesLook =
    { BodyFontSize: IObservable<float>
      BodyForeground: IBrush
      CaptionFontSize: IObservable<float>
      CaptionForeground: IBrush
      CaptionLineHeight: IObservable<float>
      FieldLabelFontSize: IObservable<float>
      FieldLabelForeground: IBrush
      FieldLabelTracking: IObservable<float>
      FieldInsetX: IObservable<float>
      FieldLabelGap: IObservable<float>
      TitleFontSize: IObservable<float>
      TitleForeground: IBrush
      EmphasisTracking: IObservable<float>
      FieldSurface: IBrush
      FieldBorder: IBrush
      FieldBorderWidth: IObservable<float>
      FieldRadius: IObservable<CornerRadius>
      FieldPadding: IObservable<Thickness>
      FieldTextMinHeight: IObservable<float>
      FieldFontSize: IObservable<float>
      FieldText: IBrush
      FieldCaret: IBrush
      MutedForeground: IBrush
      OnAccentForeground: IBrush
      SpinnerTrack: IBrush
      SpinnerAccent: IBrush
      SpinnerStrokeWidth: IObservable<float>
      SpinnerCanvasSize: IObservable<float>
      SpinnerFrame: TimeSpan
      Reduced: IObservable<bool> }

/// 基础控件工厂（迁自旧 Controls/Primitives.fs 的 Ui 模块与 ActionBorder / ToggleBorder）。
/// 全应用的按钮、输入、标签、徽标都从这里出，保证同一种语义在任何界面里长得一样、反馈一样。
/// 外观走主题的类名与 Look 的活实例；跨库协作（提示条抑制、校验描边、输入方式）由组合者接线，本库不引用兄弟库。
module Primitives =

    let private handCursor = new Cursor(StandardCursorType.Hand)
    let private live (source: IObservable<'T>) (apply: 'T -> unit) = source.Subscribe apply |> ignore

    // ---------- 文字 ----------

    let label (look: PrimitivesLook) (text: string) : TextBlock =
        let block = TextBlock(Text = text, Foreground = look.BodyForeground, VerticalAlignment = VerticalAlignment.Center)
        live look.BodyFontSize (fun v -> block.FontSize <- v)
        block

    let caption (look: PrimitivesLook) (text: string) : TextBlock =
        let block = TextBlock(Text = text, Foreground = look.CaptionForeground, TextWrapping = TextWrapping.Wrap)
        live look.CaptionFontSize (fun v -> block.FontSize <- v)
        live look.CaptionLineHeight (fun v -> block.LineHeight <- v)
        block

    let fieldLabel (look: PrimitivesLook) (text: string) : TextBlock =
        let block = TextBlock(Text = text, Foreground = look.FieldLabelForeground, FontWeight = FontWeight.Medium)
        let mutable inset = 0.0
        let mutable gap = 0.0
        let refresh () = block.Margin <- Thickness(inset, 0.0, 0.0, gap)
        live look.FieldLabelFontSize (fun v -> block.FontSize <- v)
        live look.FieldLabelTracking (fun v -> block.LetterSpacing <- v)
        live look.FieldInsetX (fun v -> inset <- v; refresh ())
        live look.FieldLabelGap (fun v -> gap <- v; refresh ())
        block

    let title (look: PrimitivesLook) (text: string) : TextBlock =
        let block = TextBlock(Text = text, Foreground = look.TitleForeground, FontWeight = FontWeight.Medium)
        live look.TitleFontSize (fun v -> block.FontSize <- v)
        block

    // ---------- 结构 ----------

    let hstack (spacing: float) (children: Control seq) : StackPanel =
        let panel = StackPanel(Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center)
        for child in children do panel.Children.Add child
        panel

    let vstack (spacing: float) (children: Control seq) : StackPanel =
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = spacing)
        for child in children do panel.Children.Add child
        panel

    let spacer () : Border = Border(Background = Brushes.Transparent)

    /// 标准分隔线（迁自旧 Ui.hairline）：几何与颜色由主题的 `hairline` 类给。
    let hairline () : Border =
        let line = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        line.Classes.Add "hairline"
        line

    /// 分组卡（迁自旧 Ui.groupingCard）：暖纸中间容器 + 最轻发丝线 + 圆角 + 调用方内边距。
    /// 底、描边、圆角由主题的 `grouping-card` 类给；内边距由调用方给。
    let groupingCard (padding: Thickness) (child: Control) (radius: float) : Border =
        let card = Border(Padding = padding, CornerRadius = CornerRadius radius, Child = child)
        card.Classes.Add "grouping-card"
        card

    // ---------- 徽标 ----------

    /// 标签语气档（迁自旧 Ui.TagTone）。
    [<RequireQualifiedAccess>]
    type TagTone =
        | Neutral
        | Warning
        | Danger

    /// 带语气的小标签（迁自旧 Ui.tagWith）。几何与配色由主题的 `tag` / `tag-warning` / `tag-danger` 类给。
    let tagWith (tone: TagTone) (text: string) : Border =
        let tag = Border(VerticalAlignment = VerticalAlignment.Center, Child = TextBlock(Text = text, VerticalAlignment = VerticalAlignment.Center))
        tag.Classes.Add "tag"
        match tone with
        | TagTone.Neutral -> ()
        | TagTone.Warning -> tag.Classes.Add "tag-warning" |> ignore
        | TagTone.Danger -> tag.Classes.Add "tag-danger" |> ignore
        tag

    /// 中性小标签（不抢强调色配额）。
    let tag (text: string) : Border = tagWith TagTone.Neutral text

    // ---------- 忙指示器 ----------

    /// 旋转指示器（迁自旧 Ui.spinner）：只在真正等待时出现。帧与减弱动效来自 Look。
    let spinner (look: PrimitivesLook) (size: float) : Control =
        let track =
            Path(
                Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 1 1 7.99 1.6",
                Stroke = look.SpinnerTrack,
                Fill = Brushes.Transparent,
                Stretch = Stretch.None,
                IsVisible = false)
        let arc =
            Path(
                Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 0 1 14.4 8",
                Stroke = look.SpinnerAccent,
                StrokeLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.None)
        let host = Canvas()
        live look.SpinnerCanvasSize (fun v ->
            host.Width <- v
            host.Height <- v)
        live look.SpinnerStrokeWidth (fun v ->
            track.StrokeThickness <- v
            arc.StrokeThickness <- v)
        host.Children.Add track |> ignore
        host.Children.Add arc |> ignore
        let view =
            Viewbox(
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Child = host,
                RenderTransform = RotateTransform 0.0,
                RenderTransformOrigin = RelativePoint.Center)
        let timer = new DispatcherTimer(Interval = look.SpinnerFrame)
        let mutable angle = 0.0
        let mutable attached = false
        let mutable reduced = false
        let mutable motionSubscription: IDisposable option = None
        let refreshMotion () =
            if reduced then
                timer.Stop()
                angle <- 0.0
                view.RenderTransform <- RotateTransform 0.0
                track.IsVisible <- true
            elif attached then
                track.IsVisible <- false
                timer.Start()
            else
                track.IsVisible <- false
                timer.Stop()
        timer.Tick.Add(fun _ ->
            angle <- (angle + 18.0) % 360.0
            view.RenderTransform <- RotateTransform angle)
        view.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            motionSubscription |> Option.iter (fun subscription -> subscription.Dispose())
            motionSubscription <- Some(look.Reduced.Subscribe(fun v ->
                reduced <- v
                refreshMotion ()))
            refreshMotion ())
        view.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            timer.Stop()
            motionSubscription |> Option.iter (fun subscription -> subscription.Dispose())
            motionSubscription <- None)
        view :> Control

    // ---------- 图标按钮 ----------

    /// 图标按钮（迁自旧 Ui.iconButton）。尺寸、圆角、静止底、悬停、按压、禁用、焦点环由主题的 `icon-button` 类给。
    /// 触屏提示抑制（旧 InputSense.SuppressTooltipOnTouch）由组合者接线。
    let iconButton (look: PrimitivesLook) (icon: IBrush -> Control) (tip: string) : Border =
        let host =
            InvokeBorder(
                Cursor = handCursor,
                Focusable = true,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center)
        let glyph = icon look.MutedForeground
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        glyph.VerticalAlignment <- VerticalAlignment.Center
        host.Child <- glyph
        host.Classes.Add "surface"
        host.Classes.Add "icon-button"
        host.Classes.Add "hover-plain"
        if not (String.IsNullOrWhiteSpace tip) then
            ToolTip.SetTip(host, tip)
            AutomationProperties.SetName(host, tip)
        host :> Border

    /// 强调色实心图标按钮（迁自旧 Ui.iconButtonAccent）。
    let iconButtonAccent (look: PrimitivesLook) (icon: IBrush -> Control) (tip: string) : Border =
        let host =
            InvokeBorder(
                Cursor = handCursor,
                Focusable = true,
                VerticalAlignment = VerticalAlignment.Center)
        let glyph = icon look.OnAccentForeground
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        glyph.VerticalAlignment <- VerticalAlignment.Center
        host.Child <- glyph
        host.Classes.Add "surface"
        host.Classes.Add "icon-button-accent"
        host.Classes.Add "hover-accent"
        if not (String.IsNullOrWhiteSpace tip) then
            ToolTip.SetTip(host, tip)
            AutomationProperties.SetName(host, tip)
        host :> Border

    // ---------- 状态设置 ----------

    /// 统一点击与键盘激活绑定（迁自旧 Ui.onClick）：鼠标释放与键盘 Enter/Space。
    let onClick (host: Border) (action: unit -> unit) =
        match host with
        | :? InvokeBorder as semantic -> semantic.SetAction action
        | _ -> ()
        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && host.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())

    /// 禁用态（迁自旧 Ui.setEnabled）：不透明度与命中由主题的 `:disabled` 样式给，这里只切可操作性。
    let setEnabled (host: Border) (enabled: bool) =
        host.IsEnabled <- enabled
        host.IsHitTestVisible <- enabled
        host.Focusable <- enabled

    /// 只调整 hit surface，不改变内部 glyph 尺寸（迁自旧 Ui.setSquareTarget）。
    let setSquareTarget (host: Border) (size: float) =
        host.Width <- size
        host.Height <- size
        host.MinWidth <- size
        host.MinHeight <- size

    /// 保留控件的布局槽位，只切换可见反馈和可操作性（迁自旧 Ui.setReservedActionVisible）。
    let setReservedActionVisible (host: Border) (visible: bool) =
        host.IsVisible <- true
        host.Opacity <- if visible then 1.0 else 0.0
        host.IsHitTestVisible <- visible
        host.Focusable <- visible
        AutomationProperties.SetAccessibilityView(host, if visible then AccessibilityView.Default else AccessibilityView.Raw)

    // ---------- 文字按钮 ----------

    /// 文字按钮（迁自旧 Ui.button）。配色、内边距、最小高度、字重由主题的 `button-*` 类给。
    let button (tone: ButtonTone) (text: string) (action: unit -> unit) : Border =
        let cls, hover =
            match tone with
            | ButtonTone.Primary -> "button-primary", "hover-accent"
            | ButtonTone.Secondary -> "button-secondary", "hover-raised"
            | ButtonTone.Ghost -> "button-ghost", "hover-plain"
            | ButtonTone.Danger -> "button-danger", "hover-danger"
        let host =
            InvokeBorder(
                Cursor = handCursor,
                Focusable = true,
                VerticalAlignment = VerticalAlignment.Center,
                Child = TextBlock(Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center))
        host.Classes.Add "surface"
        host.Classes.Add cls
        host.Classes.Add hover
        onClick host action
        AutomationProperties.SetName(host, text)
        host :> Border

    // ---------- 输入 ----------

    /// 单行输入框（迁自旧 Ui.textField）：外壳负责描边与焦点环，TextBox 本身无边框。
    /// 焦点/校验的描边仲裁归组合者（FieldCheck 管校验描边；焦点环由 `focus-soft` 类给）。
    let textField (look: PrimitivesLook) (placeholder: string) : Border * TextBox =
        let box =
            TextBox(
                PlaceholderText = placeholder,
                BorderThickness = Thickness 0.0,
                Background = Brushes.Transparent,
                Foreground = look.FieldText,
                CaretBrush = look.FieldCaret,
                Padding = Thickness 0.0,
                VerticalContentAlignment = VerticalAlignment.Center)
        live look.FieldFontSize (fun v -> box.FontSize <- v)
        live look.FieldTextMinHeight (fun v -> box.MinHeight <- v)
        let shell = Border(Background = look.FieldSurface, BorderBrush = look.FieldBorder, Child = box)
        live look.FieldBorderWidth (fun v -> shell.BorderThickness <- Thickness v)
        live look.FieldRadius (fun v -> shell.CornerRadius <- v)
        live look.FieldPadding (fun v -> shell.Padding <- v)
        shell.Classes.Add "surface"
        shell.Classes.Add "focus-soft"
        shell.Classes.Add "hover-raised"
        shell, box

    // ---------- 开关 ----------

    /// 开关（迁自旧 Ui.toggle）。轨道、拨钮、开启态的底由主题的 `toggle-track` / `toggle-knob` / `on` 类给。
    /// 返回容器与「读取/设置/翻转」访问器。
    let toggle (initial: bool) (onChanged: bool -> unit) : Control * (unit -> bool) * (bool -> unit) * (unit -> unit) =
        let mutable value = initial
        let knob = Border(VerticalAlignment = VerticalAlignment.Center)
        knob.Classes.Add "toggle-knob"
        let track =
            ToggleBorder(
                Cursor = handCursor,
                Focusable = true,
                VerticalAlignment = VerticalAlignment.Center,
                Child = knob)
        track.Classes.Add "toggle-track"
        let render () =
            if value then track.Classes.Add "on" |> ignore else track.Classes.Remove "on" |> ignore
            knob.HorizontalAlignment <- if value then HorizontalAlignment.Right else HorizontalAlignment.Left
            track.SetAutomationValue value
        let flip () =
            value <- not value
            render ()
            onChanged value
        track.SetToggleAction flip
        track.PointerReleased.Add(fun e ->
            if e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                flip ())
        track.KeyDown.Add(fun e ->
            if e.Key = Key.Enter || e.Key = Key.Space then
                e.Handled <- true
                flip ())
        render ()
        track :> Control,
        (fun () -> value),
        (fun v ->
            value <- v
            render ()),
        flip

    // ---------- 字段组与设置行 ----------

    /// 表单字段的统一垂直结构（迁自旧 Ui.fieldGroup）：label → control → validation → hint。
    /// hint 与 validation 互斥：校验可见时 hint 收起。这里只组合 Avalonia 原生控件，不接管测量/校验状态。
    let fieldGroup
        (look: PrimitivesLook)
        (labelText: string)
        (hint: string)
        (hintMargin: Thickness)
        (control: Control)
        (validation: Control option)
        : Control =
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        column.Children.Add(fieldLabel look labelText)
        column.Children.Add control
        validation |> Option.iter column.Children.Add
        if not (String.IsNullOrWhiteSpace hint) then
            let note = caption look hint
            note.Margin <- hintMargin
            match validation with
            | Some error ->
                note.IsVisible <- not error.IsVisible
                error.PropertyChanged.Add(fun args ->
                    if args.Property = Visual.IsVisibleProperty then note.IsVisible <- not error.IsVisible)
            | None -> ()
            column.Children.Add note
        column :> Control

    /// 设置行内容（迁自旧 Ui.settingsRowContent）：左文（label + caption 竖列）右控件（Dock 右停靠、末子填充）。
    let settingsRowContent (look: PrimitivesLook) (spacing: float) (titleText: string) (hint: string) (right: Control) : DockPanel =
        let column = vstack spacing [ label look titleText :> Control; caption look hint :> Control ]
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(right, Dock.Right)
        dock.Children.Add right
        dock.Children.Add column
        dock

    /// 自定义右侧控件的设置行外壳（迁自旧 Ui.settingsRowShell）：与开关行同构，但不带交互层。
    let settingsRowShell
        (look: PrimitivesLook)
        (padding: Thickness)
        (minHeight: float)
        (spacing: float)
        (titleText: string)
        (hint: string)
        (right: Control)
        : Border =
        Border(Padding = padding, MinHeight = minHeight, Child = settingsRowContent look spacing titleText hint right)

    /// 设置行开关（迁自旧 Ui.switchRow）：行容器统一代理键盘，一行只有一个 Tab 停靠点。
    /// 内层开关不进 Tab 序，Enter/Space 只走 onClick 一次。
    let switchRow
        (look: PrimitivesLook)
        (padding: Thickness)
        (radius: float)
        (minHeight: float)
        (spacing: float)
        (titleText: string)
        (hint: string)
        (initial: bool)
        (onChanged: bool -> unit)
        : Control * (unit -> bool) * (bool -> unit) =
        let toggleControl, read, write, flip = toggle initial onChanged
        toggleControl.Focusable <- false
        AutomationProperties.SetName(toggleControl, titleText)
        AutomationProperties.SetHelpText(toggleControl, hint)
        let dock = settingsRowContent look spacing titleText hint toggleControl
        let row =
            InvokeBorder(
                Padding = padding,
                CornerRadius = CornerRadius radius,
                Cursor = handCursor,
                Focusable = true,
                MinHeight = minHeight,
                Child = dock)
        row.Classes.Add "surface"
        row.Classes.Add "hover-plain"
        AutomationProperties.SetName(row, titleText)
        AutomationProperties.SetHelpText(row, hint)
        AutomationProperties.SetControlTypeOverride(row, Nullable AutomationControlType.CheckBox)
        let toggleAndRefresh () =
            flip ()
            AutomationProperties.SetItemStatus(row, if read () then "开启" else "关闭")
        AutomationProperties.SetItemStatus(row, if initial then "开启" else "关闭")
        onClick row toggleAndRefresh
        let syncWrite v =
            write v
            AutomationProperties.SetItemStatus(row, if v then "开启" else "关闭")
        row :> Control, read, syncWrite

    // ---------- 空态 ----------

    /// 空态三件套（迁自旧 Ui.emptyState / emptyStateWith）：品牌 logo 视觉锚点 + 标题 + 说明
    /// （+ 可选的加载骨架与主操作按钮），统一包在分组卡内。
    /// prominentTitle 为 true 时用 title 档 + 宽字距（整屏主空态）；几何（间距、内边距、圆角）由调用方给。
    let emptyState
        (look: PrimitivesLook)
        (prominentTitle: bool)
        (logo: Control)
        (titleText: string option)
        (hint: string)
        (extra: Control option)
        (action: Border option)
        (spacing: float)
        (padding: Thickness)
        (radius: float)
        : Control =
        let titleOf text =
            if prominentTitle then
                let block = TextBlock(Text = text, Foreground = look.TitleForeground, HorizontalAlignment = HorizontalAlignment.Center)
                live look.TitleFontSize (fun v -> block.FontSize <- v)
                live look.EmphasisTracking (fun v -> block.LetterSpacing <- v)
                block :> Control
            else
                label look text :> Control
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = spacing)
        column.Children.Add logo
        match titleText with
        | Some text -> column.Children.Add(titleOf text)
        | None -> ()
        if not (String.IsNullOrWhiteSpace hint) then column.Children.Add(caption look hint)
        match extra with
        | Some control -> column.Children.Add control
        | None -> ()
        match action with
        | Some actionButton -> column.Children.Add actionButton
        | None -> ()
        groupingCard padding column radius :> Control
