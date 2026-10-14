module Wanxiang2.FieldCheck.Tests.Tests

open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.FieldCheck

let private look () =
    let danger = SolidColorBrush(Colors.Red)
    let normal = SolidColorBrush(Colors.Gray)
    let size = Cell 11.0
    danger,
    size,
    { ErrorForeground = danger
      InvalidStroke = danger
      NormalStroke = normal
      MessageFontSize = size
      MessageLineHeight = Cell 16.0
      MessageMargin = Cell(Thickness 2.0) }

let private field () =
    let box = TextBox()
    let shell = Border(BorderThickness = Thickness 1.0, Child = box)
    shell, box

// 迁自旧 UiStabilityTests。
[<Fact>]
let ``field validation is local and does not change geometry`` () =
    Headless.run (fun () ->
        let danger, _, l = look ()
        let check = FieldCheck l
        let shell, box = field ()
        let panel = StackPanel()
        panel.Children.Add shell
        panel.Children.Add(check.MessageFor box)
        let w = Window(Width = 420.0, Height = 130.0, Content = panel)
        w.Show()
        let before = shell.BorderThickness
        check.SetError(box, "端点地址不能为空。")
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(before, shell.BorderThickness)
        Assert.Same(danger, shell.BorderBrush)
        Assert.True(FieldCheck.IsInvalid shell)
        Assert.Contains(FieldCheck.InvalidClass, shell.Classes)
        let message = check.MessageFor box
        Assert.True message.IsVisible
        Assert.Equal("端点地址不能为空。", message.Text)
        Assert.Equal("端点地址不能为空。", AutomationProperties.GetHelpText box)
        box.Text <- "https://example.com"
        Dispatcher.UIThread.RunJobs()
        Assert.False message.IsVisible
        Assert.Equal("", AutomationProperties.GetHelpText box)
        Assert.False(FieldCheck.IsInvalid shell)
        check.SetError(box, "再次验证错误")
        check.Clear box
        Dispatcher.UIThread.RunJobs()
        Assert.False message.IsVisible
        Assert.Equal(before, shell.BorderThickness)
        w.Close())

[<Fact>]
let ``文本没变（聚焦重放）不清错误`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let check = FieldCheck l
        let _, box = field ()
        box.Text <- "x"
        check.SetError(box, "错")
        box.Text <- "x"
        Assert.True(check.HasError box))

[<Fact>]
let ``validation feedback with a single error toasts the message and hides the summary`` () =
    Headless.run (fun () ->
        let box = TextBox()
        let summary = TextBlock(IsVisible = true, Focusable = true)
        Assert.Equal(Some "内容不能为空。", FieldCheck.Apply([ box, "内容不能为空。" ], Some summary))
        Assert.False(summary.IsVisible))

[<Fact>]
let ``validation feedback with multiple errors fills the focusable summary`` () =
    Headless.run (fun () ->
        let first = TextBox()
        let second = TextBox()
        let summary = TextBlock(IsVisible = false, Focusable = true)
        Assert.Equal(Some "有 2 处需要修正，请查看表单顶部摘要。", FieldCheck.Apply([ first, "甲错"; second, "乙错" ], Some summary))
        Assert.True(summary.IsVisible)
        Assert.Equal("有 2 处需要修正：甲错；乙错", summary.Text))

[<Fact>]
let ``validation feedback with no errors stays silent and hides the summary`` () =
    Headless.run (fun () ->
        let summary = TextBlock(IsVisible = true)
        Assert.Equal(None, FieldCheck.Apply([], Some summary))
        Assert.False(summary.IsVisible))

[<Fact>]
let ``摘要在用户改任一字段时收起`` () =
    Headless.run (fun () ->
        let a, b = TextBox(), TextBox()
        let summary = TextBlock(IsVisible = false, Focusable = true)
        FieldCheck.BindSummaryReset(Some summary, [ a; b ])
        FieldCheck.Apply([ a, "甲"; b, "乙" ], Some summary) |> ignore
        Assert.True summary.IsVisible
        b.Text <- "改了"
        Assert.False summary.IsVisible
        Assert.Equal("", summary.Text))

[<Fact>]
let ``外观实例改值后错误行跟着变`` () =
    Headless.run (fun () ->
        let danger, size, l = look ()
        let check = FieldCheck l
        let line = check.MessageFor(TextBox())
        size.Set 13.0
        danger.Color <- Colors.Orange
        Assert.Equal(13.0, line.FontSize)
        Assert.Equal(Colors.Orange, (line.Foreground :?> SolidColorBrush).Color))
