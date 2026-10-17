module Wanxiang2.UiPrimitives.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.UiPrimitives

let private look () =
    { BodyFontSize = Cell(14.0)
      BodyForeground = SolidColorBrush(Colors.Black)
      CaptionFontSize = Cell(12.0)
      CaptionForeground = SolidColorBrush(Colors.Gray)
      CaptionLineHeight = Cell(16.0)
      FieldLabelFontSize = Cell(13.0)
      FieldLabelForeground = SolidColorBrush(Colors.DarkGray)
      FieldLabelTracking = Cell(0.0)
      FieldInsetX = Cell(0.0)
      FieldLabelGap = Cell(4.0)
      TitleFontSize = Cell(18.0)
      TitleForeground = SolidColorBrush(Colors.Black)
      EmphasisTracking = Cell(0.0)
      FieldSurface = SolidColorBrush(Colors.White)
      FieldBorder = SolidColorBrush(Colors.Gray)
      FieldBorderWidth = Cell(1.0)
      FieldRadius = Cell(CornerRadius 4.0)
      FieldPadding = Cell(Thickness 8.0)
      FieldTextMinHeight = Cell(32.0)
      FieldFontSize = Cell(14.0)
      FieldText = SolidColorBrush(Colors.Black)
      FieldCaret = SolidColorBrush(Colors.Black)
      MutedForeground = SolidColorBrush(Colors.Gray)
      OnAccentForeground = SolidColorBrush(Colors.White)
      SpinnerTrack = SolidColorBrush(Colors.LightGray)
      SpinnerAccent = SolidColorBrush(Colors.Blue)
      SpinnerStrokeWidth = Cell(2.0)
      SpinnerCanvasSize = Cell(16.0)
      SpinnerFrame = TimeSpan.FromMilliseconds 80.0
      Reduced = Cell(false) }

[<Fact>]
let ``button 按 tone 挂类名`` () =
    Headless.run (fun () ->
        let primary = Primitives.button ButtonTone.Primary "确定" ignore
        Assert.Contains("button-primary", primary.Classes)
        Assert.Contains("surface", primary.Classes)
        Assert.Contains("hover-accent", primary.Classes)
        let ghost = Primitives.button ButtonTone.Ghost "取消" ignore
        Assert.Contains("button-ghost", ghost.Classes)
        Assert.Contains("hover-plain", ghost.Classes)
        let danger = Primitives.button ButtonTone.Danger "删除" ignore
        Assert.Contains("button-danger", danger.Classes)
        Assert.Contains("hover-danger", danger.Classes))

[<Fact>]
let ``tagWith 按 tone 挂类名`` () =
    Headless.run (fun () ->
        let neutral = Primitives.tagWith Primitives.TagTone.Neutral "默认"
        Assert.Contains("tag", neutral.Classes)
        Assert.DoesNotContain("tag-warning", neutral.Classes)
        Assert.DoesNotContain("tag-danger", neutral.Classes)
        let warning = Primitives.tagWith Primitives.TagTone.Warning "警告"
        Assert.Contains("tag-warning", warning.Classes)
        let danger = Primitives.tagWith Primitives.TagTone.Danger "危险"
        Assert.Contains("tag-danger", danger.Classes))

[<Fact>]
let ``toggle 翻转、写值与回调`` () =
    Headless.run (fun () ->
        let mutable seen = []
        let _, read, write, flip = Primitives.toggle false (fun v -> seen <- v :: seen)
        Assert.False(read ())
        flip ()
        Assert.True(read ())
        Assert.Equal<bool list>([ true ], seen)
        write false
        Assert.False(read ())
        // write 只改值并刷新视觉，不触发 onChanged（回调由 flip / 指针 / 键盘触发）
        Assert.Equal<bool list>([ true ], seen))

[<Fact>]
let ``setEnabled 切换可操作性`` () =
    Headless.run (fun () ->
        let host = Border()
        Primitives.setEnabled host false
        Assert.False(host.IsEnabled)
        Assert.False(host.IsHitTestVisible)
        Assert.False(host.Focusable)
        Primitives.setEnabled host true
        Assert.True(host.IsEnabled)
        Assert.True(host.IsHitTestVisible)
        Assert.True(host.Focusable))

[<Fact>]
let ``setSquareTarget 固定命中尺寸`` () =
    Headless.run (fun () ->
        let host = Border()
        Primitives.setSquareTarget host 28.0
        Assert.Equal(28.0, host.Width)
        Assert.Equal(28.0, host.Height)
        Assert.Equal(28.0, host.MinWidth)
        Assert.Equal(28.0, host.MinHeight))

[<Fact>]
let ``setReservedActionVisible 保留槽位只切反馈`` () =
    Headless.run (fun () ->
        let host = Border(IsVisible = false)
        Primitives.setReservedActionVisible host false
        Assert.True(host.IsVisible)
        Assert.Equal(0.0, host.Opacity)
        Assert.False(host.IsHitTestVisible)
        Assert.False(host.Focusable))

[<Fact>]
let ``textField 返回外壳与无边框输入框`` () =
    Headless.run (fun () ->
        let l = look ()
        let shell, box = Primitives.textField l "占位"
        Assert.Contains("surface", shell.Classes)
        Assert.Contains("focus-soft", shell.Classes)
        Assert.Contains("hover-raised", shell.Classes)
        Assert.Equal(Thickness 0.0, box.BorderThickness)
        Assert.Equal("占位", box.PlaceholderText))

[<Fact>]
let ``活值跟随主题管理者`` () =
    Headless.run (fun () ->
        let bodyFont = Cell(14.0)
        let l = { look () with BodyFontSize = bodyFont }
        let text = Primitives.label l "正文"
        Assert.Equal(14.0, text.FontSize)
        bodyFont.Set 20.0
        Assert.Equal(20.0, text.FontSize))
