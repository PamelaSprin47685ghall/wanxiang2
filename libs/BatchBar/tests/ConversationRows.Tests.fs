module Wanxiang2.ConversationRows.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ConversationRows
open Wanxiang2.ConversationRows.Tests.Probe

let private look () =
    let rowMin = Cell 44.0
    rowMin,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = Cell 14.0; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = Cell 8.0; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      Selected = Looks.brush Colors.LightBlue; Hover = Looks.brush Colors.WhiteSmoke; SelectedHover = Looks.brush Colors.SkyBlue
      AccentSoft = Looks.brush Colors.AliceBlue; RowMinHeight = rowMin; RowPaddingY = Cell 8.0; StateSlotWidth = Cell 8.0
      StateGlyphSize = Cell 12.0; RunningDotSize = Cell 6.0; SelectedEdgeWidth = Cell 2.0; RowContentSpacing = Cell 2.0
      IconButtonSize = Cell 30.0; CompactTarget = Cell 44.0; OpacitySubtle = Cell 0.5
      Transitions = fun _ -> Avalonia.Animation.Transitions() }

let private ids = Array.init 4 (fun _ -> Guid.NewGuid())

let private row i (running: bool) (pinned: bool) =
    let t = sprintf "对话 %d" i
    RowItem.Conversation
        { Id = ids[i]; Title = t; TitleSpans = [ { Text = t; IsMatch = false } ]; Preview = "预览"
          PreviewSpans = [ { Text = "预览"; IsMatch = false } ]; AccessibleName = t; Tooltip = t
          Running = running; Pinned = pinned; Archived = false }

let private make () =
    let rowMin, l = look ()
    let r = ConversationRows l
    let w = host r.View
    r.Render [ RowItem.Header "今天"; row 0 true false; row 1 false true; row 2 false false; RowItem.ArchivedToggle(false, 3) ]
    Dispatcher.UIThread.RunJobs()
    w.UpdateLayout()
    r, w, rowMin

let private hostOf (r: ConversationRows) i = (r.RowHost ids[i]).Value

let private key (w: Window) (k: Key) (mods: RawInputModifiers) =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``Sidebar conversation row activates on Enter and Space keys`` () =
    Headless.run (fun () ->
        let r, w, _ = make ()
        let opened = ResizeArray<Guid>()
        r.RowActivated.Add opened.Add
        (hostOf r 2).Focus() |> ignore
        w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null)
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<Guid list>([ ids[2]; ids[2] ], List.ofSeq opened)
        // 读屏 Invoke 走同一个动作
        click (hostOf r 1)
        Assert.Equal(ids[1], opened[2]))

[<Fact>]
let ``sidebar running pin and idle states keep the same title origin`` () =
    Headless.run (fun () ->
        let r, _, _ = make ()
        let titleX i =
            let title = descendants (hostOf r i) |> Seq.pick (function :? TextBlock as t when t.Inlines.Count > 0 && t.FontWeight = FontWeight.Medium -> Some t | _ -> None)
            (title.TranslatePoint(Point(0.0, 0.0), hostOf r i)).Value.X
        Assert.Equal(titleX 0, titleX 1, 3)
        Assert.Equal(titleX 1, titleX 2, 3)
        Assert.Equal("生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1)))

[<Fact>]
let ``Sidebar row more action stays reachable in compact mode`` () =
    Headless.run (fun () ->
        let r, _, _ = make ()
        r.SetCompact true
        let more = (r.MoreButton ids[2]).Value
        Assert.True(more.IsVisible && more.IsHitTestVisible && more.Focusable)
        Assert.Equal(44.0, more.Width)
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
        let r, _, _ = make ()
        r.SetMarks(None, (fun id -> id = ids[1]), true)
        let more = (r.MoreButton ids[2]).Value
        Assert.False more.IsVisible
        Assert.False more.IsHitTestVisible
        Assert.False more.Focusable
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView more)
        Assert.Equal("当前对话", AutomationProperties.GetItemStatus(hostOf r 1))
        r.SetMarks(None, (fun _ -> false), false)
        Assert.True more.IsVisible
        Assert.True more.IsHitTestVisible
        Assert.True more.Focusable)

[<Fact>]
let ``active or selected rows highlight and update screen reader item status`` () =
    Headless.run (fun () ->
        let _, l = look ()
        let r = ConversationRows l
        let w = host r.View
        let archived =
            RowItem.Conversation
                { Id = ids[3]; Title = "对话 3"; TitleSpans = [ { Text = "对话 3"; IsMatch = false } ]; Preview = "预览"
                  PreviewSpans = [ { Text = "预览"; IsMatch = false } ]; AccessibleName = "对话 3"; Tooltip = "对话 3"
                  Running = false; Pinned = false; Archived = true }
        r.Render [ row 0 true false; row 1 false true; row 2 false false; archived ]
        Dispatcher.UIThread.RunJobs()
        w.UpdateLayout()
        let border i = hostOf r i :?> Border
        // 初始：无当前对话、无多选，各行不高亮，读屏状态随模型
        Assert.Same(Brushes.Transparent :> IBrush, (border 0).Background)
        Assert.Same(Brushes.Transparent :> IBrush, (border 0).BorderBrush)
        Assert.Equal("生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1))
        Assert.Equal("", AutomationProperties.GetItemStatus(hostOf r 2))
        Assert.Equal("已归档", AutomationProperties.GetItemStatus(hostOf r 3))
        // 高亮 = 当前对话或多选中
        r.SetMarks(Some ids[0], (fun id -> id = ids[2]), true)
        Assert.Same(l.Selected, (border 0).Background)
        Assert.Same(l.Accent, (border 0).BorderBrush)
        Assert.Equal("当前对话，生成中", AutomationProperties.GetItemStatus(hostOf r 0))
        Assert.Same(Brushes.Transparent :> IBrush, (border 1).Background)
        Assert.Same(Brushes.Transparent :> IBrush, (border 1).BorderBrush)
        Assert.Equal("已置顶", AutomationProperties.GetItemStatus(hostOf r 1))
        Assert.Same(l.Selected, (border 2).Background)
        Assert.Same(l.Accent, (border 2).BorderBrush)
        Assert.Equal("当前对话", AutomationProperties.GetItemStatus(hostOf r 2))
        Assert.Same(Brushes.Transparent :> IBrush, (border 3).Background)
        Assert.Equal("已归档", AutomationProperties.GetItemStatus(hostOf r 3)))

[<Fact>]
let ``Sidebar conversation row right click invokes context menu`` () =
    Headless.run (fun () ->
        let r, w, _ = make ()
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
        r.SetMarks(None, (fun _ -> false), true)
        h.Focus() |> ignore
        key w Key.Apps RawInputModifiers.None
        Assert.Single menus |> ignore)

[<Fact>]
let ``row keys walk the list, reach the archived toggle and leave the top`` () =
    Headless.run (fun () ->
        let r, w, _ = make ()
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
        let r, w, _ = make ()
        let t = r.ArchivedToggle.Value
        Assert.Equal("已归档对话（3），点击展开", AutomationProperties.GetName t)
        Assert.Equal("已折叠", AutomationProperties.GetItemStatus t)
        r.ArchivedToggleClicked.Add(fun () ->
            r.Render [ row 0 false false; RowItem.ArchivedToggle(true, 3) ])
        t.Focus() |> ignore
        click t
        w.UpdateLayout()
        Dispatcher.UIThread.RunJobs()
        let t2 = r.ArchivedToggle.Value
        Assert.Equal("收起已归档对话", AutomationProperties.GetName t2)
        Assert.True t2.IsFocused)

[<Fact>]
let ``hit rows and status actions raise their keys and row metrics follow the theme`` () =
    Headless.run (fun () ->
        let rowMin, l = look ()
        let r = ConversationRows l
        let _ = host r.View
        r.Render
            [ row 0 false false
              RowItem.Header "消息内容"
              RowItem.Hit { Key = "h1"; Title = "标题"; Before = "前"; Matched = "中"; After = "后"; AccessibleName = "消息命中：标题，前中后" }
              RowItem.Status("还有更多结果", Some("显示更多", "more")) ]
        Dispatcher.UIThread.RunJobs()
        let hits = ResizeArray()
        let acts = ResizeArray()
        r.HitClicked.Add hits.Add
        r.StatusActionClicked.Add acts.Add
        click (named r.View "消息命中：标题，前中后").Value
        click (named r.View "显示更多").Value
        Assert.Equal<string list>([ "h1" ], List.ofSeq hits)
        Assert.Equal<string list>([ "more" ], List.ofSeq acts)
        rowMin.Set 60.0
        Assert.Equal(60.0, (hostOf r 0).MinHeight))
