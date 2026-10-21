namespace Wanxiang2.ChatHeader

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 聊天顶栏（原生 Button / TextBox / Grid / StackPanel 实现）：
/// 单入口幂等 Render(ChatHeaderVS)，对外上报意图事件；
/// 废除 Look 记录、已取消逐属性活值订阅与散落的 Dispatcher.UIThread.Post。
type ChatHeader() =
    let renamed = Event<string>()
    let renameStarted = Event<unit>()
    let renameCancelled = Event<unit>()
    let morePressed = Event<Control>()
    let sidebarPressed = Event<unit>()
    let crumbPressed = Event<unit>()

    // 原生 Button 替换 Spot/Tap，挂 icon-button 与 ghost 类
    let sidebarButton =
        let b =
            Button(Focusable = true, Cursor = new Cursor(StandardCursorType.Hand), Width = 30.0, Height = 30.0)

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.VerticalAlignment <- VerticalAlignment.Center
        b.HorizontalAlignment <- HorizontalAlignment.Center
        // 侧栏简易图标矢量
        let icon =
            Path(
                Data = Geometry.Parse "M 3 3 L 15 3 L 15 15 L 3 15 Z M 7 3 L 7 15",
                StrokeThickness = 1.2,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 14.0,
                Height = 14.0
            )

        icon.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        b.Content <- icon
        ToolTip.SetTip(b, "切换侧边栏（Ctrl / ⌘ + B）")
        AutomationProperties.SetName(b, "切换侧边栏（Ctrl / ⌘ + B）")
        b.Click.Add(fun _ -> sidebarPressed.Trigger())
        b

    let moreButton =
        let b =
            Button(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                IsVisible = false,
                Width = 30.0,
                Height = 30.0
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.VerticalAlignment <- VerticalAlignment.Center
        b.HorizontalAlignment <- HorizontalAlignment.Center

        let icon =
            Path(
                Data = Geometry.Parse "M 4 8 A 1 1 0 1 1 4 8.01 M 8 8 A 1 1 0 1 1 8 8.01 M 12 8 A 1 1 0 1 1 12 8.01",
                StrokeThickness = 2.0,
                StrokeLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 14.0,
                Height = 14.0
            )

        icon.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        b.Content <- icon
        ToolTip.SetTip(b, "对话操作")
        AutomationProperties.SetName(b, "对话操作")
        b.Click.Add(fun _ -> morePressed.Trigger(b :> Control))
        b

    let titleText =
        TextBlock(
            Text = "",
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Stretch
        )

    do titleText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

    let titleAction =
        let b =
            Button(
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            )

        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b.Content <- titleText
        b

    let editBox =
        TextBox(
            PlaceholderText = "对话标题",
            FontWeight = FontWeight.Medium,
            Padding = Thickness(4.0, 2.0),
            MinHeight = 24.0,
            Margin = Thickness 0.0,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        )

    let editShell =
        Border(
            CornerRadius = CornerRadius 4.0,
            BorderThickness = Thickness 1.0,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
            Child = editBox
        )

    do
        editShell.Bind(Border.BackgroundProperty, Dyn.res "Palette.Surface") |> ignore
        editShell.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Border") |> ignore

    let titleHost =
        Grid(
            MinWidth = 0.0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        )

    let caption = TextBlock(Text = "生成中", VerticalAlignment = VerticalAlignment.Center)
    do caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Accent") |> ignore

    let dot =
        Border(Width = 6.0, Height = 6.0, CornerRadius = CornerRadius 3.0, VerticalAlignment = VerticalAlignment.Center)

    do dot.Bind(Border.BackgroundProperty, Dyn.res "Palette.Accent") |> ignore

    let chipRow =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 6.0, VerticalAlignment = VerticalAlignment.Center)

    let generatingChip =
        Border(
            CornerRadius = CornerRadius 10.0,
            Padding = Thickness(8.0, 2.0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.0,
            IsHitTestVisible = false,
            Child = chipRow
        )

    do
        generatingChip.Bind(Border.BackgroundProperty, Dyn.res "Palette.AccentFaint")
        |> ignore

    let crumbText =
        TextBlock(TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center)

    do
        crumbText.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

    let crumbAction =
        let b = Button(Focusable = false, HorizontalAlignment = HorizontalAlignment.Left)
        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b

    let crumbBar = Border(IsVisible = false, Child = crumbAction)

    let leftGroup =
        Grid(VerticalAlignment = VerticalAlignment.Center, ColumnSpacing = 8.0)

    let rightGroup =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0, VerticalAlignment = VerticalAlignment.Center)

    let dock =
        DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)

    let top = StackPanel()
    let bar = Border(IsVisible = false, Child = top)
    do bar.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore

    let mutable currentTitle = ""
    let mutable isEditable = false
    let mutable isEditing = false
    let mutable isGenerating = false
    let mutable isCompact = false
    let mutable crumbClickable = false

    let chrome (text: string) =
        ToolTip.SetTip(titleAction, text)
        ToolTip.SetTip(titleText, text)
        AutomationProperties.SetName(titleText, text)
        AutomationProperties.SetName(titleAction, if isEditable then sprintf "重命名对话：%s" text else text)

    let beginEdit () =
        if isEditable then
            isEditing <- true
            editBox.Text <- currentTitle
            titleAction.IsVisible <- false
            editShell.IsVisible <- true
            editBox.Focus() |> ignore
            editBox.SelectAll()
            renameStarted.Trigger()

    let commit () =
        if isEditing || editShell.IsVisible then
            isEditing <- false
            let next = if isNull editBox.Text then "" else editBox.Text.Trim()
            titleAction.IsVisible <- true
            editShell.IsVisible <- false

            if not (String.IsNullOrWhiteSpace next) && next <> currentTitle then
                currentTitle <- next
                titleText.Text <- next
                editBox.Text <- next
                renamed.Trigger next
            else
                editBox.Text <- currentTitle
                titleText.Text <- currentTitle

            chrome currentTitle

    let cancel () =
        if isEditing || editShell.IsVisible then
            isEditing <- false
            editBox.Text <- currentTitle
            titleAction.IsVisible <- true
            editShell.IsVisible <- false
            chrome currentTitle
            renameCancelled.Trigger()

    let syncChip () =
        if isCompact then
            generatingChip.IsVisible <- false
            generatingChip.Opacity <- 0.0
        else
            generatingChip.IsVisible <- true
            generatingChip.Opacity <- if isGenerating then 1.0 else 0.0
            generatingChip.IsHitTestVisible <- isGenerating
            generatingChip.Focusable <- isGenerating

    let applyCompact (compact: bool) =
        isCompact <- compact
        let size = if compact then 44.0 else 30.0

        for b in [ sidebarButton; moreButton ] do
            b.Width <- size
            b.Height <- size

        syncChip ()

    do
        chipRow.Children.Add dot
        chipRow.Children.Add caption
        ToolTip.SetTip(generatingChip, "正在生成回答")
        AutomationProperties.SetName(generatingChip, "正在生成回答")

        titleHost.Children.Add titleAction
        titleHost.Children.Add editShell

        titleAction.Click.Add(fun _ ->
            if isEditable then
                beginEdit ())

        titleAction.KeyDown.Add(fun e ->
            if (e.Key = Key.F2 || e.Key = Key.Enter) && isEditable then
                e.Handled <- true
                beginEdit ())

        editBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                commit ()
            elif e.Key = Key.Escape then
                e.Handled <- true
                cancel ())

        editBox.LostFocus.Add(fun _ ->
            if isEditing then
                commit ())

        crumbAction.Click.Add(fun _ ->
            if crumbClickable then
                crumbPressed.Trigger())

        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        leftGroup.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(sidebarButton, 0)
        Grid.SetColumn(titleHost, 1)
        Grid.SetColumn(generatingChip, 2)
        leftGroup.Children.Add sidebarButton
        leftGroup.Children.Add titleHost
        leftGroup.Children.Add generatingChip

        rightGroup.Children.Add moreButton
        DockPanel.SetDock(rightGroup, Dock.Right)
        dock.Children.Add rightGroup
        dock.Children.Add leftGroup

        let crumbIcon =
            Path(
                Data = Geometry.Parse "M 4 4 L 4 10 L 10 10 M 4 10 L 12 4",
                StrokeThickness = 1.2,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 12.0,
                Height = 12.0
            )

        crumbIcon.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore

        let crumbRow =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 6.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        crumbRow.Children.Add crumbIcon
        crumbRow.Children.Add crumbText
        crumbAction.Content <- crumbRow

        top.Children.Add dock
        top.Children.Add crumbBar
        bar.Height <- 38.0
        bar.BorderThickness <- Thickness(0.0, 0.0, 0.0, 1.0)

    /// 布局根节点
    member _.Host: Border = bar
    member _.TitleAction: Button = titleAction
    member _.TitleEditor: TextBox = editBox
    member _.MoreButton: Button = moreButton
    member _.SidebarButton: Button = sidebarButton
    member _.GeneratingChip: Border = generatingChip
    member _.CrumbAction: Button = crumbAction
    member _.Title = currentTitle
    member _.IsEditing = isEditing
    member _.CrumbText = if crumbBar.IsVisible then crumbText.Text else ""

    /// 事件上报
    member _.Renamed = renamed.Publish
    member _.RenameStarted = renameStarted.Publish
    member _.RenameCancelled = renameCancelled.Publish
    member _.MorePressed = morePressed.Publish
    member _.SidebarPressed = sidebarPressed.Publish
    member _.CrumbPressed = crumbPressed.Publish

    /// 单入口幂等渲染：根据 ChatHeaderVS 投影全量刷新顶栏各部件
    member this.Render(vs: ChatHeaderVS) =
        bar.IsVisible <- vs.Visible
        applyCompact vs.Compact
        bar.Padding <- Thickness(vs.InsetLeft, 0.0, vs.InsetLeft, 0.0)

        // 更多按钮与侧栏按钮可见性
        moreButton.IsVisible <- vs.ShowMore
        sidebarButton.IsVisible <- vs.ShowBack || not vs.Compact

        // 标题态投影
        match vs.Title with
        | TitleMode.Viewing(text, canEdit) ->
            isEditable <- canEdit
            currentTitle <- text

            if not isEditing then
                titleText.Text <- text
                editBox.Text <- text
                titleAction.IsVisible <- true
                editShell.IsVisible <- false

            titleAction.Focusable <- canEdit

            titleAction.Cursor <-
                if canEdit then
                    new Cursor(StandardCursorType.Hand)
                else
                    null

            chrome text
        | TitleMode.Editing draft ->
            isEditable <- true

            if not isEditing then
                isEditing <- true
                editBox.Text <- draft
                titleAction.IsVisible <- false
                editShell.IsVisible <- true
                editBox.Focus() |> ignore
                editBox.SelectAll()

        // 生成中芯片
        match vs.GeneratingStatus with
        | Some status ->
            isGenerating <- true
            caption.Text <- if String.IsNullOrWhiteSpace status then "生成中" else status
        | None -> isGenerating <- false

        syncChip ()

        // 面包屑
        match vs.Crumb with
        | Some c ->
            crumbClickable <- c.Clickable
            crumbText.Text <- c.ParentTitle
            crumbAction.Focusable <- c.Clickable

            crumbAction.Cursor <-
                if c.Clickable then
                    new Cursor(StandardCursorType.Hand)
                else
                    null

            ToolTip.SetTip(crumbAction, c.ParentTitle)
            AutomationProperties.SetName(crumbAction, c.ParentTitle)
            crumbBar.IsVisible <- not (String.IsNullOrEmpty c.ParentTitle)
        | None ->
            crumbClickable <- false
            crumbBar.IsVisible <- false

    member _.BeginRename() = beginEdit ()
    member _.CommitRename(_refocus: bool) = commit ()
    member _.CancelRename() = cancel ()
