namespace Wanxiang2.SidebarDesk

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.WorkspaceState
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.ConversationRows
open Wanxiang2.SidebarSearch
open Wanxiang2.BatchBar
open Wanxiang2.HostSwitcher
open Wanxiang2.SidebarBrand
open Wanxiang2.ListEmptyCard

/// 侧栏交给宿主的动作（旧 SidebarActions，字段逐一对应）。
type SidebarDeskActions =
    { newConversation: unit -> unit
      openConversation: Guid -> unit
      renameConversation: ConversationSummary -> unit
      deleteConversation: ConversationSummary -> unit
      setPinned: ConversationSummary -> bool -> unit
      setArchived: ConversationSummary -> bool -> unit
      /// 选中的全部对话是否都已置顶（true 时置顶键变成取消置顶）
      selectionAllPinned: Guid list -> bool
      setPinnedMany: Guid list -> bool -> unit
      setArchivedMany: Guid list -> bool -> unit
      deleteMany: Guid list -> unit
      duplicateAsFork: ConversationSummary -> unit
      exportConversation: ConversationSummary -> unit
      openSettings: unit -> unit
      reconnect: unit -> unit
      toggleArchivedVisibility: unit -> unit
      closeNavigation: unit -> unit }

/// 旧 Views/Sidebar.fs 的协作：分组与筛选（ConversationSummary）、消息正文搜索（SearchSession + SearchRequery）、
/// 行焦点（RowFocus）、多选（SidebarSelection）、空态（SidebarEmpty）、行菜单，摆进
/// SidebarBrand / SidebarSearch / ConversationRows / ListEmptyCard / BatchBar / HostSwitcher。
type SidebarDesk(theme: ThemeManager, overlay: OverlayDesk, actions: SidebarDeskActions, brandLogo: float -> Control) =
    let brand = SidebarBrand()
    let search = SidebarSearch()
    do ignore brandLogo
    let rows = ConversationRows()
    let empty = ListEmptyCard()
    let batch = BatchBar()
    let hostBar = HostSwitcher()
    let icon = SidebarLooks.menuIcon theme

    let messageSearch = SearchSession<SearchHit>()
    let rowFocus = RowFocus()
    let selection = SidebarSelection()
    let summaryById = Dictionary<Guid, ConversationSummary>()
    let hitByKey = Dictionary<string, SearchHit>()
    let mutable summaries: ConversationSummary list = []
    let mutable lineage: Map<Guid, ForkLineage> = Map.empty
    let mutable activeId: Guid option = None
    let mutable showArchived = false
    let mutable connected = false
    let mutable listLoading = false
    let mutable projectedEmpty: SidebarEmptyKind option = None
    let mutable compact = false
    let mutable presetEntries: (string * string * (unit -> unit)) list = []
    let mutable presetMenu: (Control -> (string * string * (unit -> unit)) list -> unit) option = None
    let mutable paletteHandler: (unit -> unit) option = None
    let mutable searchHandler: (string -> bool -> int -> unit) option = None
    let mutable openSearchHit: SearchHit -> unit = ignore

    let root = Border()

    let live (id: Guid) (fallback: ConversationSummary) =
        match summaryById.TryGetValue id with
        | true, s -> s
        | _ -> fallback

    let spansOf (query: string) (text: string) =
        SidebarText.segments query text |> List.map (fun s -> { Text = s.text; IsMatch = s.isMatch })

    // 每次重建前按标题键分组一次：同名判断只看同组，不再每行扫全表（2 万行时由平方降为线性）。
    let sameTitle = Dictionary<string, ConversationSummary list>()
    let titleKey (t: string) = (if isNull t then "" else t.TrimEnd('…').Trim())
    let indexTitles () =
        sameTitle.Clear()
        for s in summaries do
            let k = titleKey s.title
            sameTitle[k] <- s :: (match sameTitle.TryGetValue k with | true, l -> l | _ -> [])
    let disambiguate (s: ConversationSummary) =
        match sameTitle.TryGetValue(titleKey s.title) with
        | true, group -> ConversationSummary.disambiguator group DateTimeOffset.Now s
        | _ -> None

    /// 预览去掉与标题重复的前缀；分叉加前缀；同名对话加区分时间
    let previewFor (s: ConversationSummary) =
        let withoutTitle =
            let preview = if isNull s.preview then "" else s.preview.Trim()
            let title = s.title.TrimEnd('…').Trim()
            if title.Length >= 4 && preview.StartsWith(title, StringComparison.Ordinal) then
                preview.Substring(title.Length).TrimStart(' ', '·', '—', '-', '，', '。')
            else preview
        let body =
            if not (String.IsNullOrWhiteSpace withoutTitle) then withoutTitle
            elif s.messageCount > 0 then sprintf "%d 条消息" s.messageCount
            else "还没有消息"
        let body =
            match disambiguate s with
            | Some stamp -> stamp + " · " + body
            | None -> body
        if s.isFork || lineage.ContainsKey s.id then "分叉 · " + body else body

    let accessibleName (s: ConversationSummary) =
        match disambiguate s with
        | Some stamp -> sprintf "%s（%s）" s.title stamp
        | None -> s.title

    let tooltipFor (s: ConversationSummary) =
        let provenance =
            // 父对话经 id 索引取，只把它交给 breadcrumb（它只在表里找父对话）。
            let parent = lineage.TryFind s.id |> Option.bind (fun l -> match summaryById.TryGetValue l.parentId with | true, p -> Some p | _ -> None)
            match ForkLineage.breadcrumb (Option.toList parent) lineage s.id with
            | Some crumb -> "\n" + crumb.text
            | None -> if s.isFork then " · 分叉" else ""
        sprintf "%s\n%d 条消息%s" s.title s.messageCount provenance

    let rowModel (query: string) (s: ConversationSummary) : RowModel =
        let preview = previewFor s
        { Id = s.id; Title = s.title; TitleSpans = spansOf query s.title; Preview = preview
          PreviewSpans = spansOf query preview; AccessibleName = accessibleName s; Tooltip = tooltipFor s
          Running = s.running; Pinned = s.pinned; Archived = s.archived }

    let hitKey (h: SearchHit) = sprintf "%O/%d" h.conversationId h.messageId

    let hitModel (h: SearchHit) : HitModel =
        let before, matched, after = SearchHit.splitPlain h
        { Key = hitKey h
          Title = (if h.archived then h.title + " · 已归档" else h.title)
          Before = before; Matched = matched; After = after
          AccessibleName = sprintf "消息命中：%s，%s" h.title h.snippet }

    let marks () = rows.SetMarks(activeId, selection.IsSelected, selection.Active)

    let rec rebuild () =
        // 焦点在某行上就记住，重建后仍可见则还回去；在搜索框里打字时不抢焦点
        let restore = rows.FocusedRow
        summaryById.Clear()
        for s in summaries do summaryById[s.id] <- s
        indexTitles ()
        let query = search.Text
        let visible =
            summaries
            |> List.filter (fun s -> showArchived || not s.archived)
            |> List.filter (ConversationSummary.matches query)
        let items = ResizeArray<RowItem>()
        for g in ConversationSummary.group DateTimeOffset.Now visible do
            items.Add(RowItem.Header g.label)
            for s in g.items do items.Add(RowItem.Conversation(rowModel query s))
        hitByKey.Clear()
        let section = messageSearch.Section query
        match section with
        | Some(hits, lines) ->
            items.Add(RowItem.Header "消息内容")
            for h in hits do
                hitByKey[hitKey h] <- h
                items.Add(RowItem.Hit(hitModel h))
            for line in lines do
                let action =
                    line.action
                    |> Option.map (fun a ->
                        SearchStatusAction.label a,
                        match a with
                        | SearchStatusAction.Retry -> "retry"
                        | SearchStatusAction.LoadMore -> "more")
                items.Add(RowItem.Status(line.text, action))
        | None -> ()
        let archivedCount = summaries |> List.filter (fun s -> s.archived) |> List.length
        if archivedCount > 0 then items.Add(RowItem.ArchivedToggle(showArchived, archivedCount))
        rows.Render(List.ofSeq items)
        marks ()
        rowFocus.SetVisible(Array.ofList rows.VisibleIds)
        rowFocus.RestoreAfterRebuild restore |> Option.iter rows.FocusRow
        let searchEmpty = not (String.IsNullOrWhiteSpace query) && List.isEmpty visible && section.IsNone
        search.SetEmptyHint searchEmpty
        let emptyKind =
            match projectedEmpty with
            | Some SidebarEmptyKind.Loading -> Some SidebarEmptyKind.Loading
            | _ ->
            if not connected then Some (SidebarEmptyKind.NotConnected compact)
            elif listLoading then Some SidebarEmptyKind.Loading
            elif List.isEmpty visible && section.IsNone then
                if not (String.IsNullOrWhiteSpace query) then Some (SidebarEmptyKind.NoSearchResults query)
                else Some SidebarEmptyKind.NoConversations
            else None
        empty.Render emptyKind

    let selectedIds () = selection.SelectedIds rows.VisibleIds

    let refreshChrome () =
        let c = selection.Chrome(rows.VisibleIds, actions.selectionAllPinned)
        let batchVS =
            if c.barsVisible then
                Some {
                    SelectedCount = selectedIds().Length
                    AllSelected = c.selectAllText = "取消全选"
                    Busy = not c.batchEnabled
                    AllPinned = c.pinText = "取消置顶"
                }
            else None
        batch.Render batchVS
        marks ()
        let brandView = brand.View
        brandView.IsVisible <- not selection.Active
        hostBar.View.IsVisible <- not selection.Active

    let focusSearch (selectAll: bool) = search.Focus selectAll

    let resetSearch (focus: bool) =
        if not (String.IsNullOrEmpty search.Text) then search.Text <- ""
        search.StopDebounce()
        rebuild ()
        if focus then focusSearch true

    let exitSelection () =
        selection.Exit()
        refreshChrome ()

    let toggleSelected id =
        selection.Toggle id
        refreshChrome ()

    let enterSelection id =
        selection.Enter id
        refreshChrome ()

    let toggleAll () =
        selection.ToggleAllVisible rows.VisibleIds
        refreshChrome ()

    let deleteSelected () =
        // 空选择不进删除流
        let ids = selectedIds ()
        if not (List.isEmpty ids) then actions.deleteMany ids

    let focusActive () =
        let ids = rows.VisibleIds
        match activeId with
        | Some id ->
            match List.tryFindIndex ((=) id) ids with
            | Some i -> rows.FocusRowAt i
            | None -> rows.RowHost id |> Option.iter (fun h -> h.Focus NavigationMethod.Directional |> ignore)
        | None -> if not (List.isEmpty ids) then rows.FocusRowAt 0

    let beginSearch () =
        // 手动触发或防抖到期后停掉在途防抖：迟到的 Tick 会清空刚到的命中
        search.StopDebounce()
        match messageSearch.Begin(search.Text, searchHandler.IsSome && connected), searchHandler with
        | Some q, Some handler ->
            rebuild ()
            handler q showArchived 0
        | _ -> ()

    let resend (next: unit -> (string * int) option) =
        match searchHandler with
        | Some handler ->
            next ()
            |> Option.iter (fun (q, offset) ->
                rebuild ()
                handler q showArchived offset)
        | None -> ()

    let openMenu (id: Guid) (anchor: Control) (alignRight: bool) =
        // 多选下不开单条菜单；条目按 id 取最新快照
        if not selection.Active then
            match summaryById.TryGetValue id with
            | true, s ->
                overlay.ShowMenu(
                    anchor,
                    alignRight,
                    [ PopupEntry.create "重命名" (fun () -> actions.renameConversation (live id s))
                      |> PopupEntry.withHint "F2" |> PopupEntry.withIcon (icon "pencil")
                      PopupEntry.create "多选" (fun () -> enterSelection id) |> PopupEntry.withIcon (icon "check")
                      PopupEntry.create (if s.pinned then "取消置顶" else "置顶") (fun () -> actions.setPinned s (not s.pinned))
                      |> PopupEntry.withHint "P" |> PopupEntry.withIcon (icon "pin")
                      PopupEntry.create (if s.archived then "取消归档" else "归档") (fun () -> actions.setArchived s (not s.archived))
                      |> PopupEntry.withIcon (icon "archive")
                      PopupEntry.create "从此分叉" (fun () -> actions.duplicateAsFork s) |> PopupEntry.withIcon (icon "fork")
                      PopupEntry.create "导出（Markdown / JSON）…" (fun () -> actions.exportConversation s)
                      |> PopupEntry.withIcon (icon "download")
                      PopupEntry.create "删除" (fun () -> actions.deleteConversation s)
                      |> PopupEntry.withHint "Delete" |> PopupEntry.withIcon (icon "trash") |> PopupEntry.asDanger ])
            | _ -> ()

    let withLive id (f: ConversationSummary -> unit) =
        match summaryById.TryGetValue id with
        | true, s -> f s
        | _ -> ()

    let view =
        root.Background <- theme.Brush PaletteKey.Rail
        root.BorderBrush <- theme.Brush PaletteKey.BorderSoft
        (theme.Metric Metric.SidebarDividerWidth :> IObservable<float>).Subscribe(fun v -> root.BorderThickness <- Thickness(0.0, 0.0, v, 0.0)) |> ignore
        AutomationProperties.SetName(rows.List, "对话列表")
        let body = Grid()
        body.Children.Add rows.View
        body.Children.Add empty.View
        body.Children.Add search.EmptyHint
        let layout = DockPanel()
        for c, d in [ brand.View, Dock.Top; search.View, Dock.Top; batch.Header, Dock.Top; batch.ActionBar, Dock.Bottom; hostBar.View, Dock.Bottom ] do
            DockPanel.SetDock(c, d)
            layout.Children.Add c
        layout.Children.Add body
        root.Child <- layout
        root

    do
        empty.ActionClicked.Add(fun () -> actions.reconnect ())
        brand.NewClicked.Add(fun () -> actions.newConversation ())
        brand.BackClicked.Add(fun () -> actions.closeNavigation ())
        brand.PresetClicked.Add(fun anchor ->
            match presetMenu with
            | Some show when not (List.isEmpty presetEntries) -> show anchor presetEntries
            | _ -> ())
        hostBar.ReconnectClicked.Add(fun () -> actions.reconnect ())
        hostBar.SettingsClicked.Add(fun () -> actions.openSettings ())
        hostBar.PaletteClicked.Add(fun () -> paletteHandler |> Option.iter (fun h -> h ()))
        batch.ExitClicked.Add(fun () -> exitSelection ())
        batch.SelectAllClicked.Add(fun () -> toggleAll ())
        batch.PinClicked.Add(fun () ->
            let ids = selectedIds ()
            actions.setPinnedMany ids (not (actions.selectionAllPinned ids)))
        batch.ArchiveClicked.Add(fun () -> actions.setArchivedMany (selectedIds ()) true)
        batch.DeleteClicked.Add(fun () -> deleteSelected ())
        search.ClearRequested.Add(fun () -> resetSearch true)
        search.Debounced.Add(fun text ->
            // 词没变（已为它发过查询）就不重发
            if text <> messageSearch.Query then beginSearch ()
            rebuild ())
        search.Keys.Add(fun e ->
            let ids = rows.VisibleIds
            match e.Key with
            | Key.Down ->
                if not (List.isEmpty ids) then e.Handled <- true; rows.FocusRowAt 0
                elif rows.HasArchivedToggle then e.Handled <- true; rows.FocusArchivedToggle()
                elif empty.IsVisible && empty.FocusAction() then e.Handled <- true
                elif connected && not listLoading && empty.IsVisible then
                    // 已连接零对话：向下仍须有键盘出口
                    e.Handled <- true
                    brand.NewButton.Focus NavigationMethod.Directional |> ignore
                elif search.EmptyHint.IsVisible then e.Handled <- true; search.FocusEmptyHint()
            | Key.Enter ->
                if not (List.isEmpty ids) then
                    e.Handled <- true
                    actions.openConversation ids.Head
                    // 桌面焦点回首行；紧凑态由宿主收抽屉
                    if not compact then rows.FocusRowAt 0
                elif search.EmptyHint.IsVisible then e.Handled <- true; search.FocusEmptyHint()
            | Key.Escape ->
                e.Handled <- true
                if not (String.IsNullOrEmpty search.Text) then resetSearch true
                elif selection.Active then exitSelection ()
                else focusActive ()
            | _ -> ())
        rows.RowActivated.Add(fun id -> if selection.Active then toggleSelected id else actions.openConversation id)
        rows.RowFocused.Add(fun id -> rowFocus.Focused id)
        rows.MenuRequested.Add(fun (id, anchor, right) -> openMenu id anchor right)
        rows.RenameKey.Add(fun id -> withLive id actions.renameConversation)
        rows.DeleteKey.Add(fun id ->
            // 多选下 Delete 走批量删除
            if selection.Active then deleteSelected () else withLive id actions.deleteConversation)
        rows.PinKey.Add(fun id -> withLive id (fun s -> actions.setPinned s (not s.pinned)))
        rows.SelectAllKey.Add(fun () -> toggleAll ())
        rows.LeaveTop.Add(fun () -> focusSearch false)
        rows.Escape.Add(fun e ->
            // 行在多选中：Esc 只退多选；其余先清搜索词，再退多选
            if e.Kind = "row" && selection.Active then e.Handled <- true; exitSelection ()
            elif not (String.IsNullOrEmpty search.Text) then e.Handled <- true; resetSearch true
            elif selection.Active then e.Handled <- true; exitSelection ())
        rows.HitClicked.Add(fun key ->
            match hitByKey.TryGetValue key with
            | true, h -> openSearchHit h
            | _ -> ())
        rows.StatusActionClicked.Add(fun key ->
            match key with
            | "retry" -> resend messageSearch.Retry
            | "more" -> resend messageSearch.LoadMore
            | _ -> ())
        rows.ArchivedToggleClicked.Add(fun () -> actions.toggleArchivedVisibility ())
        // 紧凑抽屉：Tab 只在侧栏内循环
        root.KeyDown.Add(fun e -> if compact && e.Key = Key.Tab then overlay.TrapTab(root :> Control, e))
        refreshChrome ()
        rebuild ()

    member _.View = view :> Control
    member _.Rows = rows
    member _.Search = search
    member _.Batch = batch
    member _.Host = hostBar
    member _.Brand = brand
    member _.EmptyCard = empty

    member _.ResetSearch(?focusSearch: bool) = resetSearch (defaultArg focusSearch true)

    /// 发起查询：(查询词, 含归档, 跳过条数)。未接线时不发查询、也不显示「搜索中」。
    member _.SearchHandler
        with get () = searchHandler
        and set v = searchHandler <- v
    /// 点击一条命中：打开对话并滚动到该消息
    member _.OpenSearchHit
        with get () = openSearchHit
        and set v = openSearchHit <- v

    /// 立即发起消息搜索（回车或测试用；平时由防抖触发）
    member _.BeginMessageSearch() = beginSearch ()

    /// 一页结果；词已变的迟到结果丢弃；offset > 0 是追加页
    member _.ApplySearchResults(page: SearchPageState, offset: int) =
        if messageSearch.Apply(page.query, page.hits, page.hasMore, page.truncated, offset) then rebuild ()

    member _.ApplySearchFailure(query: string, message: string) =
        if messageSearch.Fail(query, message) then rebuild ()

    member _.SearchQueryText = search.Text.Trim()
    member _.MessageHits: SearchHit list = messageSearch.Hits

    /// 导入入口已移到设置（决策 363），这里只保留接口
    member _.ConfigureImport(_handler: unit -> unit) = ()

    member _.ConfigurePalette(handler: unit -> unit) =
        paletteHandler <- Some handler
        hostBar.ConfigurePalette true

    member _.PaletteButton = hostBar.PaletteButton

    /// 预设下拉插槽：空列表时按钮隐藏
    member _.SlotPresets(entries: (string * string * (unit -> unit)) list, show: Control -> (string * string * (unit -> unit)) list -> unit) =
        presetEntries <- entries
        presetMenu <- Some show
        brand.SetPresetsVisible(not (List.isEmpty entries))

    member _.PresetMenuButton = brand.PresetButton

    /// 命令面板把全文搜索交给侧栏（一次性命令）
    member _.ApplySearchText(text: string) = search.Text <- text

    /// 谱系随快照更新插槽
    member _.SlotLineage(value: Map<Guid, ForkLineage>) = lineage <- value

    member _.ConnectionText = hostBar.ConnectionText

    /// 单入口幂等渲染：由 SidebarVS 投影全量驱动侧栏
    member this.Render(vs: SidebarVS, ?activeConversationId: Guid option, ?customSummaries: ConversationSummary list) =
        let hadSummaries = customSummaries.IsSome
        connected <- vs.Host.Connected
        compact <- vs.Compact
        if empty.Style = Some EmptyCardStyle.Note then empty.SetActionVisible vs.Compact
        brand.SetCompact vs.Compact
        search.SetCompact vs.Compact
        hostBar.SetCompact vs.Compact
        rows.SetCompact vs.Compact
        hostBar.SetServerUrl vs.Host.ServerUrl
        hostBar.SetConnection(vs.Host.Connected, vs.Host.StatusText)
        if not vs.Host.Connected then messageSearch.Disconnected()
        match customSummaries with
        | Some s -> summaries <- s
        | None -> ()
        listLoading <- match vs.Empty with SidebarEmptyKind.Loading -> true | _ -> false
        projectedEmpty <-
            match vs.Empty with
            | SidebarEmptyKind.Loading -> Some SidebarEmptyKind.Loading
            | _ -> None
        let nextActive = defaultArg activeConversationId activeId
        let activeChanged = activeId <> nextActive
        if activeChanged then
            activeId <- nextActive
        let onlyActiveChanged = activeChanged && not hadSummaries && not listLoading && vs.Empty <> SidebarEmptyKind.Loading
        if onlyActiveChanged then
            marks ()
        else
            rebuild ()
        if activeChanged then
            nextActive |> Option.iter rows.ScrollTo
        if selection.Active then refreshChrome ()

    member _.FocusSearch(?selectAll: bool) = focusSearch (defaultArg selectAll true)

    member _.ApplyServerUrl(url: string) = hostBar.SetServerUrl url

    /// 当前从上到下的对话行 id（Ctrl+1..9 用）
    member _.GetVisibleOrder() : string list = rows.VisibleIds |> List.map string

    member _.NeighborAfterDelete(removedId: string) : Guid option = rowFocus.NeighborAfterDelete removedId

    member _.FocusAfterDelete(removedId: string) =
        match rowFocus.FocusAfterDelete removedId with
        | RowFocusTarget.Row(i, _) -> rows.FocusRowAt i
        | RowFocusTarget.Search -> focusSearch false

    member _.EnterSelection(seed: Guid) = enterSelection seed
    member _.ToggleSelected(id: Guid) = toggleSelected id
    member _.ExitSelection() = exitSelection ()
    member _.ToggleSelectAllVisible() = toggleAll ()
    member _.SelectedIds() : Guid list = selectedIds ()
    member _.IsSelectionMode = selection.Active
