module Wanxiang2.FieldCheck.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.FieldCheck

let private brushes () =
    SolidColorBrush(Colors.Red), SolidColorBrush(Colors.Gray)

/// 主题资源字典的替身：FieldCheck 绑资源键，这里只往应用资源写值。
/// 返回错误前景画笔，供改色断言。
let private seedResources (danger: SolidColorBrush) (normal: SolidColorBrush) =
    let r = Application.Current.Resources
    r["Palette.Danger"] <- box danger
    r["Palette.Border"] <- box normal
    r["Metric.FontCaption"] <- box 11.0
    r["Metric.CaptionLineHeight"] <- box 16.0
    r["Margin.FieldError"] <- box (Thickness 2.0)

let private field () =
    let box = TextBox()
    let shell = Border(BorderThickness = Thickness 1.0, Child = box)
    shell, box

// 迁自旧 UiStabilityTests。
[<Fact>]
let ``field validation is local and does not change geometry`` () =
    Headless.run (fun () ->
        let danger, normal = brushes ()
        seedResources danger normal
        let check = FieldCheck()
        let shell, box = field ()
        let panel = StackPanel()
        panel.Children.Add shell
        panel.Children.Add(check.MessageFor box)
        let w = Window(Width = 420.0, Height = 130.0, Content = panel)
        w.Show()
        let before = shell.BorderThickness
        check.ApplyError(box, "端点地址不能为空。")
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(before, shell.BorderThickness)
        Assert.Same(danger :> IBrush, shell.BorderBrush)
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
        check.ApplyError(box, "再次验证错误")
        check.Clear box
        Dispatcher.UIThread.RunJobs()
        Assert.False message.IsVisible
        Assert.Equal(before, shell.BorderThickness)
        w.Close())

[<Fact>]
let ``文本没变（聚焦重放）不清错误`` () =
    Headless.run (fun () ->
        let danger, normal = brushes ()
        seedResources danger normal
        let check = FieldCheck()
        let _, box = field ()
        box.Text <- "x"
        check.ApplyError(box, "错")
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
let ``资源键改值后错误行与输入壳描边跟着变`` () =
    Headless.run (fun () ->
        let danger, normal = brushes ()
        seedResources danger normal
        let check = FieldCheck()
        let shell, fieldBox = field ()
        // 资源查找沿可视树向上：壳必须挂在窗口里才吃得到应用资源
        let panel = StackPanel()
        panel.Children.Add shell
        panel.Children.Add(check.MessageFor fieldBox)
        let w = Window(Width = 420.0, Height = 130.0, Content = panel)
        w.Show()
        let line = check.MessageFor fieldBox
        check.ApplyError(fieldBox, "错")
        // 输入壳错误态描边随资源改色（声明式，无手工订阅）
        danger.Color <- Colors.Orange
        Assert.Equal(Colors.Orange, (shell.BorderBrush :?> SolidColorBrush).Color)
        Application.Current.Resources["Metric.FontCaption"] <- box 13.0
        Assert.Equal(13.0, line.FontSize)
        // 清错后回到常态描边资源
        check.Clear fieldBox
        Assert.Same(normal :> IBrush, shell.BorderBrush)
        w.Close())
