module Wanxiang2.UiKit.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.UiKit

let private brush (c: Color) = SolidColorBrush(c) :> IBrush

let private look () =
    { Text = brush Colors.Black
      Muted = brush Colors.Gray
      Faint = brush Colors.LightGray
      Accent = brush Colors.Blue
      FieldSurface = brush Colors.White
      FieldEdge = brush Colors.Silver
      BodyFontSize = Cell 14.0
      CaptionFontSize = Cell 12.0
      MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0
      TitleFontSize = Cell 19.0
      CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2
      SpaceXs = Cell 4.0
      SpaceSm = Cell 6.0
      SpaceMd = Cell 8.0
      SpaceXl = Cell 16.0
      RadiusMd = Cell 8.0
      RadiusLg = Cell 12.0
      BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0
      TextFieldPaddingY = Cell 6.0
      TextFieldTextMinHeight = Cell 22.0
      SettingsRowMinHeight = Cell 52.0
      Glyph = (fun _ b -> Rectangle(Width = 10.0, Height = 10.0, Fill = b) :> Control) }

/// 顺序添加子控件的 StackPanel（属性初始化器不支持 Children）。
let private stack (children: Control list) =
    let p = StackPanel()
    for c in children do p.Children.Add c
    p

let private host (c: Control) =
    let w = Window(Content = c, Width = 800.0, Height = 600.0)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    w

let private focus (c: Control) =
    c.Focusable <- true
    c.Focus() |> ignore
    Dispatcher.UIThread.RunJobs()
    Assert.True c.IsFocused

let private press (w: Window) (k: Key) =
    w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null)
    w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

let private invokeViaAutomation (c: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement c
    (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    Dispatcher.UIThread.RunJobs()

let private clickCenter (w: Window) (target: Control) =
    let p = (target.TranslatePoint(Point(target.Bounds.Width / 2.0, target.Bounds.Height / 2.0), w)).Value
    w.MouseDown(p, MouseButton.Left, RawInputModifiers.None)
    w.MouseUp(p, MouseButton.Left, RawInputModifiers.None)
    Dispatcher.UIThread.RunJobs()

// ---------- Follow ----------

[<Fact>]
let ``Follow applies the current value, follows while attached and stops after detach`` () =
    Headless.run (fun () ->
        let cell = Cell 3.0
        let b = Border(Width = 100.0, Height = 20.0)
        let seen = ResizeArray<float>()
        // 构造时先应用一次；挂上可视树订阅时，观察者立刻回放当前值 → 再应用一次（迁移前的既有语义）
        Follow.live b cell (fun v -> seen.Add v)
        Assert.Equal<float list>([ 3.0 ], List.ofSeq seen)
        let w = host b
        Assert.Equal<float list>([ 3.0; 3.0 ], List.ofSeq seen)
        cell.Set 5.0
        Assert.Equal<float list>([ 3.0; 3.0; 5.0 ], List.ofSeq seen)
        w.Content <- Border()
        Dispatcher.UIThread.RunJobs()
        cell.Set 7.0
        Assert.Equal<float list>([ 3.0; 3.0; 5.0 ], List.ofSeq seen)
        // 再挂回可视树会订阅、回放当前值并继续跟随
        w.Content <- b
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<float list>([ 3.0; 3.0; 5.0; 7.0 ], List.ofSeq seen)
        cell.Set 8.0
        Assert.Equal<float list>([ 3.0; 3.0; 5.0; 7.0; 8.0 ], List.ofSeq seen)
        w.Close())

[<Fact>]
let ``Follow.live2 and live3 apply every side as it arrives`` () =
    Headless.run (fun () ->
        let a, b, c = Cell 1.0, Cell 2.0, Cell 3.0
        let r = Border()
        let triples = ResizeArray<float * float * float>()
        Follow.live3 r a b c (fun x y z -> triples.Add((x, y, z)))
        let w = host r
        // 每一侧的到达都会应用全部当前值；断言每次变更后的最后一次应用
        a.Set 10.0
        Assert.Equal((10.0, 2.0, 3.0), Seq.last triples)
        b.Set 20.0
        Assert.Equal((10.0, 20.0, 3.0), Seq.last triples)
        c.Set 30.0
        Assert.Equal((10.0, 20.0, 30.0), Seq.last triples)
        let r2 = Border()
        let pairs = ResizeArray<float * float>()
        Follow.live2 r2 a b (fun x y -> pairs.Add((x, y)))
        let w2 = host r2
        b.Set 21.0
        Assert.Equal((10.0, 21.0), Seq.last pairs)
        w.Close()
        w2.Close())

// ---------- Tap ----------

[<Fact>]
let ``Tap invokes from keyboard, pointer and automation; SetEnabled gates all three`` () =
    Headless.run (fun () ->
        let mutable n = 0
        let t = Tap(Border(), "点我", [ "surface" ], fun () -> n <- n + 1)
        t.VerticalAlignment <- VerticalAlignment.Top
        t.HorizontalAlignment <- HorizontalAlignment.Left
        t.Width <- 120.0
        t.Height <- 24.0
        t.Background <- Brushes.Transparent
        let w = host t
        focus t
        press w Key.Enter
        Assert.Equal(1, n)
        press w Key.Space
        Assert.Equal(2, n)
        clickCenter w t
        Assert.Equal(3, n)
        invokeViaAutomation t
        Assert.Equal(4, n)
        t.SetEnabled false
        Assert.False t.Focusable
        press w Key.Space
        clickCenter w t
        invokeViaAutomation t
        Assert.Equal(4, n)
        t.SetEnabled true
        focus t
        press w Key.Space
        Assert.Equal(5, n)
        w.Close())

[<Fact>]
let ``Tap.TextButton keeps the accessible name in sync with SetText`` () =
    Headless.run (fun () ->
        let mutable n = 0
        let t = Tap.TextButton("复制", [ "button-ghost" ], fun () -> n <- n + 1)
        Assert.Equal("复制", AutomationProperties.GetName t)
        Assert.Equal("复制", t.Text)
        t.SetText "已复制"
        Assert.Equal("已复制", AutomationProperties.GetName t)
        Assert.Equal("已复制", t.Text)
        invokeViaAutomation t
        Assert.Equal(1, n))

// ---------- Spot / InvokeBorder ----------

[<Fact>]
let ``Spot invokes from keyboard, pointer and automation`` () =
    Headless.run (fun () ->
        let mutable n = 0
        let s = Spot(Width = 120.0, Height = 24.0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top)
        s.Background <- Brushes.Transparent
        s.OnInvoke(fun () -> n <- n + 1)
        let w = host s
        focus s
        press w Key.Space
        Assert.Equal(1, n)
        clickCenter w s
        Assert.Equal(2, n)
        invokeViaAutomation s
        Assert.Equal(3, n)
        w.Close())

[<Fact>]
let ``InvokeBorder runs its action from automation only`` () =
    Headless.run (fun () ->
        let mutable n = 0
        let b = InvokeBorder(Width = 60.0, Height = 20.0)
        b.SetAction(fun () -> n <- n + 1)
        let w = host b
        invokeViaAutomation b
        Assert.Equal(1, n)
        // 键盘与指针激活归调用方接；本类型不自行触发
        focus b
        press w Key.Enter
        press w Key.Space
        Assert.Equal(1, n)
        w.Close())

// ---------- Kit ----------

[<Fact>]
let ``Kit text roles follow live values`` () =
    Headless.run (fun () ->
        let l = look ()
        let kit = Kit l
        let caption = kit.Caption "说明"
        let w = host caption
        Assert.Equal(12.0, caption.FontSize)
        Assert.Equal(17.0, caption.LineHeight)
        Assert.Same(l.Muted, caption.Foreground)
        (l.CaptionFontSize :?> Cell<float>).Set 15.0
        (l.CaptionLineHeight :?> Cell<float>).Set 18.0
        Assert.Equal(15.0, caption.FontSize)
        Assert.Equal(18.0, caption.LineHeight)
        let label = kit.Label "标题"
        Assert.Equal(14.0, label.FontSize)
        Assert.Same(l.Text, label.Foreground)
        Assert.Equal(19.0, (kit.Title "题").FontSize)
        Assert.Equal(17.0, (kit.Heading "节").FontSize)
        let section = kit.SectionLabel "分区"
        Assert.True(section.Classes.Contains "section-label")
        let tag = kit.Tag(TagTone.Warning, "告警")
        Assert.True(tag.Classes.Contains "tag")
        Assert.True(tag.Classes.Contains "tag-warning")
        Assert.False((kit.Tag(TagTone.Neutral, "中性")).Classes.Contains "tag-warning")
        let hair = kit.Hairline()
        Assert.True(hair.Classes.Contains "hairline")
        let card = kit.Card(l.SpaceMd, l.SpaceSm, l.RadiusMd, kit.Label "内容")
        Assert.True(card.Classes.Contains "grouping-card")
        Assert.Equal(Thickness(8.0, 6.0, 8.0, 6.0), card.Padding)
        let stack = kit.VStack(l.SpaceSm, [ kit.Label "一" :> Control; kit.Label "二" :> Control ])
        Assert.Equal(6.0, stack.Spacing)
        Assert.Equal(2, stack.Children.Count)
        let row = kit.RowShell("开关", "说明", kit.Label "右")
        Assert.Equal(52.0, row.MinHeight)
        w.Close())

[<Fact>]
let ``Kit.TextField swaps the edge for accent on focus; invalid class wins`` () =
    Headless.run (fun () ->
        let l = look ()
        let kit = Kit l
        let shell, box = kit.TextField "输入"
        let other = TextBox()
        let w = host(stack [ shell; other ])
        Assert.Same(l.FieldEdge, shell.BorderBrush)
        focus box
        Assert.Same(l.Accent, shell.BorderBrush)
        focus other
        Assert.Same(l.FieldEdge, shell.BorderBrush)
        // invalid 在：聚焦不换强调色，失焦也维持现状
        shell.Classes.Add "invalid"
        focus box
        Assert.Same(l.FieldEdge, shell.BorderBrush)
        focus other
        Assert.Same(l.FieldEdge, shell.BorderBrush)
        shell.Classes.Remove "invalid" |> ignore
        focus box
        Assert.Same(l.Accent, shell.BorderBrush)
        w.Close())

[<Fact>]
let ``Kit.Toggle flips from pointer and flip; SwitchRow proxies Enter on the row`` () =
    Headless.run (fun () ->
        let l = look ()
        let kit = Kit l
        let seen = ResizeArray<bool>()
        let track, read, write, flip = kit.Toggle(false, seen.Add)
        track.Background <- Brushes.Transparent
        track.Width <- 40.0
        track.Height <- 20.0
        let rowSeen = ResizeArray<bool>()
        let row, rowRead, rowWrite = kit.SwitchRow("折行", "自动折行", false, rowSeen.Add)
        let w = host(stack [ track; row ])
        Dispatcher.UIThread.RunJobs()
        Assert.False(read ())
        write true
        Assert.True(read ())
        Assert.True(track.Classes.Contains "on")
        Assert.Equal("开启", AutomationProperties.GetItemStatus track)
        clickCenter w track
        Assert.False(read ())
        Assert.False(track.Classes.Contains "on")
        Assert.Equal("关闭", AutomationProperties.GetItemStatus track)
        Assert.Equal<bool list>([ false ], List.ofSeq seen)
        flip ()
        Assert.True(read ())
        Assert.Equal<bool list>([ false; true ], List.ofSeq seen)
        // 行级开关：整行一个停靠点，Enter 代理翻转
        focus row
        press w Key.Enter
        Assert.True(rowRead ())
        Assert.Equal<bool list>([ true ], List.ofSeq rowSeen)
        Assert.Equal("开启", AutomationProperties.GetItemStatus row)
        rowWrite false
        Assert.False(rowRead ())
        Assert.Equal("关闭", AutomationProperties.GetItemStatus row)
        w.Close())

[<Fact>]
let ``Kit.DialogFooter walks with arrows; row-level confirm skips Danger`` () =
    Headless.run (fun () ->
        let l = look ()
        let kit = Kit l
        let log = ResizeArray<string>()
        let f = kit.DialogFooter("删除", Tone.Danger, (fun () -> log.Add "cancel"), (fun () -> log.Add "confirm"))
        let w = host f.Row
        focus f.Cancel
        press w Key.Right
        Assert.True f.Confirm.IsFocused
        press w Key.Left
        Assert.True f.Cancel.IsFocused
        // Danger：行级 Enter 不做确认；焦点在取消上按 Enter 仍是取消
        press w Key.Enter
        Assert.Equal<string list>([ "cancel" ], List.ofSeq log)
        // 非破坏性页脚：行本身拿焦点时 Enter / Space 即确认
        let g = kit.DialogFooter("保存", Tone.Primary, (fun () -> log.Add "cancel2"), (fun () -> log.Add "confirm2"))
        let w2 = host g.Row
        focus g.Row
        press w2 Key.Space
        Assert.Equal<string list>([ "cancel"; "confirm2" ], List.ofSeq log)
        focus g.Cancel
        press w2 Key.Enter
        Assert.Equal<string list>([ "cancel"; "confirm2"; "cancel2" ], List.ofSeq log)
        w.Close()
        w2.Close())

[<Fact>]
let ``Kit.Button and IconButton carry tone classes and the tip`` () =
    Headless.run (fun () ->
        let l = look ()
        let kit = Kit l
        let b = kit.Button(Tone.Danger, "删除", ignore)
        Assert.True(b.Classes.Contains "button-danger")
        Assert.True(b.Classes.Contains "surface")
        Assert.Equal("删除", AutomationProperties.GetName b)
        Assert.Equal("取消", (kit.Button(Tone.Ghost, "取消", ignore)).Text)
        let i = kit.IconButton("close", "关闭", ignore)
        Assert.True(i.Classes.Contains "icon-button")
        Assert.Equal("关闭", string (ToolTip.GetTip i))
        Assert.Equal("关闭", AutomationProperties.GetName i)
        Assert.Null(ToolTip.GetTip(kit.IconButton("close", "", ignore)))
        ())

[<Fact>]
let ``Kit.Pair yields when a side arrives and stops after dispose`` () =
    let a, b = Cell 1.0, Cell 2.0
    let seen = ResizeArray<float * float>()
    let sub =
        (Kit.Pair(a, b)).Subscribe
            { new IObserver<float * float> with
                member _.OnNext v = seen.Add v
                member _.OnError _ = ()
                member _.OnCompleted() = () }
    Assert.Equal<(float * float) list>([ (1.0, 2.0) ], List.ofSeq seen)
    a.Set 9.0
    Assert.Equal<(float * float) list>([ (1.0, 2.0); (9.0, 2.0) ], List.ofSeq seen)
    sub.Dispose()
    b.Set 7.0
    Assert.Equal<(float * float) list>([ (1.0, 2.0); (9.0, 2.0) ], List.ofSeq seen)
