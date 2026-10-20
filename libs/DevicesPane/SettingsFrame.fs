namespace Wanxiang2.SettingsFrame

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState
open Wanxiang2.DevicesPane
open Avalonia.Threading

/// 一个分区入口
type SettingsNavItem =
    { Key: string
      Label: string
      /// 图标名（Glyph 槽）
      Glyph: string }

/// 设置页外框（迁自旧 Views/SettingsView.fs 的界面部分）：头部「设置 / 返回对话」、分组的分区导航（读屏 Tab / TabItem、
/// 上下左右与 Home / End 循环移动）、范围说明、内容区（滚动、最宽封顶）、窄屏时导航转为顶部横条。
/// 分区内容、缓存与滚动记忆（SectionMemory）、范围文字由组合者接。
type SettingsFrame(groups: (string * SettingsNavItem list) list, ?glyphOf: string -> IBrush -> Control) as this =
    inherit Border()
    let selectRequested = Event<string>()
    let close = Event<unit>()
    let all = groups |> List.collect snd
    let navPanel = StackPanel(Orientation = Orientation.Vertical)
    let groupLabels = ResizeArray<TextBlock>()
    let scopeCaption = Ui.sectionLabel "主机"
    let targetCaption = Ui.caption "尚未连接主机"
    let contentHost = ContentControl()
    let buttons = Collections.Generic.Dictionary<string, Button>()
    let captions = Collections.Generic.Dictionary<string, TextBlock>()
    let glyphs = Collections.Generic.Dictionary<string, Control>()
    let accents = Collections.Generic.Dictionary<string, Border>()
    let mutable current = (List.head all).Key
    let mutable compact: bool option = None
    let mutable subtle = 0.6

    let navScroll = ScrollViewer(Content = navPanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden)
    let nav = Border(Child = navScroll)
    do nav.Bind(Border.BackgroundProperty, Dyn.res "Palette.Rail") |> ignore
    do nav.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
    let contentFrame = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
    let content = ScrollViewer(Content = contentFrame, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalContentAlignment = HorizontalAlignment.Stretch)

    let refreshStates () =
        for KeyValue(key, button) in buttons do
            let selected = key = current
            // 选中底是本地值；未选中时清掉，让主题的悬停样式生效
            if selected then button.Bind(Button.BackgroundProperty, Dyn.res "Palette.Selected") |> ignore else button.ClearValue Button.BackgroundProperty
            accents[key].IsVisible <- selected
            let label = captions[key].Text
            AutomationProperties.SetItemStatus(button, (if selected then "当前分区" else ""))
            AutomationProperties.SetHelpText(button, (if selected then label + "，当前分区" else label))
            if selected then captions[key].Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore else captions[key].Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
            captions[key].FontWeight <- (if selected then FontWeight.Bold else FontWeight.Medium)
            glyphs[key].Opacity <- (if selected then 1.0 else subtle)

    let focusKey (key: string) =
        match buttons.TryGetValue key with
        | true, b -> b.Focus(NavigationMethod.Directional) |> ignore
        | _ -> ()

    /// 键盘移动：先请组合者切换（组合者回调 Select），再把焦点送到目标键
    let move (target: string) =
        selectRequested.Trigger target
        focusKey target

    let step (dir: int) =
        let idx = all |> List.tryFindIndex (fun i -> i.Key = current) |> Option.defaultValue 0
        let n = all.Length
        move all[((idx + dir) % n + n) % n].Key

    let navButton (item: SettingsNavItem) =
        let glyph =
            match glyphOf with
            | Some g -> g item.Glyph (SolidColorBrush(Colors.Gray) :> IBrush)
            | None ->
                let t = TextBlock(Text = "[" + item.Glyph + "]")
                t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                t :> Control
        glyph.VerticalAlignment <- VerticalAlignment.Center
        let caption = Ui.smallText item.Label
        caption.FontWeight <- FontWeight.Medium
        caption.VerticalAlignment <- VerticalAlignment.Center
        // 选中左缘强调条：独立元素叠画，不进布局流、不抢焦点
        let accent = Border(HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false, Focusable = false, IsVisible = false, Width = 2.0)
        accent.Bind(Border.BackgroundProperty, Dyn.res "Palette.Accent") |> ignore
        let grid = Grid()
        grid.Children.Add accent
        let stack = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        stack.Children.Add glyph
        stack.Children.Add caption
        grid.Children.Add stack
        let host = Button(Content = grid, MinHeight = 38.0, Padding = Thickness(16.0, 12.0), CornerRadius = CornerRadius 8.0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left)
        host.Classes.Add "surface"
        host.Classes.Add "menu-item"
        host.Classes.Add "hover-plain"
        host.Click.Add(fun _ -> selectRequested.Trigger item.Key)
        AutomationProperties.SetName(host, item.Label)
        host.Tag <- item.Key
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.TabItem)
        host.KeyDown.Add(fun e ->
            match e.Key with
            | Key.Up | Key.Left ->
                e.Handled <- true
                step -1
            | Key.Down | Key.Right ->
                e.Handled <- true
                step 1
            | Key.Home ->
                e.Handled <- true
                move all.Head.Key
            | Key.End ->
                e.Handled <- true
                move (List.last all).Key
            | _ -> ())
        buttons[item.Key] <- host
        captions[item.Key] <- caption
        glyphs[item.Key] <- glyph
        accents[item.Key] <- accent
        host

    let mutable spaces = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
    let mutable navWidth = 196.0
    let mutable barHeight = 48.0
    let mutable border = 1.0

    let applyLayout () =
        let xs, md, xl, s2, _, _ = spaces
        match compact with
        | Some true ->
            DockPanel.SetDock(nav, Dock.Top)
            nav.Width <- Double.NaN
            nav.Height <- barHeight
            nav.Padding <- Thickness(xl, md)
            navPanel.Spacing <- md
            nav.BorderThickness <- Thickness(0.0, 0.0, 0.0, border)
            navPanel.Orientation <- Orientation.Horizontal
            navScroll.HorizontalScrollBarVisibility <- ScrollBarVisibility.Auto
        | _ ->
            DockPanel.SetDock(nav, Dock.Left)
            nav.Width <- navWidth
            nav.Height <- Double.NaN
            nav.Padding <- Thickness(xl, s2)
            navPanel.Spacing <- xs
            nav.BorderThickness <- Thickness(0.0, 0.0, border, 0.0)
            navPanel.Orientation <- Orientation.Vertical
            navScroll.HorizontalScrollBarVisibility <- ScrollBarVisibility.Hidden
        for l in groupLabels do
            l.IsVisible <- (compact <> Some true)
            l.Margin <- Thickness(xl, s2, xl, md)

    let mutable s8 = 0.0
    let applyPadding () =
        let _, _, _, s2, s4, s6 = spaces
        contentFrame.Padding <- (if compact = Some true then Thickness(s2, s2, s2, s6) else Thickness(s6, s4, s6, s8))

    let mutable breakpoint = 720.0
    let applyResponsive () =
        let width = this.Bounds.Width
        if width > 0.0 then
            let next = width < breakpoint
            if compact <> Some next then
                let previous = content.Offset
                compact <- Some next
                applyLayout ()
                applyPadding ()
                let maxY = max 0.0 (content.Extent.Height - content.Viewport.Height)
                content.Offset <- Vector(previous.X, min previous.Y maxY)

    do
        this.Bind(Border.BackgroundProperty, Dyn.res "Palette.Canvas") |> ignore
        let closeButton = Ui.button "ghost" "返回对话" close.Trigger
        AutomationProperties.SetHelpText(closeButton, "返回对话（Esc），未保存的草稿留在当前窗口")
        let title = TextBlock(Text = "设置", FontSize = 28.0)
        title.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        title.FontWeight <- FontWeight.Medium
        title.VerticalAlignment <- VerticalAlignment.Center
        let dock = DockPanel(LastChildFill = false, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(title, Dock.Left)
        DockPanel.SetDock(closeButton, Dock.Right)
        dock.Children.Add title
        dock.Children.Add closeButton
        let header = Border(Child = dock, Height = 48.0, Padding = Thickness(16.0, 0.0, 16.0, 0.0), BorderThickness = Thickness(0.0, 0.0, 0.0, 1.0))
        header.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
        for group, items in groups do
            let label = Ui.sectionLabel group
            groupLabels.Add label
            navPanel.Children.Add label
            for item in items do navPanel.Children.Add(navButton item)
        AutomationProperties.SetName(nav, "设置分区导航")
        AutomationProperties.SetControlTypeOverride(nav, Nullable AutomationControlType.Tab)
        spaces <- (4.0, 12.0, 16.0, 20.0, 32.0, 40.0)
        s8 <- 64.0
        navWidth <- 196.0
        barHeight <- 48.0
        border <- 1.0
        let topCap = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        topCap.Children.Add scopeCaption
        topCap.Children.Add targetCaption
        let contentStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        contentStack.Children.Add topCap
        contentStack.Children.Add contentHost
        contentFrame.Child <- contentStack
        contentFrame.MaxWidth <- 760.0
        let split = DockPanel()
        DockPanel.SetDock(nav, Dock.Left)
        split.Children.Add nav
        split.Children.Add content
        let layout = DockPanel()
        DockPanel.SetDock(header, Dock.Top)
        layout.Children.Add header
        layout.Children.Add split
        this.Child <- layout
        this.PropertyChanged.Add(fun a -> if a.Property = Visual.BoundsProperty then applyResponsive ())
        applyLayout ()
        applyPadding ()
        refreshStates ()

    override _.StyleKeyOverride = typeof<Border>

    member _.Current = current
    member _.IsCompact = (compact = Some true)

    /// 标出选中的分区（选中底、左缘强调条、字重、图标不透明度、读屏状态），并把它滚进视野
    member _.Select(key: string) =
        current <- key
        refreshStates ()
        match buttons.TryGetValue key with
        | true, b -> b.BringIntoView()
        | _ -> ()

    /// 范围说明两行
    member _.ApplyCaptions(scope: string, target: string) =
        scopeCaption.Text <- scope
        targetCaption.Text <- target

    /// 换内容；restoreY 有值时滚到该位置
    member _.ApplyContent(control: Control, restoreY: float option) =
        contentHost.Content <- control
        restoreY |> Option.iter (fun y -> content.Offset <- Vector(0.0, y))

    /// 内容区当前纵向滚动位置
    member _.ContentOffset = content.Offset.Y
    member _.ScrollTo(y: float) = content.Offset <- Vector(0.0, y)

    /// 焦点落到某个分区键（打开设置时的焦点归宿）
    member _.FocusNav(key: string) = focusKey key

    /// 分区键被点、或方向键 / Home / End 移到它（组合者切换后回调 Select）
    member _.SelectRequested = selectRequested.Publish
    /// 「返回对话」
    member _.CloseClicked = close.Publish
