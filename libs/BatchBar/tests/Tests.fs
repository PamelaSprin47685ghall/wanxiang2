module Wanxiang2.BatchBar.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.BatchBar
open Wanxiang2.BatchBar.Tests.Probe

let private look () =
    let divider = Cell 1.0
    divider,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = Cell 14.0; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = Cell 8.0; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      HeaderSurface = Looks.brush Colors.WhiteSmoke; BarSurface = Looks.brush Colors.White; Hairline = Looks.brush Colors.Gainsboro
      DividerWidth = divider; SelectButtonPaddingY = Cell 4.0 }

let private state n =
    { CountText = sprintf "已选 %d 项" n; SelectAllText = (if n = 3 then "取消全选" else "全选"); SelectAllHelp = "全选可见对话"
      PinText = "置顶"; SelectAllEnabled = true; BatchEnabled = n > 0; Visible = true }

let private make () =
    let d, l = look ()
    let b = BatchBar l
    let panel = StackPanel()
    panel.Children.Add b.Header
    panel.Children.Add b.ActionBar
    let w = host panel
    b, w, d

let private press (w: Window) k =
    w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null)
    w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``selection action buttons disable themselves while nothing is selected`` () =
    Headless.run (fun () ->
        let b, _, _ = make ()
        b.Render(state 0)
        for c in [ b.PinButton; b.ArchiveButton; b.DeleteButton ] do
            Assert.False c.IsEnabled
            Assert.False c.Focusable
        b.Render(state 2)
        for c in [ b.PinButton; b.ArchiveButton; b.DeleteButton ] do Assert.True c.IsEnabled)

[<Fact>]
let ``selection buttons rename themselves as the selection changes`` () =
    Headless.run (fun () ->
        let b, _, _ = make ()
        b.Render(state 3)
        Assert.Equal("已选 3 项", b.CountText)
        Assert.Equal("取消全选", AutomationProperties.GetName b.SelectAllButton)
        b.Render({ state 1 with PinText = "取消置顶" })
        Assert.Equal("全选", AutomationProperties.GetName b.SelectAllButton)
        Assert.Contains("取消置顶", textOf b.ActionBar))

[<Fact>]
let ``selection action bar arrows walk the batch buttons`` () =
    Headless.run (fun () ->
        let b, w, _ = make ()
        b.Render(state 2)
        b.PinButton.Focus() |> ignore
        press w Key.Right
        Assert.True b.ArchiveButton.IsFocused
        press w Key.Right
        Assert.True b.DeleteButton.IsFocused
        press w Key.Left
        Assert.True b.ArchiveButton.IsFocused)

[<Fact>]
let ``every batch control raises its event through invoke and the divider follows the theme`` () =
    Headless.run (fun () ->
        let b, _, d = make ()
        b.Render(state 2)
        let log = ResizeArray<string>()
        b.ExitClicked.Add(fun () -> log.Add "exit")
        b.SelectAllClicked.Add(fun () -> log.Add "all")
        b.PinClicked.Add(fun () -> log.Add "pin")
        b.ArchiveClicked.Add(fun () -> log.Add "archive")
        b.DeleteClicked.Add(fun () -> log.Add "delete")
        click (named b.Header "退出多选（Esc）").Value
        for c in [ b.SelectAllButton; b.PinButton; b.ArchiveButton; b.DeleteButton ] do click c
        Assert.Equal<string list>([ "exit"; "all"; "pin"; "archive"; "delete" ], List.ofSeq log)
        d.Set 2.0
        Assert.Equal(2.0, (b.ActionBar :?> Border).BorderThickness.Top))
