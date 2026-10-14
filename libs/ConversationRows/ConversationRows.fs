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
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree

/// 一段文字：是否为搜索命中
type RowSpan = { Text: string; IsMatch: bool }

/// 一行对话的呈现（文字与读屏名由组合者算好）
type RowModel =
    { Id: Guid
      Title: string
      TitleSpans: RowSpan list
      Preview: string
      PreviewSpans: RowSpan list
      AccessibleName: string
      Tooltip: string
      Running: bool
      Pinned: bool
      Archived: bool }

/// 一条消息正文命中
type HitModel =
    { Key: string
      /// 标题行（已含「· 已归档」）
      Title: string
      Before: string
      Matched: string
      After: string
      AccessibleName: string }

/// 列表里的一项
[<RequireQualifiedAccess>]
type RowItem =
    | Header of string
    | Conversation of RowModel
    | Hit of HitModel
    /// 状态行：文字 + 可选动作（文案, 键）
    | Status of text: string * action: (string * string) option
    | ArchivedToggle of expanded: bool * count: int

/// Esc 从哪里按下；Handled 由组合者置真
type RowsEscapeArgs(kind: string) =
    member _.Kind = kind
    member val Handled = false with get, set

/// 对话列表（迁自旧 Views/Sidebar.fs 的 RenderRow、RenderMessageHit、RenderSearchStatus、
/// RenderArchivedToggle、ApplyRowState、ApplyRowActionVisibility、FocusRowAt、MoveRowFocus 与列表模板）。
/// 当前对话、多选状态、搜索与菜单都归组合者；本控件只画行、走行内键盘、发事件。
type ConversationRows(look: ConversationRowsLook) =
    let kit = Kit(look)
    let list = ListBox(Background = Brushes.Transparent, BorderThickness = Thickness 0.0, Focusable = true)
    let rowHosts = Dictionary<Guid, Tap>()
    let moreButtons = Dictionary<Guid, Tap>()
    let models = Dictionary<Guid, RowModel>()
    let flatIndex = Dictionary<Guid, int>()
    let mutable visible: Guid array = [||]
    let mutable toggleIndex: int option = None
    let mutable toggleHost: Tap option = None
    let mutable activeId: Guid option = None
    let mutable isSelected: Guid -> bool = fun _ -> false
    let mutable selectionActive = false
    let mutable compact = false
    let mutable iconSize = 0.0
    let mutable compactSize = 0.0
    let mutable subtle = 0.0

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
        // 先丢掉 Text：Text 是 Inlines 的隐式 run，留着会叠两遍
        block.Text <- null
        block.Inlines.Clear()
        for p in parts do
            let run = Run(p.Text)
            if p.IsMatch then run.Foreground <- look.Accent
            block.Inlines.Add run

    let highlighted (id: Guid) = activeId = Some id || isSelected id

    let applyState (id: Guid) (host: Tap) =
        let lit = highlighted id
        host.Background <- (if lit then look.Selected else Brushes.Transparent :> IBrush)
        host.BorderBrush <- (if lit then look.Accent else Brushes.Transparent :> IBrush)
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

    /// 多选模式下整批下线；否则常驻淡显，悬停 / 聚焦时全不透明
    let applyMore (button: Tap) (emphasized: bool) =
        if selectionActive then
            button.IsVisible <- false
            button.IsHitTestVisible <- false
            button.Focusable <- false
            AutomationProperties.SetAccessibilityView(button, AccessibilityView.Raw)
        else
            button.IsVisible <- true
            button.Opacity <- (if emphasized then 1.0 else subtle)
            button.IsHitTestVisible <- true
            button.Focusable <- true
            AutomationProperties.SetAccessibilityView(button, AccessibilityView.Default)

    let square (button: Tap) =
        let v = if compact then compactSize else iconSize
        button.Width <- v
        button.Height <- v

    let stateSlot (slot: Border) (m: RowModel) =
        if m.Running then
            let dot = kit.StatusDot(look.RunningDotSize, look.Accent)
            dot.HorizontalAlignment <- HorizontalAlignment.Center
            slot.Child <- dot
        elif m.Pinned then
            let pin = look.Glyph "pin" look.Faint
            kit.Live(look.StateGlyphSize, fun v -> pin.Width <- v; pin.Height <- v)
            pin.HorizontalAlignment <- HorizontalAlignment.Center
            pin.VerticalAlignment <- VerticalAlignment.Center
            slot.Child <- pin
        else slot.Child <- null

    let rowShell (child: Control) (name: string) (action: unit -> unit) =
        let host = Tap(child, name, [ "surface"; "hover-plain" ], action)
        host.Background <- Brushes.Transparent
        host.VerticalAlignment <- VerticalAlignment.Stretch
        host.HorizontalAlignment <- HorizontalAlignment.Stretch
        kit.Live(Kit.Pair(look.SpaceXl, look.RowPaddingY), fun (x, y) -> host.Padding <- Thickness(x, y))
        kit.Live(look.RadiusMd, fun v -> host.CornerRadius <- CornerRadius v)
        kit.Live(look.RowMinHeight, fun v -> host.MinHeight <- v)
        host

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
                // 聚焦只走一次 Post（容器实现后 BringIntoView + 聚焦），与旧实现同一机制
                Dispatcher.UIThread.Post(
                    (fun () ->
                        match rowHosts.TryGetValue id with
                        | true, host ->
                            host.BringIntoView()
                            host.Focus NavigationMethod.Directional |> ignore
                        | _ -> ()),
                    DispatcherPriority.Input)
            | _ -> ()

    let focusToggle () =
        match toggleIndex with
        | Some fi ->
            list.ScrollIntoView fi
            Dispatcher.UIThread.Post(fun () ->
                match toggleHost with
                | Some host -> host.Focus NavigationMethod.Directional |> ignore
                | None -> ())
        | None -> ()

    let move (id: Guid) (delta: int) =
        match visible |> Array.tryFindIndex ((=) id) with
        | Some 0 when delta < 0 -> leaveTop.Trigger()
        | Some index ->
            let target = index + delta
            if target >= visible.Length && toggleIndex.IsSome then focusToggle ()
            else focusRowAt (Math.Clamp(target, 0, visible.Length - 1))
        | None -> ()

    let renderRow (m: RowModel) : Control =
        let id = m.Id
        let title =
            TextBlock(
                FontWeight = FontWeight.Medium,
                Foreground = look.Text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center)
        kit.Live(look.SmallFontSize, fun v -> title.FontSize <- v)
        let preview =
            TextBlock(
                Foreground = look.Faint,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center)
        kit.Live(look.CaptionFontSize, fun v -> preview.FontSize <- v)
        kit.Live(Kit.Pair(look.StateSlotWidth, look.SpaceMd), fun (w, g) -> preview.Margin <- Thickness(w + g, 0.0, 0.0, 0.0))
        let slot =
            Border(
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left)
        kit.Live(look.StateSlotWidth, fun v -> slot.Width <- v)
        kit.Live(look.StateGlyphSize, fun v -> slot.Height <- v)
        kit.Live(look.SpaceMd, fun v -> slot.Margin <- Thickness(0.0, 0.0, v, 0.0))
        let titleRow = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(slot, Dock.Left)
        titleRow.Children.Add slot
        let more =
            kit.IconButton(
                "more",
                "更多操作",
                fun () -> if not selectionActive then menuRequested.Trigger((id, moreButtons[id] :> Control, true)))
        AutomationProperties.SetName(more, sprintf "对话“%s”的操作菜单" m.Title)
        AutomationProperties.SetHelpText(more, "打开对话操作菜单")
        square more
        DockPanel.SetDock(more, Dock.Right)
        titleRow.Children.Add more
        titleRow.Children.Add title
        let column = StackPanel(Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center)
        kit.Live(look.RowContentSpacing, fun v -> column.Spacing <- v)
        column.Children.Add titleRow
        column.Children.Add preview
        let host = rowShell column m.AccessibleName (fun () -> rowActivated.Trigger id)
        host.Tag <- id
        host.BorderBrush <- Brushes.Transparent
        kit.Live(look.SelectedEdgeWidth, fun v -> host.BorderThickness <- Thickness(v, 0.0, 0.0, 0.0))
        host.Transitions <- look.Transitions true
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.ListItem)
        models[id] <- m
        spans title m.TitleSpans
        spans preview m.PreviewSpans
        stateSlot slot m
        ToolTip.SetTip(host, m.Tooltip)
        rowHosts[id] <- host
        moreButtons[id] <- more
        applyState id host
        applyMore more false
        host.DetachedFromVisualTree.Add(fun _ ->
            match rowHosts.TryGetValue id with
            | true, current when obj.ReferenceEquals(current, host) ->
                rowHosts.Remove id |> ignore
                moreButtons.Remove id |> ignore
            | _ -> ())
        more.GotFocus.Add(fun _ -> applyMore more true)
        more.LostFocus.Add(fun _ -> if not host.IsFocused then applyMore more false)
        host.PointerEntered.Add(fun _ ->
            // 悬停只动底色；已选行上层叠高亮，绝不比已选更暗
            host.Background <- (if highlighted id then look.SelectedHover else look.Hover)
            applyMore more true)
        host.PointerExited.Add(fun _ ->
            applyState id host
            if not more.IsFocused then applyMore more false)
        host.GotFocus.Add(fun _ ->
            rowFocused.Trigger id
            applyState id host
            applyMore more true)
        host.LostFocus.Add(fun _ ->
            applyState id host
            if not more.IsFocused then applyMore more false)
        host.KeyDown.Add(fun e ->
            let ctrl = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
            if e.Handled then ()
            elif e.Key = Key.F2 then e.Handled <- true; renameKey.Trigger id
            elif e.Key = Key.Delete then e.Handled <- true; deleteKey.Trigger id
            elif e.Key = Key.P && not ctrl then e.Handled <- true; pinKey.Trigger id
            elif selectionActive && e.Key = Key.A && ctrl then e.Handled <- true; selectAllKey.Trigger()
            elif selectionActive && e.Key = Key.Escape then e.Handled <- fireEscape "row"
            elif e.Key = Key.Down then e.Handled <- true; move id 1
            elif e.Key = Key.Up then e.Handled <- true; move id -1
            elif e.Key = Key.Escape then e.Handled <- fireEscape "row"
            elif e.Key = Key.Home || e.Key = Key.PageUp then e.Handled <- true; focusRowAt 0
            elif e.Key = Key.End || e.Key = Key.PageDown then
                e.Handled <- true
                if toggleIndex.IsSome then focusToggle ()
                elif visible.Length > 0 then focusRowAt (visible.Length - 1)
            elif (e.Key = Key.F10 && e.KeyModifiers.HasFlag KeyModifiers.Shift) || e.Key = Key.Apps then
                e.Handled <- true
                if not selectionActive then menuRequested.Trigger((id, host :> Control, false)))
        host.PointerPressed.Add(fun e ->
            if e.GetCurrentPoint(host).Properties.IsRightButtonPressed then
                host.Focus NavigationMethod.Pointer |> ignore
                applyState id host)
        host.PointerReleased.Add(fun e ->
            if e.InitialPressMouseButton = MouseButton.Right then
                e.Handled <- true
                host.Focus NavigationMethod.Pointer |> ignore
                applyState id host
                if not selectionActive then menuRequested.Trigger((id, host :> Control, false)))
        host :> Control

    let renderHit (h: HitModel) : Control =
        let title = TextBlock(Text = h.Title, Foreground = look.Muted, TextTrimming = TextTrimming.CharacterEllipsis)
        kit.Live(look.CaptionFontSize, fun v -> title.FontSize <- v)
        let snippet =
            TextBlock(
                Foreground = look.Text,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis)
        kit.Live(look.SmallFontSize, fun v -> snippet.FontSize <- v)
        if h.Before <> "" then snippet.Inlines.Add(Run(h.Before))
        if h.Matched <> "" then
            let run = Run(h.Matched)
            run.Foreground <- look.Accent
            run.FontWeight <- FontWeight.Medium
            // 只换字色在浅色主题下几乎看不出来：加一层强调浅底
            run.Background <- look.AccentSoft
            snippet.Inlines.Add run
        if h.After <> "" then snippet.Inlines.Add(Run(h.After))
        let column = kit.VStack(look.RowContentSpacing, [ title :> Control; snippet :> Control ])
        kit.Live(Kit.Pair(look.StateSlotWidth, look.SpaceMd), fun (w, g) -> column.Margin <- Thickness(w + g, 0.0, 0.0, 0.0))
        let host = rowShell column h.AccessibleName (fun () -> hitClicked.Trigger h.Key)
        host.Tag <- h
        host.Transitions <- look.Transitions false
        ToolTip.SetTip(host, "打开对话并定位到这条消息")
        host.KeyDown.Add(fun e -> if e.Key = Key.Escape then e.Handled <- fireEscape "hit")
        host :> Control

    let renderStatus (text: string) (action: (string * string) option) : Control =
        let label = kit.Text(text, look.CaptionFontSize, look.Muted)
        label.TextWrapping <- TextWrapping.Wrap
        AutomationProperties.SetName(label, text)
        let children =
            [ yield label :> Control
              match action with
              | Some(caption, key) ->
                  let b = kit.Button(Tone.Ghost, caption, fun () -> statusAction.Trigger key)
                  b.HorizontalAlignment <- HorizontalAlignment.Left
                  yield b :> Control
              | None -> () ]
        let b = Border(Child = kit.VStack(look.SpaceMd, children))
        kit.Live(Kit.Pair(look.SpaceXl, look.SpaceMd), fun (x, y) -> b.Padding <- Thickness(x, y))
        b :> Control

    let toggleName expanded count =
        let label = if expanded then "收起已归档对话" else sprintf "已归档对话（%d）" count
        label, (if expanded then label else sprintf "%s，点击展开" label)

    let renderToggle (expanded: bool) (count: int) : Control =
        let labelText, name = toggleName expanded count
        let glyph = look.Glyph "archive" look.Muted
        glyph.VerticalAlignment <- VerticalAlignment.Center
        let label = kit.Text(labelText, look.SmallFontSize, look.Muted)
        label.VerticalAlignment <- VerticalAlignment.Center
        label.TextTrimming <- TextTrimming.CharacterEllipsis
        let chevron = look.Glyph (if expanded then "chevron-up" else "chevron-down") look.Muted
        chevron.VerticalAlignment <- VerticalAlignment.Center
        let leading = kit.HStack(look.SpaceMd, [ glyph; label :> Control ])
        DockPanel.SetDock(chevron, Dock.Right)
        let row = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
        row.Children.Add chevron
        row.Children.Add leading
        let mutable host: Tap = Unchecked.defaultof<_>
        host <-
            rowShell row name (fun () ->
                toggleClicked.Trigger()
                // 切换会重建本行：Post 回来把焦点还给新行，连按不断线
                Dispatcher.UIThread.Post(fun () ->
                    match toggleHost with
                    | Some current -> current.Focus NavigationMethod.Directional |> ignore
                    | None -> ()))
        host.Transitions <- look.Transitions false
        host.Tag <- "archived-toggle"
        // 旧 SetExpanded = ItemStatus「已展开 / 已折叠」
        AutomationProperties.SetItemStatus(host, if expanded then "已展开" else "已折叠")
        ToolTip.SetTip(host, name)
        toggleHost <- Some host
        host.DetachedFromVisualTree.Add(fun _ ->
            match toggleHost with
            | Some current when obj.ReferenceEquals(current, host) -> toggleHost <- None
            | _ -> ())
        host.KeyDown.Add(fun e ->
            if e.Key = Key.Up then
                e.Handled <- true
                if visible.Length > 0 then focusRowAt (visible.Length - 1) else leaveTop.Trigger()
            elif e.Key = Key.Down || e.Key = Key.End || e.Key = Key.PageDown then
                // 已是最后一项：吞掉，免得 ListBox 默认导航把焦点吸走
                e.Handled <- true
            elif e.Key = Key.Home || e.Key = Key.PageUp then
                e.Handled <- true
                if visible.Length > 0 then focusRowAt 0 else leaveTop.Trigger()
            elif e.Key = Key.Escape then e.Handled <- fireEscape "archived")
        host :> Control

    let build (item: RowItem) : Control =
        match item with
        | RowItem.Header label ->
            let header = kit.SectionLabel label
            header.Focusable <- false
            let b = Border(Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, Child = header)
            kit.Live(Kit.Pair(look.SpaceXl, look.SpaceXs), fun (x, s) -> b.Padding <- Thickness(x, x, x, s))
            b :> Control
        | RowItem.Conversation m -> renderRow m
        | RowItem.Hit h -> renderHit h
        | RowItem.Status(text, action) -> renderStatus text action
        | RowItem.ArchivedToggle(expanded, count) -> renderToggle expanded count

    do
        list.ItemsPanel <- FuncTemplate<Panel>(fun () -> VirtualizingStackPanel() :> Panel)
        // Avalonia 12：模板是 (item, namescope) → Control；回收靠虚拟化容器，不靠 existing
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
        kit.Live(look.IconButtonSize, fun v -> iconSize <- v; for b in moreButtons.Values do square b)
        kit.Live(look.CompactTarget, fun v -> compactSize <- v; for b in moreButtons.Values do square b)
        kit.Live(look.OpacitySubtle, fun v ->
            subtle <- v
            for b in moreButtons.Values do applyMore b (b.IsFocused || b.IsPointerOver))

    member _.View = list :> Control
    member _.List = list

    /// 换整份列表。滚动偏移原样保留；焦点还原由组合者用 FocusedRow / FocusRow 做。
    member _.Render(items: RowItem list) =
        let scroller =
            list.GetVisualDescendants()
            |> Seq.tryPick (function :? ScrollViewer as sv -> Some sv | _ -> None)
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

    /// 本次渲染的行序
    member _.VisibleIds: Guid list = List.ofArray visible
    member _.HasArchivedToggle = toggleIndex.IsSome
    member _.ArchivedToggle = toggleHost |> Option.map (fun t -> t :> Control)

    /// 当前有键盘焦点的行
    member _.FocusedRow: Guid option =
        rowHosts |> Seq.tryPick (fun kv -> if kv.Value.IsFocused then Some kv.Key else None)

    member _.RowHost(id: Guid) : Control option =
        match rowHosts.TryGetValue id with
        | true, h -> Some(h :> Control)
        | _ -> None

    member _.MoreButton(id: Guid) : Control option =
        match moreButtons.TryGetValue id with
        | true, b -> Some(b :> Control)
        | _ -> None

    member _.FocusRowAt(index: int) = focusRowAt index

    /// 重建后补聚焦（已聚焦则不动）
    member _.FocusRow(id: Guid) =
        Dispatcher.UIThread.Post(
            (fun () ->
                match rowHosts.TryGetValue id with
                | true, host when not host.IsFocused -> host.Focus NavigationMethod.Directional |> ignore
                | _ -> ()),
            DispatcherPriority.Input)

    member _.FocusArchivedToggle() = focusToggle ()

    /// 只滚屏不抢焦点
    member _.ScrollTo(id: Guid) =
        match flatIndex.TryGetValue id with
        | true, fi -> list.ScrollIntoView fi
        | _ -> ()

    /// 当前对话、选中判定与多选模式；已渲染的行与更多键立即重绘
    member _.SetMarks(active: Guid option, selected: Guid -> bool, inSelection: bool) =
        activeId <- active
        isSelected <- selected
        selectionActive <- inSelection
        for kv in rowHosts do applyState kv.Key kv.Value
        for b in moreButtons.Values do applyMore b false

    member _.SetCompact(value: bool) =
        compact <- value
        for kv in moreButtons do
            square kv.Value
            let lit =
                kv.Value.IsPointerOver || kv.Value.IsFocused
                || (match rowHosts.TryGetValue kv.Key with
                    | true, h -> h.IsPointerOver || h.IsFocused
                    | _ -> false)
            applyMore kv.Value lit

    [<CLIEvent>] member _.RowActivated = rowActivated.Publish
    [<CLIEvent>] member _.RowFocused = rowFocused.Publish
    /// (id, 锚点, 右对齐)；多选模式下不发
    [<CLIEvent>] member _.MenuRequested = menuRequested.Publish
    [<CLIEvent>] member _.RenameKey = renameKey.Publish
    [<CLIEvent>] member _.DeleteKey = deleteKey.Publish
    [<CLIEvent>] member _.PinKey = pinKey.Publish
    [<CLIEvent>] member _.SelectAllKey = selectAllKey.Publish
    [<CLIEvent>] member _.Escape = escape.Publish
    /// 首行再往上（或没有行时）：交还搜索框
    [<CLIEvent>] member _.LeaveTop = leaveTop.Publish
    [<CLIEvent>] member _.HitClicked = hitClicked.Publish
    [<CLIEvent>] member _.StatusActionClicked = statusAction.Publish
    [<CLIEvent>] member _.ArchivedToggleClicked = toggleClicked.Publish
