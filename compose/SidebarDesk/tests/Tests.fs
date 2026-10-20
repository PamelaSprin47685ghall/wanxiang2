module Wanxiang2.SidebarDesk.Tests.Tests

// 真实组合：主题管理者给外观、OverlayDesk 出菜单，侧栏各控件经 SidebarDesk 协作。测试名沿用旧仓库。

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.DeskTests

let private all (root: Visual) = root.GetVisualDescendants() |> Seq.choose (function :? Control as c -> Some c | _ -> None) |> List.ofSeq
let private byName (root: Visual) name = all root |> List.tryFind (fun c -> AutomationProperties.GetName c = name)
let private pump () = Dispatcher.UIThread.RunJobs()
let private invoke (c: Control) =
    match c with
    | :? Button as b ->
        b.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, b))
    | :? Border as border ->
        border.Focus() |> ignore
        border.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.None, Source = border))
    | _ ->
        let peer = ControlAutomationPeer.CreatePeerForElement c
        (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    pump ()
let private press (c: Control) (k: Key) (mods: KeyModifiers) =
    c.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = mods, Source = c))
    pump ()
let private key c k = press c k KeyModifiers.None

let private now = DateTimeOffset.Now
let private conv (title: string) =
    { id = Guid.NewGuid(); title = title; preview = "预览 " + title; running = false; pinned = false; archived = false
      createdAt = now; updatedAt = now; messageCount = 2; isFork = false; providerId = "p"; model = "m"; lastCommitId = 0UL }

type private Log() =
    member val Items = ResizeArray<string>()
    member this.Add(s: string) = this.Items.Add s

type private Rig = { desk: SidebarDesk; overlay: OverlayDesk; log: Log; window: Window; root: Grid }

let private setupWith (allPinned: Guid list -> bool) =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let log = Log()
    let actions =
        { newConversation = (fun () -> log.Add "new")
          openConversation = (fun id -> log.Add(sprintf "open %O" id))
          renameConversation = (fun s -> log.Add("rename " + s.title))
          deleteConversation = (fun s -> log.Add("delete " + s.title))
          setPinned = (fun s v -> log.Add(sprintf "pin %s %b" s.title v))
          setArchived = (fun s v -> log.Add(sprintf "archive %s %b" s.title v))
          selectionAllPinned = allPinned
          setPinnedMany = (fun ids v -> for id in ids do log.Add(sprintf "pinMany %O %b" id v))
          setArchivedMany = (fun ids v -> for id in ids do log.Add(sprintf "archiveMany %O %b" id v))
          deleteMany = (fun ids -> log.Add(sprintf "deleteMany %d" ids.Length))
          duplicateAsFork = (fun s -> log.Add("fork " + s.title))
          exportConversation = (fun s -> log.Add("export " + s.title))
          openSettings = (fun () -> log.Add "settings")
          reconnect = (fun () -> log.Add "reconnect")
          toggleArchivedVisibility = (fun () -> log.Add "toggleArchived")
          closeNavigation = (fun () -> log.Add "close") }
    let desk = SidebarDesk(theme, overlay, actions, fun s -> Border(Width = s, Height = s) :> Control)
    root.Children.Insert(0, desk.View)
    let window = Desk.show root 360.0 800.0
    let emptySidebarVS () = (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Sidebar
    desk.Render({ emptySidebarVS () with Host = { (emptySidebarVS ()).Host with Connected = true; StatusText = "已连接" } })
    { desk = desk; overlay = overlay; log = log; window = window; root = root }

let private emptySidebarVS () =
    let vs = (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Sidebar
    { vs with Host = { vs.Host with Connected = true; StatusText = "已连接" } }

let private setup () = setupWith (fun _ -> false)

/// SidebarRowVS 是侧栏唯一输入：测试直接构造投影行，不再走旁路参数。
let private rowVS (s: ConversationSummary) : Wanxiang2.WorkspaceState.SidebarRowVS =
    { Id = s.id
      Title = s.title
      Preview = s.preview
      Pinned = s.pinned
      Archived = s.archived
      Running = s.running
      MessageCount = s.messageCount
      CreatedAt = s.createdAt
      UpdatedAt = s.updatedAt
      IsFork = s.isFork
      ProviderId = s.providerId
      Model = s.model
      LastCommitId = s.lastCommitId
      Busy = false }

let private load r (items: ConversationSummary list) =
    r.desk.Render { emptySidebarVS () with Rows = items |> List.map rowVS }
    pump ()
    r.window.UpdateLayout()
    pump ()

let private row r (s: ConversationSummary) = (r.desk.Rows.RowHost s.id).Value
let private focused r = TopLevel.GetTopLevel(r.desk.View).FocusManager.GetFocusedElement()
let private runs (t: TextBlock) = t.Inlines |> Seq.choose (function :? Run as x -> Some x | _ -> None) |> List.ofSeq
let private allText (root: Control) =
    all root
    |> List.map (function
        | :? TextBlock as t when not (isNull t.Inlines) && t.Inlines.Count > 0 -> String.Join("", runs t |> List.map (fun x -> x.Text))
        | :? TextBlock as t -> t.Text
        | _ -> "")
    |> fun xs -> String.Join("\n", xs)
let private highlighted (root: Control) =
    all root |> List.collect (function :? TextBlock as t when not (isNull t.Inlines) -> runs t | _ -> [])
    |> List.filter (fun x -> x.IsSet TextElement.ForegroundProperty)

// ---- CraftsmanshipPolishTests ----

[<Fact>]
let ``Sidebar conversation row right click invokes context menu`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        press (row r a) Key.F10 KeyModifiers.Shift
        Assert.True(r.overlay.Layer.IsPopupOpen)
        let names = all r.overlay.Layer.PopupCard |> List.choose (function :? TextBlock as t -> Some t.Text | _ -> None)
        for n in [ "重命名"; "多选"; "置顶"; "归档"; "从此分叉"; "导出（Markdown / JSON）…"; "删除" ] do Assert.Contains(n, names))

[<Fact>]
let ``Sidebar search clear button has accessible tooltip and automation properties`` () =
    Headless.run (fun () ->
        let r = setup ()
        let clear = r.desk.Search.ClearButton
        Assert.Equal("清空搜索", AutomationProperties.GetName clear)
        Assert.Equal("清空搜索", ToolTip.GetTip clear :?> string)
        Assert.False clear.IsHitTestVisible
        r.desk.Search.Box.Text <- "abc"
        pump ()
        Assert.True clear.IsHitTestVisible
        invoke clear
        Assert.Equal("", r.desk.Search.Text))

[<Fact>]
let ``Sidebar search navigation moves focus and handles escape`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        r.desk.FocusSearch()
        key r.desk.Search.Box Key.Down
        let first = r.desk.Rows.VisibleIds.Head
        Assert.Same(row r { a with id = first }, focused r)
        key (focused r :?> Control) Key.Up
        Assert.Same(r.desk.Search.Box, focused r)
        r.desk.Search.Box.Text <- "甲"
        pump ()
        key r.desk.Search.Box Key.Escape
        Assert.Equal("", r.desk.Search.Text)
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a; rowVS b ]; ActiveId = Some b.id }
        key r.desk.Search.Box Key.Escape
        Assert.Same(row r b, focused r))

// ---- SelectionOperabilityTests ----

[<Fact>]
let ``entering selection mode turns row clicks into selection toggles`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        r.desk.EnterSelection a.id
        invoke (row r b)
        Assert.Equal<Guid list>(r.desk.Rows.VisibleIds |> List.filter (fun id -> id = a.id || id = b.id), r.desk.SelectedIds())
        Assert.DoesNotContain(r.log.Items, fun s -> s.StartsWith "open"))

[<Fact>]
let ``deselecting the last item exits selection mode`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        r.desk.EnterSelection a.id
        invoke (row r a)
        Assert.False r.desk.IsSelectionMode)

[<Fact>]
let ``escape exits selection mode from the archived toggle row`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, z = conv "甲", { conv "旧" with archived = true }
        load r [ a; z ]
        r.desk.EnterSelection a.id
        key r.desk.Rows.ArchivedToggle.Value Key.Escape
        Assert.False r.desk.IsSelectionMode)

[<Fact>]
let ``batch pin and archive issue one command per selected conversation`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        r.desk.EnterSelection a.id
        r.desk.ToggleSelected b.id
        invoke r.desk.Batch.PinButton
        invoke r.desk.Batch.ArchiveButton
        Assert.Equal(2, r.log.Items |> Seq.filter (fun s -> s.StartsWith "pinMany" && s.EndsWith "true") |> Seq.length)
        Assert.Equal(2, r.log.Items |> Seq.filter (fun s -> s.StartsWith "archiveMany") |> Seq.length))

[<Fact>]
let ``escape in selection mode exits the mode even with a search query`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        r.desk.ApplySearchText "甲"
        pump ()
        r.desk.EnterSelection a.id
        key (row r a) Key.Escape
        Assert.False r.desk.IsSelectionMode
        Assert.Equal("甲", r.desk.Search.Text))

[<Fact>]
let ``selection action buttons disable themselves while nothing is selected`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        r.desk.EnterSelection a.id
        Assert.True r.desk.Batch.DeleteButton.IsEnabled
        // 已选项被列表换掉：批量键跟着禁用
        load r [ b ]
        Assert.False r.desk.Batch.DeleteButton.IsEnabled
        Assert.False r.desk.Batch.PinButton.IsEnabled
        Assert.False r.desk.Batch.ArchiveButton.IsEnabled)

[<Fact>]
let ``selection mode gives up the brand header and footer`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        Assert.True r.desk.Brand.View.IsVisible
        r.desk.EnterSelection a.id
        Assert.False r.desk.Brand.View.IsVisible
        Assert.False r.desk.Host.View.IsVisible
        Assert.True r.desk.Batch.Header.IsVisible
        r.desk.ExitSelection()
        Assert.True r.desk.Brand.View.IsVisible
        Assert.True r.desk.Host.View.IsVisible)

[<Fact>]
let ``row context menu offers the selection entry`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        invoke (r.desk.Rows.MoreButton a.id).Value
        let entry = all r.overlay.Layer.PopupCard |> List.find (fun c -> AutomationProperties.GetName c = "多选")
        invoke entry
        Assert.True r.desk.IsSelectionMode
        Assert.Equal<Guid list>([ a.id ], r.desk.SelectedIds()))

[<Fact>]
let ``selection buttons rename themselves as the selection changes`` () =
    Headless.run (fun () ->
        let pinned = Collections.Generic.HashSet<Guid>()
        let r = setupWith (fun ids -> not ids.IsEmpty && ids |> List.forall pinned.Contains)
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        pinned.Add a.id |> ignore
        r.desk.EnterSelection a.id
        Assert.Equal("取消置顶", AutomationProperties.GetName r.desk.Batch.PinButton)
        r.desk.ToggleSelectAllVisible()
        Assert.Equal("置顶", AutomationProperties.GetName r.desk.Batch.PinButton)
        Assert.Equal("取消全选", AutomationProperties.GetName r.desk.Batch.SelectAllButton))

[<Fact>]
let ``setting the active conversation scrolls it into view`` () =
    Headless.run (fun () ->
        let r = setup ()
        let items = [ for i in 1..80 -> conv (sprintf "对话 %d" i) ]
        load r items
        let last = r.desk.Rows.VisibleIds |> List.last
        r.desk.Render { emptySidebarVS () with Rows = items |> List.map rowVS; ActiveId = Some last }
        pump ()
        r.window.UpdateLayout()
        pump ()
        Assert.True((r.desk.Rows.RowHost last).IsSome))

[<Fact>]
let ``pin key follows the selected items' pinned state alone`` () =
    Headless.run (fun () ->
        let pinned = Collections.Generic.HashSet<Guid>()
        let r = setupWith (fun ids -> not ids.IsEmpty && ids |> List.forall pinned.Contains)
        let a, b = conv "甲", { conv "乙" with pinned = true }
        pinned.Add b.id |> ignore
        load r [ a; b ]
        r.desk.EnterSelection b.id
        invoke r.desk.Batch.PinButton
        Assert.Contains(sprintf "pinMany %O false" b.id, r.log.Items)
        Assert.DoesNotContain(r.log.Items, fun s -> s.Contains(string a.id)))

[<Fact>]
let ``batch delete with an empty selection never reaches the batch action`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        r.desk.EnterSelection a.id
        load r [ b ]
        key (row r b) Key.Delete
        Assert.DoesNotContain(r.log.Items, fun s -> s.StartsWith "deleteMany"))

// ---- KeyConvergenceTests ----

[<Fact>]
let ``p key on a focused row toggles pin`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        key (row r a) Key.P
        Assert.Contains("pin 甲 true", r.log.Items))

[<Fact>]
let ``ctrl p on a focused row is left for the global table`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        press (row r a) Key.P KeyModifiers.Control
        Assert.DoesNotContain(r.log.Items, fun s -> s.StartsWith "pin"))

[<Fact>]
let ``page up on a sidebar row scrolls the list to the first item`` () =
    Headless.run (fun () ->
        let r = setup ()
        let items = [ for i in 1..5 -> conv (sprintf "对话 %d" i) ]
        load r items
        let ids = r.desk.Rows.VisibleIds
        let lastRow = (r.desk.Rows.RowHost(List.last ids)).Value
        key lastRow Key.PageUp
        Assert.Same((r.desk.Rows.RowHost ids.Head).Value, focused r))

// ---- InteractabilityPolishTests ----

[<Fact>]
let ``row context menu stays closed while the sidebar is in selection mode`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        r.desk.EnterSelection a.id
        press (row r a) Key.F10 KeyModifiers.Shift
        Assert.False r.overlay.Layer.IsPopupOpen)

[<Fact>]
let ``escape in the search box leaves selection mode first`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        r.desk.EnterSelection a.id
        key r.desk.Search.Box Key.Escape
        Assert.False r.desk.IsSelectionMode)

// ---- LibraryTests ----

[<Fact>]
let ``sidebar shows hit snippets under message content and clicking opens the hit`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = conv "甲"
        load r [ a ]
        let queries = ResizeArray<string>()
        r.desk.SearchHandler <- Some(fun q _ _ -> queries.Add q)
        let opened = ResizeArray<SearchHit>()
        r.desk.OpenSearchHit <- opened.Add
        r.desk.Search.Text <- "苹果"
        pump ()
        r.desk.BeginMessageSearch()
        Assert.Equal<string list>([ "苹果" ], List.ofSeq queries)
        let hit =
            { conversationId = a.id; title = "甲"; archived = false; messageId = 3UL; role = "user"
              snippet = "我爱吃苹果"; matchStart = 3; matchLength = 2 }
        r.desk.ApplySearchResults({ queryId = Guid.NewGuid(); query = "苹果"; hits = [ hit ]; hasMore = false; truncated = false }, 0)
        pump ()
        r.window.UpdateLayout()
        Assert.Equal<SearchHit list>([ hit ], r.desk.MessageHits)
        let host = (byName r.desk.View "消息命中：甲，我爱吃苹果").Value
        invoke host
        Assert.Equal<SearchHit list>([ hit ], List.ofSeq opened))

[<Fact>]
let ``sidebar marks fork rows and does not search when no handler is wired`` () =
    Headless.run (fun () ->
        let r = setup ()
        let f = { conv "分支" with isFork = true }
        load r [ f ]
        Assert.Contains("分叉 · ", allText r.desk.View)
        r.desk.Search.Text <- "zzz"
        pump ()
        r.desk.BeginMessageSearch()
        pump ()
        Assert.DoesNotContain("正在搜索", String.Join("\n", Desk.texts r.desk.View)))

// ---- UiStabilityTests ----

[<Fact>]
let ``changing active sidebar row does not rebuild the conversation list`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a, b = conv "甲", conv "乙"
        load r [ a; b ]
        let source = r.desk.Rows.List.ItemsSource
        let hostA = row r a
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a; rowVS b ]; ActiveId = Some a.id }
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a; rowVS b ]; ActiveId = Some b.id }
        Assert.Same(source, r.desk.Rows.List.ItemsSource)
        Assert.Same(hostA, row r a)
        Assert.Equal("当前对话", AutomationProperties.GetItemStatus(row r b)))

[<Fact>]
let ``sidebar empty state separates list loading from confirmed empty and embeds primary actions`` () =
    Headless.run (fun () ->
        let r = setup ()
        r.desk.Render({ emptySidebarVS () with Host = { (emptySidebarVS ()).Host with Connected = false; StatusText = "未连接" } })
        Assert.True r.desk.EmptyCard.IsVisible
        Assert.False r.desk.EmptyCard.ActionButton.IsVisible
        r.desk.Render({ emptySidebarVS () with Compact = true; Host = { (emptySidebarVS ()).Host with Connected = false; StatusText = "未连接" } })
        Assert.True r.desk.EmptyCard.ActionButton.IsVisible
        invoke r.desk.EmptyCard.ActionButton
        Assert.Contains("reconnect", r.log.Items)
        r.desk.Render({ emptySidebarVS () with Compact = false; Host = { (emptySidebarVS ()).Host with Connected = true; StatusText = "已连接" } })
        r.desk.Render({ emptySidebarVS () with Empty = Wanxiang2.WorkspaceState.SidebarEmptyKind.Loading })
        Assert.True r.desk.EmptyCard.Skeleton.IsVisible
        Assert.DoesNotContain("还没有对话", Desk.texts r.desk.View)
        load r []
        Assert.False r.desk.EmptyCard.Skeleton.IsVisible
        Assert.Contains("还没有对话", Desk.texts r.desk.View))

// ---- SearchHighlightTests ----

[<Fact>]
let ``title and preview both highlight while searching`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = { conv "苹果笔记" with preview = "今天买了苹果" }
        load r [ a ]
        r.desk.Search.Text <- "苹果"
        pump ()
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a ] }
        pump ()
        let lit = highlighted (row r a) |> List.map (fun x -> x.Text)
        Assert.True(lit.Length >= 2, sprintf "%A" lit))

[<Fact>]
let ``clearing the query removes every highlight`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = { conv "苹果笔记" with preview = "今天买了苹果" }
        load r [ a ]
        r.desk.Search.Text <- "苹果"
        pump ()
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a ] }
        pump ()
        r.desk.ResetSearch false
        pump ()
        r.window.UpdateLayout()
        Assert.Equal("", r.desk.Search.Text)
        Assert.Equal<string list>([], highlighted (row r a) |> List.map (fun x -> x.Text + "|" + string x.Foreground)))

[<Fact>]
let ``recycled rows keep their highlight after a snapshot refresh`` () =
    Headless.run (fun () ->
        let r = setup ()
        let a = { conv "苹果笔记" with preview = "今天买了苹果" }
        load r [ a ]
        r.desk.Search.Text <- "苹果"
        pump ()
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS a ] }
        pump ()
        r.desk.Render { emptySidebarVS () with Rows = [ rowVS { a with messageCount = 9 } ] }
        pump ()
        r.window.UpdateLayout()
        Assert.NotEmpty(highlighted (row r a)))

// 两万行重建须是线性的：同名区分与分叉面包屑按索引取，不再每行扫全表。
[<Fact>]
let ``两万行带分叉与同名对话的重建在一秒内完成`` () =
    Headless.run (fun () ->
        let r = setup ()
        let items = [ for i in 0 .. 19999 -> conv (sprintf "对话 %d" (i % 5000)) ]
        let lineage =
            items |> List.chunkBySize 2 |> List.choose (function
                | [ p; c ] -> Some(c.id, { parentId = p.id; forkBaseMessageId = None; forkFirstMessageId = None; parentTitle = None; parentDeleted = false; forkOrdinal = Some 1 })
                | _ -> None) |> Map.ofList
        let sw = Diagnostics.Stopwatch.StartNew()
        // 谱系随 SidebarVS 一起进（单一入口，不再有旁路插槽）
        r.desk.Render { emptySidebarVS () with Rows = items |> List.map rowVS; Lineage = lineage }
        pump ()
        r.window.UpdateLayout()
        pump ()
        sw.Stop()
        Assert.True(sw.ElapsedMilliseconds < 1000L, sprintf "重建用了 %d ms" sw.ElapsedMilliseconds))

// ---- 单一权威：品牌行与页脚只经各自的 Render，组合者不旁路写控件 ----

[<Fact>]
let ``紧凑态经投影驱动品牌行返回键，不靠旁路写控件`` () =
    Headless.run (fun () ->
        let r = setup ()
        Assert.False r.desk.Brand.BackButton.IsVisible
        r.desk.Render { emptySidebarVS () with Compact = true }
        pump ()
        Assert.True r.desk.Brand.BackButton.IsVisible
        r.desk.Render { emptySidebarVS () with Compact = false }
        pump ()
        Assert.False r.desk.Brand.BackButton.IsVisible)

[<Fact>]
let ``页脚主机状态经投影渲染，服务器地址命令改投影后重渲染`` () =
    Headless.run (fun () ->
        let r = setup ()
        r.desk.Render { emptySidebarVS () with Host = { (emptySidebarVS ()).Host with ServerUrl = "https://example.com:8443/x" } }
        pump ()
        Assert.Contains("example.com:8443", String.Join("\n", Desk.texts r.desk.Host.View))
        r.desk.ApplyServerUrl "http://localhost:9000"
        pump ()
        Assert.Contains("localhost:9000", String.Join("\n", Desk.texts r.desk.Host.View)))

[<Fact>]
let ``预设插槽改品牌行投影的可见性`` () =
    Headless.run (fun () ->
        let r = setup ()
        Assert.False r.desk.Brand.PresetButton.IsVisible
        r.desk.SlotPresets([ "writer", "写作助手", (fun () -> ()) ], fun _ _ -> ())
        pump ()
        Assert.True r.desk.Brand.PresetButton.IsVisible
        r.desk.SlotPresets([], fun _ _ -> ())
        pump ()
        Assert.False r.desk.Brand.PresetButton.IsVisible)

[<Fact>]
let ``预设插槽接上后，模型重投影不夺走可见性`` () =
    Headless.run (fun () ->
        let r = setup ()
        r.desk.SlotPresets([ "writer", "写作助手", (fun () -> ()) ], fun _ _ -> ())
        pump ()
        Assert.True r.desk.Brand.PresetButton.IsVisible
        // 模型投影里没有预设（空目录），但宿主接了下拉插槽：按钮仍该在
        r.desk.Render { emptySidebarVS () with Brand = { (emptySidebarVS ()).Brand with PresetsVisible = false } }
        pump ()
        Assert.True r.desk.Brand.PresetButton.IsVisible)
