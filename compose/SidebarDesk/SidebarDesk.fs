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
    {
        newConversation: unit -> unit
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
        /// 进入多选并选中第一项（多选的事实在状态树，这里只是把意图交给宿主）
        enterSelection: Guid -> unit
        /// 切换一行的选中态
        toggleSelected: Guid -> unit
        /// 退出多选
        exitSelection: unit -> unit
        /// 全选可见行（已全选则退出多选）
        toggleSelectAllVisible: Guid list -> unit
        duplicateAsFork: ConversationSummary -> unit
        exportConversation: ConversationSummary -> unit
        openSettings: unit -> unit
        reconnect: unit -> unit
        toggleArchivedVisibility: unit -> unit
        closeNavigation: unit -> unit
    }

/// 旧 Views/Sidebar.fs 的协作：分组与筛选（ConversationSummary）、消息正文搜索（SearchSession + SearchRequery）、
/// 行焦点（RowFocus）、多选（投影里的选中集合，见 SidebarSelection）、空态（SidebarEmpty）、行菜单，摆进
/// SidebarBrand / SidebarSearch / ConversationRows / ListEmptyCard / BatchBar / HostSwitcher。
type SidebarDesk(theme: ThemeManager, overlay: OverlayDesk, actions: SidebarDeskActions, brandLogo: float -> Control) =
    let brand = SidebarBrand()
    // 品牌标：入口壳给的是真 logo 工厂（SharedTheme.logo），按侧栏边长档接入一次。
    do brand.SlotLogo(brandLogo (theme.Metric(Metric.LogoSidebar).Value))
    let search = SidebarSearch()
    let rows = ConversationRows()
    let empty = ListEmptyCard()
    let batch = BatchBar()
    let hostBar = HostSwitcher()
    let icon = SidebarLooks.menuIcon theme

    let messageSearch = SearchSession<SearchHit>()
    let rowFocus = RowFocus()
    let summaryById = Dictionary<Guid, ConversationSummary>()

    let mutable lastVS: SidebarVS =
        let initVS =
            (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Sidebar

        { initVS with
            Host =
                { initVS.Host with
                    Connected = false
                    StatusText = "未连接"
                }
        }

    let mutable compact = false
    let mutable presetEntries: (string * string * (unit -> unit)) list = []
    /// 宿主接了预设下拉插槽时的可见性（Some = 插槽说了算；None = 跟模型投影走）。
    let mutable presetVisible: bool option = None

    let mutable presetMenu: (Control -> (string * string * (unit -> unit)) list -> unit) option =
        None

    let mutable paletteHandler: (unit -> unit) option = None
    let mutable searchHandler: (string -> bool -> int -> unit) option = None
    let mutable openSearchHit: SearchHit -> unit = ignore

    let root = Border()

    let currentActiveId () = lastVS.ActiveId
    let currentConnected () = lastVS.Host.Connected
    let currentShowArchived () = lastVS.ShowArchived
    let currentLineage () = lastVS.Lineage
    let currentRows () = lastVS.Rows
    /// 多选的事实**只来自投影**（`SidebarVS.SelectedRowIds`）；侧栏不再自己记一份选择集合。
    let selectedSet () = lastVS.SelectedRowIds
    let inSelectionMode () = lastVS.BatchMode

    let isSelected (id: Guid) =
        inSelectionMode () && selectedSet () |> Set.contains id

    let isListLoading () =
        match lastVS.Empty with
        | SidebarEmptyKind.Loading -> true
        | _ -> false

    let live (id: Guid) (fallback: ConversationSummary) =
        match summaryById.TryGetValue id with
        | true, s -> s
        | _ -> fallback

    let spansOf (query: string) (text: string) =
        SidebarText.segments query text
        |> List.map (fun s -> { Text = s.text; IsMatch = s.isMatch })

    // 每次重建前按标题键分组一次：同名判断只看同组，不再每行扫全表（2 万行时由平方降为线性）。
    let sameTitle = Dictionary<string, ConversationSummary list>()

    let titleKey (t: string) =
        (if isNull t then "" else t.TrimEnd('…').Trim())

    let indexTitles (items: ConversationSummary list) =
        sameTitle.Clear()

        for s in items do
            let k = titleKey s.title

            sameTitle[k] <-
                s
                :: (match sameTitle.TryGetValue k with
                    | true, l -> l
                    | _ -> [])

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
            else
                preview

        let body =
            if not (String.IsNullOrWhiteSpace withoutTitle) then
                withoutTitle
            elif s.messageCount > 0 then
                sprintf "%d 条消息" s.messageCount
            else
                "还没有消息"

        let body =
            match disambiguate s with
            | Some stamp -> stamp + " · " + body
            | None -> body

        if s.isFork || (currentLineage ()).ContainsKey s.id then
            "分叉 · " + body
        else
            body

    let accessibleName (s: ConversationSummary) =
        match disambiguate s with
        | Some stamp -> sprintf "%s（%s）" s.title stamp
        | None -> s.title

    let tooltipFor (s: ConversationSummary) =
        let provenance =
            // 父对话经 id 索引取，只把它交给 breadcrumb（它只在表里找父对话）。
            let parent =
                (currentLineage ()).TryFind s.id
                |> Option.bind (fun l ->
                    match summaryById.TryGetValue l.parentId with
                    | true, p -> Some p
                    | _ -> None)

            match ForkLineage.breadcrumb (Option.toList parent) (currentLineage ()) s.id with
            | Some crumb -> "\n" + crumb.text
            | None -> if s.isFork then " · 分叉" else ""

        sprintf "%s\n%d 条消息%s" s.title s.messageCount provenance

    let rowModel (query: string) (s: ConversationSummary) : RowModel =
        let preview = previewFor s

        {
            Id = s.id
            Title = s.title
            TitleSpans = spansOf query s.title
            Preview = preview
            PreviewSpans = spansOf query preview
            AccessibleName = accessibleName s
            Tooltip = tooltipFor s
            Running = s.running
            Pinned = s.pinned
            Archived = s.archived
        }

    let hitKey (h: SearchHit) =
        sprintf "%O/%d" h.conversationId h.messageId

    let findHitByKey (key: string) : SearchHit option =
        messageSearch.Hits |> List.tryFind (fun h -> hitKey h = key)

    let hitModel (h: SearchHit) : HitModel =
        let before, matched, after = SearchHit.splitPlain h

        {
            Key = hitKey h
            Title = (if h.archived then h.title + " · 已归档" else h.title)
            Before = before
            Matched = matched
            After = after
            AccessibleName = sprintf "消息命中：%s，%s" h.title h.snippet
        }

    let marks () =
        rows.ApplyMarks(currentActiveId (), isSelected, inSelectionMode ())

    let mutable renderedQuery = ""

    let rec rebuild () =
        // 焦点在某行上就记住，重建后仍可见则还回去；在搜索框里打字时不抢焦点
        let restore = rows.FocusedRow
        // 行即事实：SidebarRowVS 携带摘要的全部字段，这里只做投影，不再合成缺数据的行。
        let summaries =
            currentRows ()
            |> List.map (fun r ->
                {
                    id = r.Id
                    title = r.Title
                    preview = r.Preview
                    running = r.Running
                    pinned = r.Pinned
                    archived = r.Archived
                    createdAt = r.CreatedAt
                    updatedAt = r.UpdatedAt
                    messageCount = r.MessageCount
                    isFork = r.IsFork
                    providerId = r.ProviderId
                    model = r.Model
                    lastCommitId = r.LastCommitId
                })

        summaryById.Clear()

        for s in summaries do
            summaryById[s.id] <- s

        indexTitles summaries
        let query = search.Text
        renderedQuery <- query
        let showArchived = currentShowArchived ()

        let visible =
            summaries
            |> List.filter (fun s -> showArchived || not s.archived)
            |> List.filter (ConversationSummary.matches query)

        let items = ResizeArray<RowItem>()

        for g in ConversationSummary.group DateTimeOffset.Now visible do
            items.Add(RowItem.Header g.label)

            for s in g.items do
                items.Add(RowItem.Conversation(rowModel query s))

        let section = messageSearch.Section query

        match section with
        | Some(hits, lines) ->
            items.Add(RowItem.Header "消息内容")

            for h in hits do
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

        if archivedCount > 0 then
            items.Add(RowItem.ArchivedToggle(showArchived, archivedCount))

        rows.Render(List.ofSeq items)
        marks ()
        rowFocus.ApplyVisible(Array.ofList rows.VisibleIds)
        rowFocus.RestoreAfterRebuild restore |> Option.iter rows.FocusRow

        let searchEmpty =
            not (String.IsNullOrWhiteSpace query) && List.isEmpty visible && section.IsNone

        search.ApplyEmptyHint searchEmpty

        let emptyKind =
            match lastVS.Empty with
            | SidebarEmptyKind.Loading -> Some SidebarEmptyKind.Loading
            | _ ->
                if not (currentConnected ()) then
                    Some(SidebarEmptyKind.NotConnected compact)
                elif isListLoading () then
                    Some SidebarEmptyKind.Loading
                elif List.isEmpty visible && section.IsNone && List.isEmpty (currentRows ()) then
                    if not (String.IsNullOrWhiteSpace query) then
                        Some(SidebarEmptyKind.NoSearchResults query)
                    else
                        Some SidebarEmptyKind.NoConversations
                else
                    None

        empty.Render emptyKind

    /// 可见的已选项，按可见行次序（批量动作只作用于当前可见的选中项）。
    /// 选中集合来自投影（唯一事实），这里只按可见行过滤。
    let selectedIds () =
        SidebarSelection.SelectedIds rows.VisibleIds (selectedSet ())

    let refreshChrome () =
        // 批量条的渲染参数：计数与「全选 / 取消全选」按**可见行**算（可见行是侧栏的：
        // 搜索框与归档开关在这里）；「是否多选中」与选中集合来自投影。
        let visible = rows.VisibleIds
        let usable = selectedIds ()

        let allVisibleSelected =
            not (List.isEmpty visible)
            && visible |> List.forall (fun id -> selectedSet () |> Set.contains id)

        let allPinned = actions.selectionAllPinned usable

        let batchVS =
            if inSelectionMode () then
                Some
                    {
                        SelectedCount = usable.Length
                        AllSelected = allVisibleSelected
                        Busy = List.isEmpty usable
                        AllPinned = allPinned
                    }
            else
                None

        batch.Render batchVS
        marks ()
        let brandView = brand.View
        brandView.IsVisible <- not (inSelectionMode ())
        hostBar.View.IsVisible <- not (inSelectionMode ())

    let focusSearch (selectAll: bool) = search.Focus selectAll

    let resetSearch (focus: bool) =
        if not (String.IsNullOrEmpty search.Text) then
            search.Text <- ""

        search.StopDebounce()
        rebuild ()

        if focus then
            focusSearch true

    let exitSelection () =
        actions.exitSelection ()
        refreshChrome ()

    let toggleSelected id =
        actions.toggleSelected id
        refreshChrome ()

    let enterSelection id =
        actions.enterSelection id
        refreshChrome ()

    let toggleAll () =
        actions.toggleSelectAllVisible rows.VisibleIds
        refreshChrome ()

    let deleteSelected () =
        // 空选择不进删除流
        let ids = selectedIds ()

        if not (List.isEmpty ids) then
            actions.deleteMany ids

    let focusActive () =
        let ids = rows.VisibleIds

        match currentActiveId () with
        | Some id ->
            match List.tryFindIndex ((=) id) ids with
            | Some i -> rows.FocusRowAt i
            | None ->
                rows.RowHost id
                |> Option.iter (fun h -> h.Focus NavigationMethod.Directional |> ignore)
        | None ->
            if not (List.isEmpty ids) then
                rows.FocusRowAt 0

    let beginSearch () =
        // 手动触发或防抖到期后停掉在途防抖：迟到的 Tick 会清空刚到的命中
        search.StopDebounce()

        match messageSearch.Begin(search.Text, searchHandler.IsSome && currentConnected ()), searchHandler with
        | Some q, Some handler ->
            rebuild ()
            handler q (currentShowArchived ()) 0
        | _ -> ()

    let resend (next: unit -> (string * int) option) =
        match searchHandler with
        | Some handler ->
            next ()
            |> Option.iter (fun (q, offset) ->
                rebuild ()
                handler q (currentShowArchived ()) offset)
        | None -> ()

    let openMenu (id: Guid) (anchor: Control) (alignRight: bool) =
        // 多选下不开单条菜单；条目按 id 取最新快照
        if not (inSelectionMode ()) then
            match summaryById.TryGetValue id with
            | true, s ->
                overlay.ShowMenu(
                    anchor,
                    alignRight,
                    [
                        PopupEntry.create "重命名" (fun () -> actions.renameConversation (live id s))
                        |> PopupEntry.withHint "F2"
                        |> PopupEntry.withIcon (icon "pencil")
                        PopupEntry.create "多选" (fun () -> enterSelection id)
                        |> PopupEntry.withIcon (icon "check")
                        PopupEntry.create (if s.pinned then "取消置顶" else "置顶") (fun () ->
                            actions.setPinned s (not s.pinned))
                        |> PopupEntry.withHint "P"
                        |> PopupEntry.withIcon (icon "pin")
                        PopupEntry.create (if s.archived then "取消归档" else "归档") (fun () ->
                            actions.setArchived s (not s.archived))
                        |> PopupEntry.withIcon (icon "archive")
                        PopupEntry.create "从此分叉" (fun () -> actions.duplicateAsFork s)
                        |> PopupEntry.withIcon (icon "fork")
                        PopupEntry.create "导出（Markdown / JSON）…" (fun () -> actions.exportConversation s)
                        |> PopupEntry.withIcon (icon "download")
                        PopupEntry.create "删除" (fun () -> actions.deleteConversation s)
                        |> PopupEntry.withHint "Delete"
                        |> PopupEntry.withIcon (icon "trash")
                        |> PopupEntry.asDanger
                    ]
                )
            | _ -> ()

    let withLive id (f: ConversationSummary -> unit) =
        match summaryById.TryGetValue id with
        | true, s -> f s
        | _ -> ()

    let view =
        root.Background <- theme.Brush PaletteKey.Rail
        root.BorderBrush <- theme.Brush PaletteKey.BorderSoft

        (theme.Metric Metric.SidebarDividerWidth :> IObservable<float>)
            .Subscribe(fun v -> root.BorderThickness <- Thickness(0.0, 0.0, v, 0.0))
        |> ignore

        AutomationProperties.SetName(rows.List, "对话列表")
        let body = Grid()
        body.Children.Add rows.View
        body.Children.Add empty.View
        body.Children.Add search.EmptyHint
        let layout = DockPanel()

        for c, d in
            [
                brand.View, Dock.Top
                search.View, Dock.Top
                batch.Header, Dock.Top
                batch.ActionBar, Dock.Bottom
                hostBar.View, Dock.Bottom
            ] do
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
            if text <> messageSearch.Query then
                beginSearch ()

            rebuild ())

        search.Keys.Add(fun e ->
            let ids = rows.VisibleIds

            match e.Key with
            | Key.Down ->
                if not (List.isEmpty ids) then
                    e.Handled <- true
                    rows.FocusRowAt 0
                elif rows.HasArchivedToggle then
                    e.Handled <- true
                    rows.FocusArchivedToggle()
                elif empty.IsVisible && empty.FocusAction() then
                    e.Handled <- true
                elif currentConnected () && not (isListLoading ()) && empty.IsVisible then
                    // 已连接零对话：向下仍须有键盘出口
                    e.Handled <- true
                    brand.NewButton.Focus NavigationMethod.Directional |> ignore
                elif search.EmptyHint.IsVisible then
                    e.Handled <- true
                    search.FocusEmptyHint()
            | Key.Enter ->
                if not (List.isEmpty ids) then
                    e.Handled <- true
                    actions.openConversation ids.Head
                    // 桌面焦点回首行；紧凑态由宿主收抽屉
                    if not compact then
                        rows.FocusRowAt 0
                elif search.EmptyHint.IsVisible then
                    e.Handled <- true
                    search.FocusEmptyHint()
            | Key.Escape ->
                e.Handled <- true

                if not (String.IsNullOrEmpty search.Text) then
                    resetSearch true
                elif inSelectionMode () then
                    exitSelection ()
                else
                    focusActive ()
            | _ -> ())

        rows.RowActivated.Add(fun id ->
            if inSelectionMode () then
                toggleSelected id
            else
                actions.openConversation id)

        rows.RowFocused.Add(fun id -> rowFocus.Focused id)
        rows.MenuRequested.Add(fun (id, anchor, right) -> openMenu id anchor right)
        rows.RenameKey.Add(fun id -> withLive id actions.renameConversation)

        rows.DeleteKey.Add(fun id ->
            // 多选下 Delete 走批量删除
            if inSelectionMode () then
                deleteSelected ()
            else
                withLive id actions.deleteConversation)

        rows.PinKey.Add(fun id -> withLive id (fun s -> actions.setPinned s (not s.pinned)))
        rows.SelectAllKey.Add(fun () -> toggleAll ())
        rows.LeaveTop.Add(fun () -> focusSearch false)

        rows.Escape.Add(fun e ->
            // 行在多选中：Esc 只退多选；其余先清搜索词，再退多选
            if e.Kind = "row" && inSelectionMode () then
                e.Handled <- true
                exitSelection ()
            elif not (String.IsNullOrEmpty search.Text) then
                e.Handled <- true
                resetSearch true
            elif inSelectionMode () then
                e.Handled <- true
                exitSelection ())

        rows.HitClicked.Add(fun key ->
            match findHitByKey key with
            | Some h -> openSearchHit h
            | _ -> ())

        rows.StatusActionClicked.Add(fun key ->
            match key with
            | "retry" -> resend messageSearch.Retry
            | "more" -> resend messageSearch.LoadMore
            | _ -> ())

        rows.ArchivedToggleClicked.Add(fun () -> actions.toggleArchivedVisibility ())
        // 紧凑抽屉：Tab 只在侧栏内循环
        root.KeyDown.Add(fun e ->
            if compact && e.Key = Key.Tab then
                overlay.TrapTab(root :> Control, e))

        refreshChrome ()
        rebuild ()

    member _.View = view :> Control
    member _.Rows = rows
    member _.Search = search
    member _.Batch = batch
    member _.Host = hostBar
    member _.Brand = brand
    member _.EmptyCard = empty

    member _.ResetSearch(?focusSearch: bool) =
        resetSearch (defaultArg focusSearch true)

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
        if messageSearch.Apply(page.query, page.hits, page.hasMore, page.truncated, offset) then
            rebuild ()

    member _.ApplySearchFailure(query: string, message: string) =
        if messageSearch.Fail(query, message) then
            rebuild ()

    member _.SearchQueryText = search.Text.Trim()
    member _.MessageHits: SearchHit list = messageSearch.Hits

    /// 导入入口已移到设置（决策 363），这里只保留接口
    member _.ConfigureImport(_handler: unit -> unit) = ()

    /// 命令面板入口插槽：宿主注入打开面板的动作，页脚面板键随之出现。
    member _.SlotPalette(handler: unit -> unit) =
        paletteHandler <- Some handler
        hostBar.SlotPalette true

    member _.PaletteButton = hostBar.PaletteButton

    /// 预设下拉插槽：空列表时按钮隐藏
    member _.SlotPresets
        (
            entries: (string * string * (unit -> unit)) list,
            show: Control -> (string * string * (unit -> unit)) list -> unit
        ) =
        presetEntries <- entries
        presetMenu <- Some show
        // 预设可见性：宿主接了插槽就以插槽为准（模型投影里的目录预设只是兜底），
        // 写进投影再渲染，不旁路写控件。
        presetVisible <- Some(not (List.isEmpty entries))

        lastVS <-
            { lastVS with
                Brand =
                    { lastVS.Brand with
                        PresetsVisible = presetVisible.Value
                    }
            }

        brand.Render lastVS.Brand

    member _.PresetMenuButton = brand.PresetButton

    /// 命令面板把全文搜索交给侧栏（一次性命令）
    member _.ApplySearchText(text: string) = search.Text <- text

    member _.ConnectionText = hostBar.ConnectionText

    /// 单入口幂等渲染：SidebarVS 是唯一输入（行、活动对话、归档可见性、分叉面包屑都在投影里）。
    /// 未连接时的空行不是权威陈述（模型断线后仍保留摘要，等重连快照）：
    /// 换紧凑态之类的重投影拿初始模型投影进来时，列表绝不该闪成空白。
    member this.Render(vs: SidebarVS) =
        let effectiveRows =
            if not vs.Host.Connected && List.isEmpty vs.Rows && not (List.isEmpty lastVS.Rows) then
                lastVS.Rows
            else
                vs.Rows
        // 紧凑态在侧栏级投影里，品牌行的紧凑档由它派生（Brand.Compact 随投影走，不旁路写控件）。
        // 预设可见性：宿主接了插槽就以插槽为准，否则跟模型投影走。
        let brandVS =
            { vs.Brand with
                Compact = vs.Compact
                PresetsVisible = presetVisible |> Option.defaultValue vs.Brand.PresetsVisible
            }

        let nextVS =
            { vs with
                Rows = effectiveRows
                Brand = brandVS
            }

        let nextActive = nextVS.ActiveId
        let prevVS = lastVS
        lastVS <- nextVS

        compact <- vs.Compact
        brand.Render nextVS.Brand
        hostBar.Render nextVS.Host

        if not vs.Host.Connected then
            messageSearch.Disconnected()

        let activeChanged = prevVS.ActiveId <> nextVS.ActiveId
        let rowsChanged = prevVS.Rows <> nextVS.Rows
        let emptyChanged = prevVS.Empty <> nextVS.Empty
        let showArchivedChanged = prevVS.ShowArchived <> nextVS.ShowArchived
        let lineageChanged = prevVS.Lineage <> nextVS.Lineage
        let connChanged = prevVS.Host.Connected <> nextVS.Host.Connected
        let compactChanged = prevVS.Compact <> nextVS.Compact

        let listChanged =
            rowsChanged
            || emptyChanged
            || showArchivedChanged
            || lineageChanged
            || connChanged
            || compactChanged
            || renderedQuery <> search.Text

        // 无变化的 Tick 不能替换按钮，否则按下与释放之间的重建会取消点击。
        if listChanged then rebuild () else marks ()

        if activeChanged then
            nextActive |> Option.iter rows.ScrollTo

        if inSelectionMode () then
            refreshChrome ()

    member _.FocusSearch(?selectAll: bool) = focusSearch (defaultArg selectAll true)

    /// 服务器 URL 命令：改投影值再渲染（命令入口，命名避开 Set*）。
    member _.ApplyServerUrl(url: string) =
        lastVS <-
            { lastVS with
                Host = { lastVS.Host with ServerUrl = url }
            }

        hostBar.Render lastVS.Host

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
    member _.IsSelectionMode = inSelectionMode ()
