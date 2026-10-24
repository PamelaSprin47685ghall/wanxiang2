namespace Wanxiang2.ConversationRows

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Controls.Templates
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 一段文字：是否为搜索命中
type RowSpan = { Text: string; IsMatch: bool }

/// 一行对话的呈现
type RowModel =
    {
        Id: Guid
        Title: string
        TitleSpans: RowSpan list
        Preview: string
        PreviewSpans: RowSpan list
        AccessibleName: string
        Tooltip: string
        Running: bool
        Pinned: bool
        Archived: bool
    }

/// 一条消息正文命中
type HitModel =
    {
        Key: string
        Title: string
        Before: string
        Matched: string
        After: string
        AccessibleName: string
    }

/// 列表里的一项
[<RequireQualifiedAccess>]
type RowItem =
    | Header of string
    | Conversation of RowModel
    | Hit of HitModel
    | Status of text: string * action: (string * string) option
    | ArchivedToggle of expanded: bool * count: int

/// Esc 从哪里按下
type RowsEscapeArgs(kind: string) =
    member _.Kind = kind
    member val Handled = false with get, set

/// 对话列表：废除 Look 记录，去脑化，每行通过 Render(SidebarRowVS) 或 RowModel 呈现。
/// 样式通过样式类与统一主题系统获取。
type ConversationRows() =
    let list =
        ListBox(Background = Brushes.Transparent, BorderThickness = Thickness 0.0, Focusable = true)

    do AutomationProperties.SetName(list, "对话列表")
    let rowHosts = Dictionary<Guid, Control>()
    let moreButtons = Dictionary<Guid, Button>()
    let models = Dictionary<Guid, RowModel>()
    let flatIndex = Dictionary<Guid, int>()
    let mutable visible: Guid array = [||]
    let mutable toggleIndex: int option = None
    let mutable toggleHost: Button option = None
    let mutable activeId: Guid option = None
    let mutable isSelected: Guid -> bool = fun _ -> false
    let mutable selectionActive = false

    let rowActivated = Event<Guid>()
    let rowFocused = Event<Guid>()
    let menuRequested = Event<Guid * Control * bool>()
    let renameKey = Event<Guid>()
    let deleteKey = Event<Guid>()
    let pinKey = Event<Guid>()
    let selectAllKey = Event<unit>()
    let escape = Event<RowsEscapeArgs>()
    let leaveTop = Event<unit>()
    let hitClicked = Event<string>()
    let statusAction = Event<string>()
    let toggleClicked = Event<unit>()

    let spans (block: TextBlock) (parts: RowSpan list) =
        block.Text <- null
        block.Inlines.Clear()

        for p in parts do
            let run = Run(p.Text)

            if p.IsMatch then
                run.Foreground <- Brushes.Blue

            block.Inlines.Add(run :> Inline)

    let highlighted (id: Guid) = activeId = Some id || isSelected id

    let applyState (id: Guid) (host: Control) =
        let lit = highlighted id

        if lit then
            if not (host.Classes.Contains "selected") then
                host.Classes.Add "selected"

        else
            host.Classes.Remove "selected" |> ignore

        match host with
        | :? Button as button ->
            button.BorderThickness <- if lit then Thickness(2.0, 0.0, 0.0, 0.0) else Thickness 0.0

            if lit then
                if not (button.Classes.Contains "current-fill") then
                    button.Classes.Add "current-fill"
                    button.Bind(Button.BackgroundProperty, Dyn.res "Palette.Selected") |> ignore
            elif button.Classes.Contains "current-fill" then
                button.Classes.Remove "current-fill" |> ignore
                button.ClearValue Button.BackgroundProperty
        | _ -> ()

        let status =
            match models.TryGetValue id with
            | true, m ->
                match lit, m.Running with
                | true, true -> "当前对话，生成中"
                | true, false -> "当前对话"
                | false, true -> "生成中"
                | false, false when m.Pinned -> "已置顶"
                | false, false when m.Archived -> "已归档"
                | _ -> ""
            | _ -> if lit then "当前对话" else ""

        AutomationProperties.SetItemStatus(host, status)

    let applyMore (button: Button) =
        if selectionActive then
            button.IsVisible <- false
            button.IsHitTestVisible <- false
            button.Focusable <- false
            AutomationProperties.SetAccessibilityView(button, AccessibilityView.Raw)
        else
            button.IsVisible <- true
            button.IsHitTestVisible <- true
            button.Focusable <- true
            AutomationProperties.SetAccessibilityView(button, AccessibilityView.Default)

    let fireEscape kind =
        let args = RowsEscapeArgs(kind)
        escape.Trigger args
        args.Handled

    let focusRowAt (index: int) =
        if index >= 0 && index < visible.Length then
            let id = visible[index]

            match flatIndex.TryGetValue id with
            | true, fi ->
                list.ScrollIntoView fi

                match rowHosts.TryGetValue id with
                | true, host ->
                    host.BringIntoView()
                    host.Focus NavigationMethod.Directional |> ignore
                | _ -> ()
            | _ -> ()

    let focusToggle () =
        match toggleIndex with
        | Some fi ->
            list.ScrollIntoView fi

            match toggleHost with
            | Some host -> host.Focus NavigationMethod.Directional |> ignore
            | None -> ()
        | None -> ()

    let move (id: Guid) (delta: int) =
        match visible |> Array.tryFindIndex ((=) id) with
        | Some 0 when delta < 0 -> leaveTop.Trigger()
        | Some index ->
            let target = index + delta

            if target >= visible.Length && toggleIndex.IsSome then
                focusToggle ()
            else
                focusRowAt (Math.Clamp(target, 0, visible.Length - 1))
        | None -> ()

    let renderRow (m: RowModel) : Control =
        let id = m.Id

        let title =
            TextBlock(
                FontSize = 13.5,
                FontWeight = FontWeight.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            )

        let preview =
            TextBlock(
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            )

        preview.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint")
        |> ignore

        let slot =
            Border(
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 8.0,
                Height = 12.0,
                Margin = Thickness(0.0, 0.0, 8.0, 0.0)
            )

        // react 行首无槽位：只有运行中圆点占位，其余整槽收成 0 宽（置顶由分组头表达）
        if m.Running then
            let dot =
                Border(
                    Width = 6.0,
                    Height = 6.0,
                    CornerRadius = CornerRadius 3.0,
                    HorizontalAlignment = HorizontalAlignment.Center
                )

            dot.Bind(Border.BackgroundProperty, Dyn.res "Palette.Accent") |> ignore
            slot.Child <- dot
        else
            slot.Width <- 0.0
            slot.Height <- 0.0
            slot.Margin <- Thickness 0.0

        let titleRow =
            DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)

        DockPanel.SetDock(slot, Dock.Left)
        titleRow.Children.Add slot

        let more =
            let b = Button(Content = "⋯")
            b.Classes.Add "icon-button"
            b.Classes.Add "ghost"
            AutomationProperties.SetName(b, sprintf "对话“%s”的操作菜单" m.Title)
            AutomationProperties.SetHelpText(b, "打开对话操作菜单")

            b.Click.Add(fun _ ->
                if not selectionActive then
                    menuRequested.Trigger((id, b :> Control, true)))

            b

        DockPanel.SetDock(more, Dock.Right)
        titleRow.Children.Add more
        titleRow.Children.Add title

        let column =
            StackPanel(Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Spacing = 2.0)

        column.Children.Add titleRow
        column.Children.Add preview

        let host = Button(Content = column)
        host.Classes.Add "ghost"
        host.Classes.Add "hover-plain"
        host.HorizontalAlignment <- HorizontalAlignment.Stretch
        host.MinHeight <- 44.0
        host.Padding <- Thickness(10.0, 6.0)
        host.CornerRadius <- CornerRadius(0.0, 8.0, 8.0, 0.0)
        host.Bind(Button.BorderBrushProperty, Dyn.res "Palette.Accent") |> ignore
        host.Click.Add(fun _ -> rowActivated.Trigger id)
        host.Tag <- id
        AutomationProperties.SetName(host, m.AccessibleName)
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.ListItem)

        models[id] <- m
        spans title m.TitleSpans
        spans preview m.PreviewSpans
        ToolTip.SetTip(host, m.Tooltip)
        rowHosts[id] <- host :> Control
        moreButtons[id] <- more
        applyState id host
        applyMore more

        host.GotFocus.Add(fun _ ->
            rowFocused.Trigger id
            applyState id host)

        host.LostFocus.Add(fun _ -> applyState id host)

        host.KeyDown.Add(fun e ->
            let ctrl =
                e.KeyModifiers.HasFlag KeyModifiers.Control
                || e.KeyModifiers.HasFlag KeyModifiers.Meta

            if (e.Key = Key.Enter || e.Key = Key.Space) && not selectionActive then
                e.Handled <- true
                rowActivated.Trigger id

            if e.Handled then
                ()
            elif e.Key = Key.F2 then
                e.Handled <- true
                renameKey.Trigger id
            elif e.Key = Key.Delete then
                e.Handled <- true
                deleteKey.Trigger id
            elif e.Key = Key.P && not ctrl then
                e.Handled <- true
                pinKey.Trigger id
            elif selectionActive && e.Key = Key.A && ctrl then
                e.Handled <- true
                selectAllKey.Trigger()
            elif selectionActive && e.Key = Key.Escape then
                e.Handled <- fireEscape "row"
            elif e.Key = Key.Down then
                e.Handled <- true
                move id 1
            elif e.Key = Key.Up then
                e.Handled <- true
                move id -1
            elif e.Key = Key.Escape then
                e.Handled <- fireEscape "row"
            elif e.Key = Key.Home || e.Key = Key.PageUp then
                e.Handled <- true
                focusRowAt 0
            elif e.Key = Key.End || e.Key = Key.PageDown then
                e.Handled <- true

                if toggleIndex.IsSome then
                    focusToggle ()
                elif visible.Length > 0 then
                    focusRowAt (visible.Length - 1)
            elif
                (e.Key = Key.F10 && e.KeyModifiers.HasFlag KeyModifiers.Shift)
                || e.Key = Key.Apps
            then
                e.Handled <- true

                if not selectionActive then
                    menuRequested.Trigger((id, host :> Control, false)))

        host.PointerReleased.Add(fun e ->
            if e.InitialPressMouseButton = MouseButton.Right then
                e.Handled <- true
                host.Focus NavigationMethod.Pointer |> ignore
                applyState id host

                if not selectionActive then
                    menuRequested.Trigger((id, host :> Control, false)))

        host :> Control

    let renderHit (h: HitModel) : Control =
        let title = TextBlock(Text = h.Title, TextTrimming = TextTrimming.CharacterEllipsis)

        let snippet =
            TextBlock(TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis)

        if h.Before <> "" then
            snippet.Inlines.Add(Run(h.Before))

        if h.Matched <> "" then
            let run = Run(h.Matched)
            run.FontWeight <- FontWeight.Bold
            snippet.Inlines.Add run

        if h.After <> "" then
            snippet.Inlines.Add(Run(h.After))

        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        column.Children.Add title
        column.Children.Add snippet
        let host = Button(Content = column)
        host.Classes.Add "ghost"
        host.Classes.Add "surface"
        host.Classes.Add "hover-plain"
        host.HorizontalAlignment <- HorizontalAlignment.Stretch
        host.Padding <- Thickness(16.0, 8.0)
        host.Click.Add(fun _ -> hitClicked.Trigger h.Key)
        host.Tag <- h
        AutomationProperties.SetName(host, h.AccessibleName)
        ToolTip.SetTip(host, "打开对话并定位到这条消息")

        host.KeyDown.Add(fun e ->
            if e.Key = Key.Escape then
                e.Handled <- fireEscape "hit")

        host :> Control

    let renderStatus (text: string) (action: (string * string) option) : Control =
        let label = TextBlock(Text = text, TextWrapping = TextWrapping.Wrap)
        AutomationProperties.SetName(label, text)

        let children =
            [
                yield label :> Control
                match action with
                | Some(caption, key) ->
                    let b = Button(Content = caption)
                    AutomationProperties.SetName(b, caption)
                    b.Classes.Add "ghost"
                    b.Classes.Add "button-ghost"
                    b.HorizontalAlignment <- HorizontalAlignment.Left
                    b.Click.Add(fun _ -> statusAction.Trigger key)
                    yield b :> Control
                | None -> ()
            ]

        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)

        for c in children do
            panel.Children.Add c

        let b = Border(Child = panel, Padding = Thickness(16.0, 8.0))
        b :> Control

    let renderToggle (expanded: bool) (count: int) : Control =
        let labelText = if expanded then "返回对话" else sprintf "归档 %d" count

        let name =
            if expanded then
                "收起已归档对话"
            else
                sprintf "已归档对话（%d），点击展开" count

        let caption =
            TextBlock(Text = labelText, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center)

        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        let host = Button(Content = caption)
        host.Classes.Add "ghost"
        host.Classes.Add "hover-plain"
        host.HorizontalAlignment <- HorizontalAlignment.Left
        host.MinHeight <- 34.0
        host.Padding <- Thickness(8.0, 0.0)
        host.Margin <- Thickness(12.0, 4.0)

        host.Click.Add(fun _ ->
            match toggleHost with
            | Some h -> h.Focus NavigationMethod.Directional |> ignore
            | None -> ()

            toggleClicked.Trigger()
            focusToggle ())

        host.Tag <- "archived-toggle"
        AutomationProperties.SetName(host, name)
        AutomationProperties.SetItemStatus(host, if expanded then "已展开" else "已折叠")
        ToolTip.SetTip(host, name)
        toggleHost <- Some host

        host.KeyDown.Add(fun e ->
            if e.Key = Key.Up then
                e.Handled <- true

                if visible.Length > 0 then
                    focusRowAt (visible.Length - 1)
                else
                    leaveTop.Trigger()
            elif e.Key = Key.Down || e.Key = Key.End || e.Key = Key.PageDown then
                e.Handled <- true
            elif e.Key = Key.Home || e.Key = Key.PageUp then
                e.Handled <- true

                if visible.Length > 0 then
                    focusRowAt 0
                else
                    leaveTop.Trigger()
            elif e.Key = Key.Escape then
                e.Handled <- fireEscape "archived")

        host :> Control

    let build (item: RowItem) : Control =
        match item with
        | RowItem.Header label ->
            let header =
                TextBlock(
                    Text = label,
                    FontSize = 11.5,
                    FontWeight = FontWeight.Medium,
                    Margin = Thickness(8.0, 12.0, 8.0, 4.0)
                )

            header.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
            header.Classes.Add "section-label"
            header :> Control
        | RowItem.Conversation m -> renderRow m
        | RowItem.Hit h -> renderHit h
        | RowItem.Status(text, action) -> renderStatus text action
        | RowItem.ArchivedToggle(expanded, count) -> renderToggle expanded count

    do
        list.ItemsPanel <- FuncTemplate<Panel>(fun () -> VirtualizingStackPanel() :> Panel)
        list.ItemTemplate <- FuncDataTemplate<RowItem>((fun item _ -> build item), true)

        list.ContainerPrepared.Add(fun args ->
            match args.Container with
            | :? ListBoxItem as item ->
                item.Focusable <- false
                item.Padding <- Thickness 0.0
                item.Margin <- Thickness 0.0
                item.Background <- Brushes.Transparent
                item.BorderThickness <- Thickness 0.0
                item.HorizontalContentAlignment <- HorizontalAlignment.Stretch
            | _ -> ())

        AutomationProperties.SetName(list, "对话列表")

    member _.View = list :> Control
    member _.List = list

    /// 换整份列表
    member _.Render(items: RowItem list) =
        let scroller =
            list.GetVisualDescendants()
            |> Seq.tryPick (function
                | :? ScrollViewer as sv -> Some sv
                | _ -> None)

        let saved = scroller |> Option.map (fun sv -> sv.Offset)
        rowHosts.Clear()
        flatIndex.Clear()
        toggleHost <- None
        toggleIndex <- None
        let ids = ResizeArray<Guid>()

        items
        |> List.iteri (fun i item ->
            match item with
            | RowItem.Conversation m ->
                models[m.Id] <- m
                flatIndex[m.Id] <- i
                ids.Add m.Id
            | RowItem.ArchivedToggle _ -> toggleIndex <- Some i
            | _ -> ())

        visible <- ids.ToArray()
        list.ItemsSource <- ResizeArray(items)

        match saved, scroller with
        | Some o, Some sv -> sv.Offset <- o
        | _ -> ()

    /// 单行渲染适配器：从 SidebarRowVS 产生 RowModel
    member this.Render(row: SidebarRowVS) =
        let spans text = [ { Text = text; IsMatch = false } ]

        let m: RowModel =
            {
                Id = row.Id
                Title = row.Title
                TitleSpans = spans row.Title
                Preview = row.Preview
                PreviewSpans = spans row.Preview
                AccessibleName = row.Title
                Tooltip = row.Title
                Running = row.Running
                Pinned = row.Pinned
                Archived = row.Archived
            }

        renderRow m

    member _.VisibleIds: Guid list = List.ofArray visible
    member _.HasArchivedToggle = toggleIndex.IsSome
    member _.ArchivedToggle = toggleHost |> Option.map (fun t -> t :> Control)

    member _.FocusedRow: Guid option =
        rowHosts
        |> Seq.tryPick (fun kv -> if kv.Value.IsFocused then Some kv.Key else None)

    member _.RowHost(id: Guid) : Control option =
        match rowHosts.TryGetValue id with
        | true, h -> Some h
        | _ -> None

    member _.MoreButton(id: Guid) : Control option =
        match moreButtons.TryGetValue id with
        | true, b -> Some(b :> Control)
        | _ -> None

    member _.FocusRowAt(index: int) = focusRowAt index

    member _.FocusRow(id: Guid) =
        match rowHosts.TryGetValue id with
        | true, host when not host.IsFocused -> host.Focus NavigationMethod.Directional |> ignore
        | _ -> ()

    member _.FocusArchivedToggle() = focusToggle ()

    member _.ScrollTo(id: Guid) =
        match flatIndex.TryGetValue id with
        | true, fi -> list.ScrollIntoView fi
        | _ -> ()

    member _.ApplyMarks(active: Guid option, selected: Guid -> bool, inSelection: bool) =
        activeId <- active
        isSelected <- selected
        selectionActive <- inSelection

        for kv in rowHosts do
            applyState kv.Key kv.Value

        for b in moreButtons.Values do
            applyMore b

    [<CLIEvent>]
    member _.RowActivated = rowActivated.Publish

    [<CLIEvent>]
    member _.RowFocused = rowFocused.Publish

    [<CLIEvent>]
    member _.MenuRequested = menuRequested.Publish

    [<CLIEvent>]
    member _.RenameKey = renameKey.Publish

    [<CLIEvent>]
    member _.DeleteKey = deleteKey.Publish

    [<CLIEvent>]
    member _.PinKey = pinKey.Publish

    [<CLIEvent>]
    member _.SelectAllKey = selectAllKey.Publish

    [<CLIEvent>]
    member _.Escape = escape.Publish

    [<CLIEvent>]
    member _.LeaveTop = leaveTop.Publish

    [<CLIEvent>]
    member _.HitClicked = hitClicked.Publish

    [<CLIEvent>]
    member _.StatusActionClicked = statusAction.Publish

    [<CLIEvent>]
    member _.ArchivedToggleClicked = toggleClicked.Publish
