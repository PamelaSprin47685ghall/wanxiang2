module Wanxiang2.ConversationRows.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Controls.Documents
open Avalonia.Threading
open Xunit
open Wanxiang2.ConversationRows
open Wanxiang2.ConversationRows.Tests.Probe

let private ids = Array.init 4 (fun _ -> Guid.NewGuid())

let private row i (running: bool) (pinned: bool) =
    let t = sprintf "对话 %d" i

    RowItem.Conversation
        {
            Id = ids[i]
            Title = t
            TitleSpans = [ { Text = t; IsMatch = false } ]
            Preview = "预览"
            PreviewSpans = [ { Text = "预览"; IsMatch = false } ]
            Provenance = []
            Meta = "gpt-5"
            Snippet = ""
            SnippetSpans = []
            MetaRight = ""
            AccessibleName = t
            Tooltip = t
            Running = running
            Pinned = pinned
            Archived = false
        }

let private make () =
    let r = ConversationRows()
    let w = host r.View

    r.Render
        [
            RowItem.Header "今天"
            row 0 true false
            row 1 false true
            row 2 false false
            RowItem.ArchivedToggle(false, 3)
        ]

    Dispatcher.UIThread.RunJobs()
    w.UpdateLayout()
    r, w

let private hostOf (r: ConversationRows) i = (r.RowHost ids[i]).Value

let private key (w: Window) (k: Key) (mods: RawInputModifiers) =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``Sidebar conversation row activates on Enter and Space keys`` () =
    Headless.run (fun () ->
        let r, w = make ()
        let opened = ResizeArray<Guid>()
        r.RowActivated.Add opened.Add
        (hostOf r 2).Focus() |> ignore
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null)
        w.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null)
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null)
        w.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<Guid list>([ ids[2]; ids[2] ], List.ofSeq opened)
        // 读屏 Invoke 走同一个动作
        click (hostOf r 1)
        Assert.Equal(ids[1], opened[2]))

[<Fact>]
let ``sidebar running pin and idle states keep status updated`` () =
    Headless.run (fun () ->
        let r, _ = make ()
        Assert.Equal("生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1)))

[<Fact>]
let ``Sidebar row more action stays reachable`` () =
    Headless.run (fun () ->
        let r, _ = make ()
        let more = (r.MoreButton ids[2]).Value
        Assert.True(more.IsVisible && more.IsHitTestVisible && more.Focusable)
        let menus = ResizeArray()
        r.MenuRequested.Add menus.Add
        click more
        let id, anchor, right = menus[0]
        Assert.Equal(ids[2], id)
        Assert.Same(more, anchor)
        Assert.True right)

[<Fact>]
let ``row more button leaves the hit tree in selection mode`` () =
    Headless.run (fun () ->
        let r, _ = make ()
        r.ApplyMarks(None, (fun id -> id = ids[1]), true)
        let more = (r.MoreButton ids[2]).Value
        Assert.False more.IsVisible
        Assert.False more.IsHitTestVisible
        Assert.False more.Focusable
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView more)
        Assert.Equal("当前对话", AutomationProperties.GetItemStatus(hostOf r 1))
        r.ApplyMarks(None, (fun _ -> false), false)
        Assert.True more.IsVisible
        Assert.True more.IsHitTestVisible
        Assert.True more.Focusable)

[<Fact>]
let ``active or selected rows highlight and update screen reader item status`` () =
    Headless.run (fun () ->
        let r = ConversationRows()
        let w = host r.View

        let archived =
            RowItem.Conversation
                {
                    Id = ids[3]
                    Title = "对话 3"
                    TitleSpans = [ { Text = "对话 3"; IsMatch = false } ]
                    Preview = "预览"
                    PreviewSpans = [ { Text = "预览"; IsMatch = false } ]
                    Provenance = [ "归档" ]
                    Meta = "gpt-5"
                    Snippet = ""
                    SnippetSpans = []
                    MetaRight = ""
                    AccessibleName = "对话 3"
                    Tooltip = "对话 3"
                    Running = false
                    Pinned = false
                    Archived = true
                }

        r.Render [ row 0 true false; row 1 false true; row 2 false false; archived ]
        Dispatcher.UIThread.RunJobs()
        w.UpdateLayout()
        let btn i = hostOf r i :?> Button
        // 初始：无当前对话、无多选，各行不高亮，读屏状态随模型
        Assert.False(btn(0).Classes.Contains "selected")
        Assert.Equal("生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1))
        Assert.Equal("", AutomationProperties.GetItemStatus(hostOf r 2))
        Assert.Equal("已归档", AutomationProperties.GetItemStatus(hostOf r 3))
        // 高亮 = 当前对话或多选中
        r.ApplyMarks(Some ids[0], (fun id -> id = ids[2]), true)
        Assert.True(btn(0).Classes.Contains "selected")
        Assert.Equal("当前对话，生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.False(btn(1).Classes.Contains "selected")
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1))
        Assert.True(btn(2).Classes.Contains "selected")
        Assert.Equal("当前对话", AutomationProperties.GetItemStatus(hostOf r 2))
        Assert.False(btn(3).Classes.Contains "selected")
        Assert.Equal("已归档", AutomationProperties.GetItemStatus(hostOf r 3)))

[<Fact>]
let ``Sidebar conversation row right click invokes context menu`` () =
    Headless.run (fun () ->
        let r, w = make ()
        let menus = ResizeArray()
        r.MenuRequested.Add menus.Add
        let h = hostOf r 2
        let p = h.TranslatePoint(Point(10.0, 10.0), w).Value
        w.MouseDown(p, MouseButton.Right, RawInputModifiers.None)
        w.MouseUp(p, MouseButton.Right, RawInputModifiers.None)
        Dispatcher.UIThread.RunJobs()
        Assert.Single menus |> ignore
        let id, anchor, right = menus[0]
        Assert.Equal(ids[2], id)
        Assert.Same(h, anchor)
        Assert.False right
        // 多选模式下不开单条菜单
        r.ApplyMarks(None, (fun _ -> false), true)
        h.Focus() |> ignore
        key w Key.Apps RawInputModifiers.None
        Assert.Single menus |> ignore)

[<Fact>]
let ``row keys walk the list, reach the archived toggle and leave the top`` () =
    Headless.run (fun () ->
        let r, w = make ()
        let tops = ref 0
        r.LeaveTop.Add(fun () -> tops.Value <- tops.Value + 1)
        (hostOf r 0).Focus() |> ignore
        key w Key.Down RawInputModifiers.None
        Assert.True((hostOf r 1).IsFocused)
        key w Key.End RawInputModifiers.None
        Assert.True(r.ArchivedToggle.Value.IsFocused)
        key w Key.Up RawInputModifiers.None
        Assert.True((hostOf r 2).IsFocused)
        key w Key.Home RawInputModifiers.None
        Assert.True((hostOf r 0).IsFocused)
        key w Key.Up RawInputModifiers.None
        Assert.Equal(1, tops.Value)
        let pins = ResizeArray()
        r.PinKey.Add pins.Add
        key w Key.P RawInputModifiers.None
        key w Key.P RawInputModifiers.Control
        Assert.Equal<Guid list>([ ids[0] ], List.ofSeq pins))

[<Fact>]
let ``archived toggle is an expander button that keeps focus across the rebuild`` () =
    Headless.run (fun () ->
        let r, w = make ()
        let t = r.ArchivedToggle.Value
        Assert.Equal("已归档对话（3），点击展开", AutomationProperties.GetName t)
        Assert.Equal("已折叠", AutomationProperties.GetItemStatus t)
        r.ArchivedToggleClicked.Add(fun () -> r.Render [ row 0 false false; RowItem.ArchivedToggle(true, 3) ])
        t.Focus() |> ignore
        click t
        w.UpdateLayout()
        Dispatcher.UIThread.RunJobs()
        let t2 = r.ArchivedToggle.Value
        Assert.Equal("收起已归档对话", AutomationProperties.GetName t2)
        Assert.True t2.IsFocused)

[<Fact>]
let ``hit rows and status actions raise their keys`` () =
    Headless.run (fun () ->
        let r = ConversationRows()
        let _ = host r.View

        r.Render
            [
                row 0 false false
                RowItem.Header "消息内容"
                RowItem.Hit
                    {
                        Key = "h1"
                        Title = "标题"
                        Before = "前"
                        Matched = "中"
                        After = "后"
                        AccessibleName = "消息命中：标题，前中后"
                    }
                RowItem.Status("还有更多结果", Some("显示更多", "more"))
            ]

        Dispatcher.UIThread.RunJobs()
        let hits = ResizeArray()
        let acts = ResizeArray()
        r.HitClicked.Add hits.Add
        r.StatusActionClicked.Add acts.Add
        click (named r.View "消息命中：标题，前中后").Value
        click (named r.View "显示更多").Value
        Assert.Equal<string list>([ "h1" ], List.ofSeq hits)
        Assert.Equal<string list>([ "more" ], List.ofSeq acts))

[<Fact>]
let ``meta right renders the timestamp as a right-docked block`` () =
    Headless.run (fun () ->
        let r = ConversationRows()
        let _ = host r.View

        let withStamp (mr: string) : RowItem =
            RowItem.Conversation
                {
                    Id = ids[0]
                    Title = "对话"
                    TitleSpans = [ { Text = "对话"; IsMatch = false } ]
                    Preview = "gpt-5"
                    PreviewSpans = [ { Text = "gpt-5"; IsMatch = false } ]
                    Provenance = []
                    Meta = "gpt-5"
                    Snippet = ""
                    SnippetSpans = []
                    MetaRight = mr
                    AccessibleName = "对话"
                    Tooltip = "对话"
                    Running = false
                    Pinned = false
                    Archived = false
                }

        let stampBlocks () : Control list =
            Probe.descendants r.View
            |> Seq.choose (fun c ->
                match c with
                | :? TextBlock as t when t.Text = "10月21日" -> Some c
                | _ -> None)
            |> Seq.toList

        // 空串：时间块不渲染（树里没有独立的「10月21日」TextBlock）
        r.Render [ withStamp "" ]
        Dispatcher.UIThread.RunJobs()
        let emptyBlocks: Control list = stampBlocks ()
        Assert.Equal(0, emptyBlocks.Length)

        // 非空：时间戳以**独立** TextBlock 存在（不与预览共串）
        r.Render [ withStamp "10月21日" ]
        Dispatcher.UIThread.RunJobs()
        let blocks: Control list = stampBlocks ()
        Assert.Equal(1, blocks.Length))

/// 第二行拆三层：来源标记渲染成 tag（Border 包着的短文字），模型名是独立的弱化 TextBlock，
/// 命中片段单独一个 TextBlock。三者不再挤成一个同灰度的字符串。
///
/// 旧实现把「归档 · 模型\n片段」拼进一个 `Preview` 字符串再交给单个 TextBlock，
/// 扫读时时间、模型、来源分不开。这条用例钉住拆开后的结构：
/// 来源必须是**容器**（tag）而不是文字前缀，模型与片段必须是**两个**独立 TextBlock。
[<Fact>]
let ``row renders provenance as a tag and keeps model and snippet as separate lines`` () =
    Headless.run (fun () ->
        let r, _w = make ()

        let item =
            RowItem.Conversation
                {
                    Id = ids[0]
                    Title = "对话"
                    TitleSpans = [ { Text = "对话"; IsMatch = false } ]
                    Preview = "分叉 · 归档 · gpt-5\n上次说的那个方案"
                    PreviewSpans =
                        [
                            {
                                Text = "分叉 · 归档 · gpt-5\n上次说的那个方案"
                                IsMatch = false
                            }
                        ]
                    Provenance = [ "归档"; "分叉" ]
                    Meta = "gpt-5"
                    Snippet = "上次说的那个方案"
                    SnippetSpans = [ { Text = "上次说的那个方案"; IsMatch = false } ]
                    MetaRight = "10月21日"
                    AccessibleName = "对话"
                    Tooltip = "对话"
                    Running = false
                    Pinned = false
                    Archived = true
                }

        r.Render [ item ]
        Dispatcher.UIThread.RunJobs()

        let all =
            Probe.descendants r.View
            |> Seq.choose (function
                | null -> None
                | c -> Some c)
            |> Seq.toList

        let texts =
            all
            |> List.choose (function
                | :? TextBlock as t ->
                    let inlineText =
                        if isNull t.Inlines then
                            ""
                        else
                            t.Inlines
                            |> Seq.choose (function
                                | :? Run as r -> Some r.Text
                                | _ -> None)
                            |> String.concat ""

                    let s = if isNull t.Text then inlineText else t.Text

                    if String.IsNullOrEmpty s then None else Some s
                | _ -> None)

        // 来源是**两个** tag：文字在 Border 里，且不带「· 」前缀。
        let tagTexts =
            all
            |> List.choose (function
                | :? Border as b when not (isNull b.Child) ->
                    match b.Child with
                    | :? TextBlock as t when not (isNull t.Text) -> Some t.Text
                    | _ -> None
                | _ -> None)

        Assert.Contains("归档", tagTexts)
        Assert.Contains("分叉", tagTexts)
        // 前缀写法必须消失：否则还是「一条同灰度的串」
        Assert.DoesNotContain(texts, fun t -> t.Contains "归档 · ")
        Assert.DoesNotContain(texts, fun t -> t.Contains "分叉 · ")

        // 模型与片段各是一个独立 TextBlock，不是同一串的两行。
        Assert.Contains("gpt-5", texts)
        Assert.Contains("上次说的那个方案", texts)
        Assert.DoesNotContain(texts, fun t -> t.Contains "gpt-5\n"))

/// 模型名再长也不能把行撑破：第二行必须收在行宽内。
///
/// 用 `Horizontal` 的 `StackPanel` 会让子元素按无限宽度测量，`TextTrimming`
/// 因此失效，长模型名会把时间戳挤出可视区。这条用例钉住「收在行宽内」。
[<Fact>]
let ``long model name stays inside the row width`` () =
    Headless.run (fun () ->
        let r, w = make ()
        let longModel = String.replicate 40 "very-long-model-name-"

        let item =
            RowItem.Conversation
                {
                    Id = ids[0]
                    Title = "对话"
                    TitleSpans = [ { Text = "对话"; IsMatch = false } ]
                    Preview = longModel
                    PreviewSpans = [ { Text = longModel; IsMatch = false } ]
                    Provenance = [ "归档" ]
                    Meta = longModel
                    Snippet = "片段"
                    SnippetSpans = [ { Text = "片段"; IsMatch = false } ]
                    MetaRight = "10月21日"
                    AccessibleName = "对话"
                    Tooltip = "对话"
                    Running = false
                    Pinned = false
                    Archived = true
                }

        r.Render [ item ]
        Dispatcher.UIThread.RunJobs()
        w.UpdateLayout()

        let row = hostOf r 0

        let metaText =
            Probe.descendants row
            |> Seq.choose (function
                | :? TextBlock as t when t.Text = longModel -> Some t
                | _ -> None)
            |> Seq.tryHead

        match metaText with
        | None -> Assert.Fail "模型名 TextBlock 未渲染"
        | Some t ->
            let bounds = t.Bounds
            // 行宽 900 的窗口里，单块文本不该超出宿主宽度；Horizontal StackPanel 下会远超。
            Assert.True(
                t.DesiredSize.Width <= row.Bounds.Width - 40.0,
                sprintf "模型名宽 %.1f 超出可用行宽 %.1f" t.DesiredSize.Width row.Bounds.Width
            )

            Assert.True(bounds.Width <= row.Bounds.Width, "模型名实际布局宽度超出宿主"))

/// 时间戳不能因为三层全空而被吞掉。
///
/// 旧实现只要 `MetaRight` 非空就渲染时间。拆三层后若把「三层全空」的回退
/// 放在最前面，会遮住「三层全空但有时间」这条路径，时间戳静默消失。
[<Fact>]
let ``timestamp still shows when provenance model and snippet are all empty`` () =
    Headless.run (fun () ->
        let r, _w = make ()

        let item =
            RowItem.Conversation
                {
                    Id = ids[0]
                    Title = "对话"
                    TitleSpans = [ { Text = "对话"; IsMatch = false } ]
                    Preview = ""
                    PreviewSpans = []
                    Provenance = []
                    Meta = ""
                    Snippet = ""
                    SnippetSpans = []
                    MetaRight = "10月21日"
                    AccessibleName = "对话"
                    Tooltip = "对话"
                    Running = false
                    Pinned = false
                    Archived = false
                }

        r.Render [ item ]
        Dispatcher.UIThread.RunJobs()

        let stamps =
            Probe.descendants r.View
            |> Seq.choose (function
                | :? TextBlock as t when t.Text = "10月21日" -> Some t
                | _ -> None)
            |> Seq.toList

        Assert.Equal(1, stamps.Length))

/// 字号跟随主题刻度 `Metric.FontBody` / `Metric.FontSmall` / `Metric.FontCaption`，不写死常数。
///
/// 旧实现把会话标题写死 13.5、归档开关写死 12.5、标签/模型/片段/时间/分组头写死 11.5，
/// 属于「两处数值相同、来源不同」——改主题字号刻度时侧栏纹丝不动。
[<Fact>]
let ``row typography follows the font metrics, not constants`` () =
    Headless.run (fun () ->
        let res = Application.Current.Resources
        res["Metric.FontBody"] <- box 21.0
        res["Metric.FontSmall"] <- box 19.0
        res["Metric.FontCaption"] <- box 17.0

        try
            let r, w = make ()

            let item =
                RowItem.Conversation
                    {
                        Id = ids[0]
                        Title = "会话主标题"
                        TitleSpans = [ { Text = "会话主标题"; IsMatch = false } ]
                        Preview = "回退预览"
                        PreviewSpans = [ { Text = "回退预览"; IsMatch = false } ]
                        Provenance = [ "归档" ]
                        Meta = "gpt-5"
                        Snippet = "命中摘要行"
                        SnippetSpans = [ { Text = "命中摘要行"; IsMatch = false } ]
                        MetaRight = "14:05"
                        AccessibleName = "会话主标题"
                        Tooltip = "会话主标题"
                        Running = false
                        Pinned = false
                        Archived = true
                    }

            let fallbackItem =
                RowItem.Conversation
                    {
                        Id = ids[1]
                        Title = "回退行标题"
                        TitleSpans = [ { Text = "回退行标题"; IsMatch = false } ]
                        Preview = "回退预览"
                        PreviewSpans = [ { Text = "回退预览"; IsMatch = false } ]
                        Provenance = []
                        Meta = ""
                        Snippet = ""
                        SnippetSpans = []
                        MetaRight = ""
                        AccessibleName = "回退行标题"
                        Tooltip = "回退行标题"
                        Running = false
                        Pinned = false
                        Archived = false
                    }

            r.Render [ RowItem.Header "今天分组"; item; fallbackItem; RowItem.ArchivedToggle(false, 3) ]

            Dispatcher.UIThread.RunJobs()
            w.UpdateLayout()

            let blocks =
                Probe.descendants r.View
                |> Seq.choose (function
                    | :? TextBlock as t ->
                        let inlineText =
                            if isNull t.Inlines then
                                ""
                            else
                                t.Inlines
                                |> Seq.choose (function
                                    | :? Run as run -> Some run.Text
                                    | _ -> None)
                                |> String.concat ""

                        let s = if String.IsNullOrEmpty t.Text then inlineText else t.Text
                        if String.IsNullOrEmpty s then None else Some(s, t.FontSize)
                    | _ -> None)
                |> Map.ofSeq

            Assert.Equal(21.0, blocks["会话主标题"])
            Assert.Equal(19.0, blocks["归档 3"])
            Assert.Equal(17.0, blocks["今天分组"])
            Assert.Equal(17.0, blocks["归档"])
            Assert.Equal(17.0, blocks["gpt-5"])
            Assert.Equal(17.0, blocks["命中摘要行"])
            Assert.Equal(17.0, blocks["14:05"])
            Assert.Equal(17.0, blocks["回退预览"])
        finally
            res.Remove "Metric.FontBody" |> ignore
            res.Remove "Metric.FontSmall" |> ignore
            res.Remove "Metric.FontCaption" |> ignore)
