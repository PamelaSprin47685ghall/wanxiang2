namespace Wanxiang2.ChatHeader

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 用到的笔刷
[<RequireQualifiedAccess>]
type HeaderInk =
    | Text
    | TextFaint
    | TextMuted
    | Accent
    | AccentFaint
    | Hover
    | BorderSoft
    | Border
    | Line
    | Surface
    | Hairline

/// 用到的活度量
[<RequireQualifiedAccess>]
type HeaderGauge =
    /// 图标按钮边长；标题与编辑框同高
    | IconButton
    /// 窄屏触控目标
    | CompactTarget
    | BarHeight
    /// 最窄档水平留白（旧 Tokens.shellInset）
    | ShellInset
    | SpaceXs
    | SpaceMd
    | SpaceXl
    | RadiusSm
    | RadiusPill
    | BorderWidth
    /// 聚焦外环粗细（旧 Spacing.Stroke.thick）
    | FocusSpread
    | FontTitle
    | FontMicro
    | FontCaption
    | ChipPadX
    | ChipPadY
    | ChipDot
    /// 标题最小宽（旧 ControlMetrics.emptyStateTitleMinWidth）
    | TitleMinWidth
    | IconGlyph

/// 用到的图标
[<RequireQualifiedAccess>]
type HeaderGlyph =
    | Sidebar
    | More
    | Fork

/// 外观
type ChatHeaderLook =
    { Ink: HeaderInk -> IBrush
      Gauge: HeaderGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      Glyph: HeaderGlyph -> IBrush -> Control
      /// 图标按钮样式（旧 Ui.iconButton）
      DressIcon: Border -> unit
      /// 平面悬停表面（旧 Interaction.HoverVariant.plain）
      DressPlain: Border -> unit
      /// 描边表面（旧 Interaction.HoverVariant.bordered）
      DressField: Border -> unit }

/// 面包屑呈现事实（宿主由 BreadcrumbAction 折出）
type CrumbView =
    { Text: string
      Description: string
      /// 父对话还在：可点
      Clickable: bool }

/// 聊天顶栏（旧 ChatView 的 headerBar + breadcrumbBar）：侧栏开关、可改名标题、生成中状态点、更多按钮、分叉面包屑。
/// 菜单内容与去向归宿主；本库只报「按了哪里」。
type ChatHeader(look: ChatHeaderLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let now = Follow.now
    let renamed = Event<string>()
    let morePressed = Event<Control>()
    let sidebarPressed = Event<unit>()
    let crumbPressed = Event<unit>()

    let iconButton glyph (tip: string) =
        let b = Spot(Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
                     VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
        look.DressIcon b
        let g = look.Glyph glyph (ink HeaderInk.TextMuted)
        g.HorizontalAlignment <- HorizontalAlignment.Center
        g.VerticalAlignment <- VerticalAlignment.Center
        b.Child <- g
        ToolTip.SetTip(b, tip)
        AutomationProperties.SetName(b, tip)
        b

    let titleText =
        TextBlock(Text = "", FontWeight = FontWeight.Medium, Foreground = ink HeaderInk.Text,
                  VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                  HorizontalAlignment = HorizontalAlignment.Stretch)
    let titleAction =
        Spot(Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center,
             HorizontalAlignment = HorizontalAlignment.Stretch, Focusable = false, Child = titleText)
    let editBox =
        TextBox(PlaceholderText = "对话标题", BorderThickness = Thickness 0.0, Background = Brushes.Transparent,
                Foreground = ink HeaderInk.Text, CaretBrush = ink HeaderInk.Accent, FontWeight = FontWeight.Medium,
                Padding = Thickness 0.0, MinHeight = 0.0, Margin = Thickness 0.0,
                VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch)
    let editShell =
        Border(Background = ink HeaderInk.Surface, BorderBrush = ink HeaderInk.Border, VerticalAlignment = VerticalAlignment.Center,
               HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false, Child = editBox)
    let titleHost = Grid(MinWidth = 0.0, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center)

    let caption = TextBlock(Text = "生成中", Foreground = ink HeaderInk.Accent, VerticalAlignment = VerticalAlignment.Center)
    let dot = Border(Background = ink HeaderInk.Accent, VerticalAlignment = VerticalAlignment.Center)
    let chipRow = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let generatingChip =
        Border(Background = ink HeaderInk.AccentFaint, VerticalAlignment = VerticalAlignment.Center,
               Opacity = 0.0, IsHitTestVisible = false, Child = chipRow)

    let sidebar = iconButton HeaderGlyph.Sidebar "切换侧边栏（Ctrl / ⌘ + B）"
    let more = iconButton HeaderGlyph.More "对话操作"

    let crumbText = TextBlock(Foreground = ink HeaderInk.TextMuted, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center)
    let crumbAction = Spot(Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, Focusable = false)
    let crumbBar = Border(IsVisible = false, Child = crumbAction)

    let leftGroup = Grid(VerticalAlignment = VerticalAlignment.Center)
    let rightGroup = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let dock = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
    let top = StackPanel()
    let bar = Border(BorderBrush = ink HeaderInk.Hairline, IsVisible = false, Child = top)

    let mutable editable = false
    let mutable editing = false
    let mutable generating = false
    let mutable compact = false
    let mutable hasConversation = false
    let mutable crumbClickable = false

    let visual () =
        if not editable then
            titleAction.Background <- Brushes.Transparent
            titleAction.BorderBrush <- Brushes.Transparent
            titleAction.BoxShadow <- BoxShadows()
            titleText.Foreground <- ink HeaderInk.TextFaint
        elif titleAction.IsFocused then
            titleAction.Background <- Brushes.Transparent
            titleAction.BorderBrush <- ink HeaderInk.Accent
            let c = match ink HeaderInk.Accent with :? ISolidColorBrush as s -> s.Color | _ -> Colors.Transparent
            titleAction.BoxShadow <- BoxShadows(BoxShadow(Spread = now (gauge HeaderGauge.FocusSpread), Color = c))
            titleText.Foreground <- ink HeaderInk.Accent
        elif titleAction.IsPointerOver then
            // 悬停只换底色与描边，不向外扩张
            titleAction.Background <- ink HeaderInk.Hover
            titleAction.BorderBrush <- ink HeaderInk.BorderSoft
            titleAction.BoxShadow <- BoxShadows()
            titleText.Foreground <- ink HeaderInk.Text
        else
            titleAction.Background <- Brushes.Transparent
            titleAction.BorderBrush <- Brushes.Transparent
            titleAction.BoxShadow <- BoxShadows()
            titleText.Foreground <- ink HeaderInk.Text

    /// 标题铬层统一入口：可见文本与读屏名不脱节
    let chrome (text: string) =
        ToolTip.SetTip(titleAction, text)
        ToolTip.SetTip(titleText, text)
        AutomationProperties.SetName(titleText, text)
        AutomationProperties.SetName(titleAction, if editable then sprintf "重命名对话：%s" text else text)
        visual ()

    let restoreFocus () =
        if titleAction.IsVisible && titleAction.Focusable then
            titleAction.Focus(NavigationMethod.Directional) |> ignore
            Dispatcher.UIThread.Post(
                (fun () ->
                    if titleAction.IsVisible && titleAction.Focusable && not titleAction.IsFocused then
                        titleAction.Focus(NavigationMethod.Directional) |> ignore),
                DispatcherPriority.Input)

    let commit (refocus: bool) =
        if editing || editShell.IsVisible then
            editing <- false
            let next = if isNull editBox.Text then "" else editBox.Text.Trim()
            titleAction.IsVisible <- true
            editShell.IsVisible <- false
            let effective =
                if not (String.IsNullOrWhiteSpace next) && next <> titleText.Text then
                    titleText.Text <- next
                    editBox.Text <- next
                    renamed.Trigger next
                    next
                else
                    editBox.Text <- titleText.Text
                    titleText.Text
            chrome effective
            if refocus then restoreFocus ()

    let cancel () =
        if editing || editShell.IsVisible then
            editing <- false
            editBox.Text <- titleText.Text
            titleAction.IsVisible <- true
            editShell.IsVisible <- false
            chrome titleText.Text
            restoreFocus ()

    let beginEdit () =
        if editable && titleAction.Focusable then
            editing <- true
            editBox.Text <- titleText.Text
            titleAction.IsVisible <- false
            editShell.IsVisible <- true
            editBox.Focus() |> ignore
            editBox.SelectAll()
            Dispatcher.UIThread.Post(
                (fun () ->
                    if editShell.IsVisible then
                        editBox.Focus() |> ignore
                        editBox.SelectAll()),
                DispatcherPriority.Input)

    /// 生成中状态点：桌面保留槽位只切透明度；窄屏整体不占布局
    let syncChip () =
        if compact then
            generatingChip.IsVisible <- false
            generatingChip.Opacity <- 0.0
        else
            generatingChip.IsVisible <- true
            generatingChip.Opacity <- if generating then 1.0 else 0.0
            generatingChip.IsHitTestVisible <- generating
            generatingChip.Focusable <- generating
            AutomationProperties.SetAccessibilityView(generatingChip, if generating then AccessibilityView.Default else AccessibilityView.Raw)

    let square () =
        let s = now (gauge (if compact then HeaderGauge.CompactTarget else HeaderGauge.IconButton))
        for b in [ sidebar; more ] do
            b.Width <- s
            b.Height <- s
            b.MinWidth <- s
            b.MinHeight <- s

    let titleGeometry (h: float) =
        for c in [ titleHost :> Layoutable; titleAction; editShell ] do
            c.Margin <- Thickness 0.0
        titleHost.Height <- h
        titleHost.MinHeight <- h
        titleHost.MaxHeight <- h
        for b in [ titleAction :> Border; editShell ] do
            b.Height <- h
            b.MinHeight <- h
            b.MaxHeight <- h
        editBox.Height <- h - 2.0
        editBox.MaxHeight <- h - 2.0

    do
        Follow.live bar (gauge HeaderGauge.IconButton) (fun h -> titleGeometry h; square ())
        Follow.live bar (gauge HeaderGauge.CompactTarget) (fun _ -> square ())
        Follow.live titleText (gauge HeaderGauge.FontTitle) (fun s -> titleText.FontSize <- s; editBox.FontSize <- s)
        Follow.live titleText look.BodyFont (fun f -> titleText.FontFamily <- f; editBox.FontFamily <- f; caption.FontFamily <- f; crumbText.FontFamily <- f)
        Follow.live bar (gauge HeaderGauge.BorderWidth) (fun w ->
            titleAction.BorderThickness <- Thickness w
            editShell.BorderThickness <- Thickness w
            bar.BorderThickness <- Thickness(0.0, 0.0, 0.0, w))
        Follow.live bar (gauge HeaderGauge.RadiusSm) (fun r ->
            titleAction.CornerRadius <- CornerRadius r
            editShell.CornerRadius <- CornerRadius r
            crumbAction.CornerRadius <- CornerRadius r)
        Follow.live bar (gauge HeaderGauge.TitleMinWidth) (fun w -> titleAction.MinWidth <- w; editShell.MinWidth <- w)
        Follow.live bar (gauge HeaderGauge.SpaceXl) (fun s -> titleAction.Padding <- Thickness(s, 0.0); editShell.Padding <- Thickness(s, 0.0))
        Follow.live bar (gauge HeaderGauge.RadiusPill) (fun r -> generatingChip.CornerRadius <- CornerRadius r; dot.CornerRadius <- CornerRadius r)
        Follow.live2 bar (gauge HeaderGauge.ChipPadX) (gauge HeaderGauge.ChipPadY) (fun x y -> generatingChip.Padding <- Thickness(x, y))
        Follow.live bar (gauge HeaderGauge.ChipDot) (fun d -> dot.Width <- d; dot.Height <- d)
        Follow.live caption (gauge HeaderGauge.FontMicro) (fun s -> caption.FontSize <- s)
        Follow.live crumbText (gauge HeaderGauge.FontCaption) (fun s -> crumbText.FontSize <- s)
        Follow.live bar (gauge HeaderGauge.SpaceMd) (fun s ->
            chipRow.Spacing <- s
            leftGroup.ColumnSpacing <- s)
        Follow.live bar (gauge HeaderGauge.SpaceXs) (fun s -> rightGroup.Spacing <- s)
        Follow.live2 bar (gauge HeaderGauge.SpaceMd) (gauge HeaderGauge.SpaceXs) (fun m x -> crumbAction.Padding <- Thickness(m, x))
        Follow.live2 bar (gauge HeaderGauge.ShellInset) (gauge HeaderGauge.SpaceXs) (fun i x -> crumbBar.Padding <- Thickness(i, 0.0, i, x))
        Follow.live bar (gauge HeaderGauge.BarHeight) (fun h -> bar.Height <- h)
        Follow.live bar (gauge HeaderGauge.ShellInset) (fun i -> bar.Padding <- Thickness(i, 0.0, i, 0.0))

        chipRow.Children.Add dot
        chipRow.Children.Add caption
        ToolTip.SetTip(generatingChip, "正在生成回答")
        AutomationProperties.SetName(generatingChip, "正在生成回答")
        titleHost.Children.Add titleAction
        titleHost.Children.Add editShell

        look.DressPlain titleAction
        look.DressField editShell
        look.DressPlain crumbAction
        titleAction.OnInvoke beginEdit
        titleAction.KeyDown.Add(fun e ->
            if (e.Key = Key.F2 || e.Key = Key.Enter) && editable then
                e.Handled <- true
                beginEdit ())
        titleAction.GotFocus.Add(fun _ -> visual ())
        titleAction.LostFocus.Add(fun _ -> visual ())
        titleAction.PointerEntered.Add(fun _ -> visual ())
        titleAction.PointerExited.Add(fun _ -> visual ())
        editBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                commit true
            elif e.Key = Key.Escape then
                e.Handled <- true
                cancel ())
        editBox.GotFocus.Add(fun _ -> editShell.BorderBrush <- ink HeaderInk.Accent)
        editBox.LostFocus.Add(fun _ ->
            editShell.BorderBrush <- ink HeaderInk.Border
            if editing then
                let top = TopLevel.GetTopLevel bar
                let next = if isNull top || isNull top.FocusManager then null else top.FocusManager.GetFocusedElement()
                // 焦点落空（或落回窗口 / 顶栏自己）时提交后把焦点还给标题
                commit (isNull next || obj.ReferenceEquals(next, top) || obj.ReferenceEquals(next, bar)))
        editShell.PointerEntered.Add(fun _ -> if not editBox.IsFocused then editShell.BorderBrush <- ink HeaderInk.Line)
        editShell.PointerExited.Add(fun _ -> if not editBox.IsFocused then editShell.BorderBrush <- ink HeaderInk.Border)

        sidebar.OnInvoke(fun () -> sidebarPressed.Trigger())
        more.OnInvoke(fun () -> morePressed.Trigger(more :> Control))
        more.IsVisible <- false
        crumbAction.OnInvoke(fun () -> if crumbClickable then crumbPressed.Trigger())

        // 左组用 Grid：标题放进唯一可收缩的 Star 列，长标题真正截断而不挤右侧
        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(sidebar, 0)
        Grid.SetColumn(titleHost, 1)
        Grid.SetColumn(generatingChip, 2)
        leftGroup.Children.Add sidebar
        leftGroup.Children.Add titleHost
        leftGroup.Children.Add generatingChip
        rightGroup.Children.Add more
        DockPanel.SetDock(rightGroup, Dock.Right)
        dock.Children.Add rightGroup
        dock.Children.Add leftGroup

        let crumbIcon = look.Glyph HeaderGlyph.Fork (ink HeaderInk.TextMuted)
        Follow.live crumbIcon (gauge HeaderGauge.IconGlyph) (fun s -> crumbIcon.Width <- s; crumbIcon.Height <- s)
        crumbIcon.VerticalAlignment <- VerticalAlignment.Center
        let crumbRow = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live crumbRow (gauge HeaderGauge.SpaceMd) (fun s -> crumbRow.Spacing <- s)
        crumbRow.Children.Add crumbIcon
        crumbRow.Children.Add crumbText
        crumbAction.Child <- crumbRow

        top.Children.Add dock
        top.Children.Add crumbBar
        visual ()

    /// 放进布局的根（顶栏 + 面包屑）
    member _.Host: Border = bar
    member _.TitleAction: Border = titleAction
    member _.TitleEditor: TextBox = editBox
    member _.MoreButton: Border = more
    member _.SidebarButton: Border = sidebar
    member _.GeneratingChip: Border = generatingChip
    member _.CrumbAction: Border = crumbAction
    member _.Title = titleText.Text
    member _.IsEditing = editing
    member _.CrumbText = if crumbBar.IsVisible then crumbText.Text else ""

    /// 用户改了标题（去掉首尾空白、非空且有变化）
    member _.Renamed = renamed.Publish
    /// 按了「对话操作」（带锚点）；菜单内容归宿主
    member _.MorePressed = morePressed.Publish
    member _.SidebarPressed = sidebarPressed.Publish
    /// 按了可点的面包屑
    member _.CrumbPressed = crumbPressed.Publish

    /// 主机标题在改名途中到达时不丢草稿：隐藏标签跟进最新值，提交仍以草稿为准
    member _.SetTitle(text: string, canEdit: bool) =
        editable <- canEdit
        let still = canEdit && editing && editShell.IsVisible
        editing <- still
        editShell.IsVisible <- still
        titleAction.IsVisible <- not still
        titleText.Text <- text
        if not still then editBox.Text <- text
        titleAction.Focusable <- canEdit
        titleAction.Cursor <- if canEdit then new Cursor(StandardCursorType.Hand) else null
        chrome text

    member _.BeginRename() = beginEdit ()
    member _.CommitRename(refocus: bool) = commit refocus
    member _.CancelRename() = cancel ()

    member _.SetGenerating(value: bool, statusText: string) =
        generating <- value
        caption.Text <- if String.IsNullOrWhiteSpace statusText then "生成中" else statusText
        syncChip ()

    /// 有对话时才显示「对话操作」；顶栏本身从此常驻
    member _.SetConversationChrome(value: bool) =
        hasConversation <- value
        more.IsVisible <- value
        bar.IsVisible <- true

    member _.SetCompact(value: bool) =
        compact <- value
        square ()
        more.IsVisible <- hasConversation
        syncChip ()

    /// 水平留白（宿主与消息列同源取档）
    member _.SetInset(inset: float) = bar.Padding <- Thickness(inset, 0.0, inset, 0.0)

    /// 面包屑；None 时整条隐藏
    member _.SetCrumb(view: CrumbView option) =
        match view with
        | None ->
            crumbClickable <- false
            crumbBar.IsVisible <- false
        | Some c ->
            crumbClickable <- c.Clickable
            crumbText.Text <- c.Text
            crumbAction.Focusable <- c.Clickable
            crumbAction.Cursor <- if c.Clickable then new Cursor(StandardCursorType.Hand) else null
            ToolTip.SetTip(crumbAction, c.Description)
            AutomationProperties.SetName(crumbAction, c.Text)
            AutomationProperties.SetHelpText(crumbAction, c.Description)
            // 可点时读屏报按钮（旧 AutomationProperties.SetRole Button），父对话没了报纯文本
            if c.Clickable then AutomationProperties.SetControlTypeOverride(crumbAction, Nullable AutomationControlType.Button)
            else AutomationProperties.SetControlTypeOverride(crumbAction, Nullable AutomationControlType.Text)
            crumbBar.IsVisible <- not (String.IsNullOrEmpty c.Text)
