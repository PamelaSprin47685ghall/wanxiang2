module Wanxiang2.PopupMenu.Tests.Tests

open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.PopupMenu

let look () =
    let minH = Cell 32.0
    minH,
    { Text = Brushes.Black
      Muted = Brushes.Gray
      Faint = Brushes.LightGray
      Danger = Brushes.Red
      Accent = Brushes.Blue
      Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b, Name = "check"))
      Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
      FontSize = Cell 12.0
      HintFontSize = Cell 10.0
      IconSlotWidth = Cell 16.0
      IconHeight = Cell 16.0
      RightSlotMinWidth = Cell 16.0
      HintMargin = Cell(Thickness(12.0, 0.0, 0.0, 0.0))
      ColumnSpacing = Cell 6.0
      ItemPadding = Cell(Thickness(8.0, 4.0))
      ItemRadius = Cell(CornerRadius 4.0)
      ItemMinHeight = minH
      ItemSpacing = Cell 2.0
      MaxHeight = Cell 200.0
      DividerMargin = Cell(Thickness(6.0, 2.0))
      HeaderMargin = Cell(Thickness(8.0, 6.0, 8.0, 2.0))
      SelectPadding = Cell(Thickness(8.0, 4.0))
      SelectRadius = Cell(CornerRadius 8.0)
      SelectEdgeWidth = Cell(Thickness 1.0)
      SelectCaptionMaxWidth = Cell 260.0 }

let show (content: Control) =
    let window = Window(Width = 360.0, Height = 320.0, Content = content)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    window

let byName (root: Visual) name =
    root.GetVisualDescendants() |> Seq.choose (function :? Control as c when AutomationProperties.GetName c = name -> Some c | _ -> None) |> Seq.head

let key (c: Control) k =
    let e = KeyEventArgs(Key = k, RoutedEvent = InputElement.KeyDownEvent)
    c.RaiseEvent e
    Dispatcher.UIThread.RunJobs()
    e

// 迁自旧 OverlayMenuTests。
[<Fact>]
let ``select button carries an automation name that tracks the text`` () =
    Headless.run (fun () ->
        let _, l = look ()
        let b = SelectButton(l, "跟随系统")
        Assert.Equal("跟随系统", AutomationProperties.GetName b.View)
        b.SetText "浅色"
        Assert.Equal("浅色", AutomationProperties.GetName b.View)
        Assert.Equal<obj>("浅色", ToolTip.GetTip b.View)
        Assert.Equal(AutomationControlType.ComboBox, AutomationProperties.GetControlTypeOverride(b.View).Value)
        // 调用方显式命名优先，不被下拉文本覆盖。
        let named = SelectButton(l, "gpt-4o")
        AutomationProperties.SetName(named.View, "切换模型")
        named.SetText "无所谓"
        Assert.Equal("切换模型", AutomationProperties.GetName named.View))

// 迁自旧 OverlayMenuTests。
[<Fact>]
let ``select button and menu items keep full opacity until pressed`` () =
    Headless.run (fun () ->
        let minH, l = look ()
        let b = SelectButton(l, "模型")
        Assert.Equal(1.0, b.View.Opacity, 3)
        let menu = (PopupMenu.build l ignore [ PopupEntry.create "重命名" ignore |> PopupEntry.withHint "F2" ]).Value
        let window = show menu
        let item = byName menu "重命名" :?> Border
        Assert.Equal(1.0, item.Opacity, 3)
        Assert.Equal(32.0, item.MinHeight, 3)
        minH.Set 40.0
        Assert.Equal(40.0, item.MinHeight, 3)
        window.Close())

// 迁自旧 UiStabilityTests `forty item menu keeps long labels bounded and supports directional focus`（菜单本身部分；落进浮层在组合者里测）。
[<Fact>]
let ``forty item menu supports directional focus and scrolls`` () =
    Headless.run (fun () ->
        let _, l = look ()
        let longLabel = "菜单项 01 " + String.replicate 18 "非常长的名称"
        let entries =
            [ for i in 1..40 ->
                  let label = if i = 1 then longLabel else sprintf "菜单项 %02d" i
                  PopupEntry.create label ignore
                  |> (if i % 3 = 0 then PopupEntry.withIcon (fun b -> Border(Background = b)) else id) ]
        let menu = (PopupMenu.build l ignore entries).Value
        let window = show menu
        let first = byName menu longLabel
        first.Focus() |> ignore
        Assert.True((key first Key.Down).Handled)
        let second = byName menu "菜单项 02"
        Assert.True second.IsFocused
        key second Key.End |> ignore
        let last = byName menu "菜单项 40"
        Assert.True last.IsFocused
        key last Key.Down |> ignore
        Assert.True first.IsFocused
        key first Key.Up |> ignore
        Assert.True last.IsFocused
        key last Key.PageUp |> ignore
        Assert.True first.IsFocused
        Assert.Equal<obj>(longLabel, ToolTip.GetTip first)
        Assert.Equal(AutomationControlType.MenuItem, AutomationProperties.GetControlTypeOverride(first).Value)
        let scroller = menu :?> ScrollViewer
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height)
        Assert.True(first.Bounds.Width <= scroller.Bounds.Width + 0.5)
        window.Close())

[<Fact>]
let ``roving index matches the old menu rule`` () =
    Assert.Equal(Some 0, PopupMenu.nextIndex Key.Down -1 3)
    Assert.Equal(Some 2, PopupMenu.nextIndex Key.Up -1 3)
    Assert.Equal(Some 2, PopupMenu.nextIndex Key.PageDown 0 3)
    Assert.Equal(None, PopupMenu.nextIndex Key.Down 0 0)
    Assert.Equal(None, PopupMenu.nextIndex Key.Left 0 3)

[<Fact>]
let ``enter and space activate the entry and grouped menus skip empty groups`` () =
    Headless.run (fun () ->
        let _, l = look ()
        let hits = ResizeArray()
        let a = PopupEntry.create "甲" ignore |> PopupEntry.markSelected true
        let b = PopupEntry.create "乙" ignore |> PopupEntry.asDanger
        Assert.True((PopupMenu.buildGrouped l ignore [ "空", [] ]).IsNone)
        Assert.True((PopupMenu.build l ignore []).IsNone)
        let menu = (PopupMenu.buildGrouped l (fun e -> hits.Add e.Label) [ "服务一", [ a ]; "空", []; "服务二", [ b ] ]).Value
        let window = show menu
        let panel = (menu :?> ScrollViewer).Content :?> StackPanel
        // 头、项、分隔、头、项
        Assert.Equal(5, panel.Children.Count)
        let ia = byName menu "甲"
        let ib = byName menu "乙"
        Assert.True(ib.Classes.Contains "hover-danger")
        Assert.True(ia.Classes.Contains "hover-plain")
        Assert.True(ia.GetVisualDescendants() |> Seq.exists (function :? Control as c -> c.Name = "check" | _ -> false))
        key ia Key.Enter |> ignore
        key ib Key.Space |> ignore
        Assert.Equal<string list>([ "甲"; "乙" ], List.ofSeq hits)
        window.Close())

[<Fact>]
let ``select button opens on up and down`` () =
    Headless.run (fun () ->
        let _, l = look ()
        let b = SelectButton(l, "模型")
        let mutable opened = 0
        b.OpenRequested.Add(fun () -> opened <- opened + 1)
        let window = show b.View
        key b.View Key.Down |> ignore
        key b.View Key.Up |> ignore
        key b.View Key.Left |> ignore
        Assert.Equal(2, opened)
        Assert.Equal(AutomationControlType.ComboBox, AutomationProperties.GetControlTypeOverride(b.View).Value)
        Assert.True b.View.Focusable
        window.Close())
