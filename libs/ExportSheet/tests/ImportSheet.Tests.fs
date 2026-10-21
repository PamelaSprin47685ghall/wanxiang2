module Wanxiang2.ImportSheet.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.ImportSheet

let look () =
    {
        Text = Brushes.Black
        Muted = Brushes.Gray
        Danger = Brushes.Red
    }

let privacy = "导出文件不含 API 密钥。"

let enter (c: Control) =
    c.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = c))

/// 组合者替身：ImportView.chrome 的规则
let makeVs hasPreview busy status tone details =
    let gate =
        if not hasPreview then ImportGate.Hidden
        elif busy then ImportGate.Disabled
        else ImportGate.Enabled

    let pickText = if hasPreview then "换一个文件…" else "选择 JSON 文件…"

    {
        StatusText = status
        Tone = tone
        Details = details
        ImportGate = gate
        PickEnabled = not busy
        PickText = pickText
    }

// 迁自旧 R14TransferTests `313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`（控件部分；解析与提交在组合者）。
[<Fact>]
let ``313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`` () =
    Headless.run (fun () ->
        let sheet = ImportSheet(look (), privacy)
        let window = Window(Width = 900.0, Height = 700.0, Content = sheet.View)
        window.Show()
        let mutable imports = 0
        sheet.ImportClicked.Add(fun () -> imports <- imports + 1)
        sheet.Render(makeVs false false "还没有选择文件。" ImportTone.Muted None)
        Assert.False sheet.ImportButton.IsVisible
        sheet.Render(makeVs false false "这不是有效的 JSON 文件。" ImportTone.Danger None)
        Assert.Contains("不是有效的 JSON", sheet.StatusText)
        let details = Some [ "文件：旅行计划.json"; "导入后的标题：旅行计划（导入）" ]
        sheet.Render(makeVs true false "「旅行计划」· 2 条消息" ImportTone.Text details)
        Assert.True sheet.ImportButton.IsVisible
        Assert.Contains("2 条消息", sheet.StatusText)
        Assert.Contains("旅行计划（导入）", sheet.DetailsText)
        enter sheet.ImportButton
        Assert.Equal(1, imports)
        // 提交中：导入键不可用，再按无效
        sheet.Render(makeVs true true "正在导入…" ImportTone.Text details)
        enter sheet.ImportButton
        Assert.Equal(1, imports)
        Assert.False sheet.PickButton.IsEnabled
        // 失败可重试
        sheet.Render(makeVs true false "导入没有完成，主机拒绝了这份内容或连接已断开。可以再试一次。" ImportTone.Danger details)
        Assert.Contains("没有完成", sheet.StatusText)
        Assert.True sheet.ImportButton.IsEnabled
        enter sheet.ImportButton
        Assert.Equal(2, imports)
        window.Close())

[<Fact>]
let ``buttons carry names, help, theme classes and the privacy note`` () =
    Headless.run (fun () ->
        let sheet = ImportSheet(look (), privacy)
        let window = Window(Width = 600.0, Height = 500.0, Content = sheet.View)
        window.Show()
        let mutable picks = 0
        let mutable cancels = 0
        sheet.PickClicked.Add(fun () -> picks <- picks + 1)
        sheet.CancelClicked.Add(fun () -> cancels <- cancels + 1)
        Assert.Equal("选择要导入的 JSON 文件", AutomationProperties.GetName sheet.PickButton)
        sheet.Render(makeVs true false "「旅行计划」· 2 条消息" ImportTone.Text None)
        Assert.Equal("换一个文件…", AutomationProperties.GetName sheet.PickButton)
        Assert.Equal("在这台主机上新建一个对话并写入文件里的消息", AutomationProperties.GetHelpText sheet.ImportButton)
        Assert.True(sheet.ImportButton.Classes.Contains "button-primary")
        Assert.True(sheet.PickButton.Classes.Contains "button-secondary")
        Assert.True(sheet.CloseButton.Classes.Contains "button-ghost")

        let note =
            (sheet.View :?> StackPanel).Children
            |> Seq.pick (function
                | :? TextBlock as t when t.Name = "import-privacy-note" -> Some t
                | _ -> None)

        Assert.Equal(privacy, note.Text)
        enter sheet.PickButton
        enter sheet.CloseButton
        Assert.Equal((1, 1), (picks, cancels))
        sheet.FocusImport()
        Dispatcher.UIThread.RunJobs()
        Assert.True(sheet.ImportButton.IsVisible)
        Assert.True sheet.ImportButton.IsFocused
        sheet.Render(makeVs true false "「旅行计划」· 2 条消息" ImportTone.Text None)

        Assert.False(
            (sheet.View :?> StackPanel).Children
            |> Seq.exists (fun c -> c.Name = "import-details" && c.IsVisible)
        )

        window.Close())

// 字号 / 行高 / 间距走主题动态资源键（不再经 Appearance 记录传活值）。
[<Fact>]
let ``字号行高与间距走主题资源键`` () =
    Headless.run (fun () ->
        let r = Avalonia.Application.Current.Resources
        r["Metric.FontTitle"] <- box 18.0
        r["Metric.FontBody"] <- box 14.0
        r["Metric.FontSmall"] <- box 12.0
        r["Metric.FontCaption"] <- box 12.0
        r["Metric.CaptionLineHeight"] <- box 18.0
        r["Space.Xl"] <- box 12.0
        r["Space.Md"] <- box 8.0
        let sheet = ImportSheet(look (), privacy)
        let window = Window(Width = 600.0, Height = 500.0, Content = sheet.View)
        window.Show()
        Dispatcher.UIThread.RunJobs()

        let heading =
            window.GetVisualDescendants()
            |> Seq.pick (function
                | :? TextBlock as t when t.Text = "导入对话" -> Some t
                | _ -> None)

        Assert.Equal(18.0, heading.FontSize)

        let caption =
            window.GetVisualDescendants()
            |> Seq.pick (function
                | :? TextBlock as t when t.Text = privacy -> Some t
                | _ -> None)

        Assert.Equal(12.0, caption.FontSize)
        Assert.Equal(18.0, caption.LineHeight)
        Assert.Equal(12.0, (sheet.View :?> StackPanel).Spacing)
        window.Close())
