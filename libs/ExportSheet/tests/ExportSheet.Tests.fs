module Wanxiang2.ExportSheet.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.ExportSheet

let look () =
    { Text = Brushes.Black; Muted = Brushes.Gray; Success = Brushes.Green; Danger = Brushes.Red }

let privacy = "导出文件不含 API 密钥。"


let reading reduced =
    { StatusText = "正在读取…"
      Tone = ExportSheetTone.Muted
      Progress = if reduced then ExportSheetProgress.Determinate 0.0 else ExportSheetProgress.Indeterminate
      Action = ExportSheetAction.None
      CloseText = "取消导出" }
let failed = { reading false with StatusText = "测试直接推到失败终态"; Tone = ExportSheetTone.Danger; Progress = ExportSheetProgress.Hidden; Action = ExportSheetAction.Retry; CloseText = "关闭" }
let cancelled = { failed with StatusText = "导出已取消。"; Tone = ExportSheetTone.Text }
let ready = { failed with StatusText = "已读取 5 条消息。"; Tone = ExportSheetTone.Text; Progress = ExportSheetProgress.Hidden; Action = ExportSheetAction.Save(ExportSaveGate.Enabled, true) }
let savingNow = { ready with Action = ExportSheetAction.Save(ExportSaveGate.Disabled, true) }

let host w (sheet: ExportSheet) =
    let window = Window(Content = sheet.View, Width = w, Height = 600.0)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    window

let key (target: Control) k =
    let args = KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = KeyModifiers.None, Source = target)
    target.RaiseEvent args
    Dispatcher.UIThread.RunJobs()
    args

let byName (root: Visual) name =
    root.GetVisualDescendants() |> Seq.choose (function :? Control as c -> Some c | _ -> None) |> Seq.find (fun c -> AutomationProperties.GetName c = name)

let invoke control =
    (ControlAutomationPeer.CreatePeerForElement control |> Assert.IsAssignableFrom<IInvokeProvider>).Invoke()
    Dispatcher.UIThread.RunJobs()

// 迁自旧 ConversationExportTests。
[<Theory>]
[<InlineData(390.0)>]
[<InlineData(900.0)>]
let ``export footer arrows move focus between the three buttons`` (width: float) =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "完整导出", privacy)
        let window = host width sheet
        sheet.Render(reading false)
        let close = byName window "取消导出"
        let retry = byName window "重新导出"
        let save = byName window "保存 Markdown"
        close.Focus() |> ignore
        let blocked = key close Key.Right
        Assert.False(blocked.Handled, "邻键禁用时方向键不冒领")
        Assert.True close.IsFocused
        sheet.Render failed
        Assert.True retry.IsEffectivelyVisible
        Assert.True retry.IsEnabled
        close.Focus() |> ignore
        Assert.True((key close Key.Right).Handled)
        Assert.True retry.IsFocused
        Assert.False save.IsEffectivelyVisible
        Assert.False((key retry Key.Right).Handled)
        Assert.True retry.IsFocused
        Assert.True((key retry Key.Left).Handled)
        Assert.True close.IsFocused
        window.Close())

[<Fact>]
let ``export ready state skips the hidden retry slot for arrow navigation`` () =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "就绪态方向键", privacy)
        let window = host 480.0 sheet
        sheet.Render ready
        let close = byName window "关闭"
        let retry = byName window "重新导出"
        Assert.True (byName window "保存 Markdown").IsEffectivelyVisible
        Assert.False retry.IsEffectivelyVisible
        close.Focus() |> ignore
        Assert.True((key close Key.Right).Handled)
        let focused = window.FocusManager.GetFocusedElement() :?> Control
        Assert.Equal("保存 JSON", AutomationProperties.GetName focused)
        Assert.True((key focused Key.Left).Handled)
        Assert.True close.IsFocused
        window.Close())

[<Theory>]
[<InlineData(390.0)>]
[<InlineData(900.0)>]
let ``export save action appears only after the last page and fits the window`` (width: float) =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "完整导出", privacy)
        let root = Grid()
        root.Children.Add sheet.View
        let window = Window(Content = root, Width = width, Height = 600.0)
        window.Show()
        sheet.Render(reading false)
        Dispatcher.UIThread.RunJobs()
        Assert.False sheet.SaveButton.IsVisible
        sheet.Render ready
        Dispatcher.UIThread.RunJobs()
        Assert.True sheet.SaveButton.IsVisible
        Assert.True sheet.SaveButton.IsEnabled
        let save = sheet.SaveButton
        Assert.True(save.Bounds.Width > 0.0)
        Assert.True(save.Bounds.Height > 0.0)
        let origin = save.TranslatePoint(Point(0.0, 0.0), root).Value
        Assert.True(origin.X >= 0.0 && origin.X + save.Bounds.Width <= root.Bounds.Width + 0.5)
        Assert.True(origin.Y >= 0.0 && origin.Y + save.Bounds.Height <= root.Bounds.Height + 0.5)
        window.Close())

[<Fact>]
let ``cancel stays in the dialog and offers the retry entry`` () =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "取消后可重试", privacy)
        let window = host 480.0 sheet
        // 组合者替身：读取中点关闭键 = 取消，重试 = 回到读取
        sheet.CloseClicked.Add(fun () -> sheet.Render cancelled)
        sheet.RetryClicked.Add(fun () -> sheet.Render(reading false))
        sheet.Render(reading false)
        invoke (byName window "取消导出")
        Assert.Equal("导出已取消。", sheet.StatusText)
        let retry = byName window "重新导出"
        Assert.True retry.IsVisible
        invoke retry
        let bars = window.GetVisualDescendants() |> Seq.choose (function :? ProgressBar as b -> Some b | _ -> None) |> List.ofSeq
        Assert.Equal(1, bars.Length)
        Assert.True bars.Head.IsVisible
        Assert.False retry.IsVisible
        window.Close())

[<Fact>]
let ``export dialog returns focus to close when the save slot becomes unusable`` () =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "禁用态焦点", privacy)
        let window = host 480.0 sheet
        sheet.Render ready
        let close = byName window "关闭"
        let save = byName window "保存 Markdown"
        save.Focus() |> ignore
        Assert.True save.IsFocused
        sheet.Render savingNow
        Dispatcher.UIThread.RunJobs()
        Assert.False save.IsEnabled
        Assert.True(close.IsFocused, "保存键不可用时焦点必须回到始终可用的关闭键")
        window.Close())

// 迁自旧 PresentationRefreshTests。
[<Fact>]
let ``export progress bar is determinate under reduced motion`` () =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "测试对话", privacy)
        let window = host 480.0 sheet
        sheet.Render(reading true)
        Assert.False sheet.Progress.IsIndeterminate
        sheet.Render(reading false)
        Assert.True sheet.Progress.IsIndeterminate
        window.Close())

// 迁自旧 R14TransferTests `312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`（界面部分）。
[<Fact>]
let ``312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`` () =
    Headless.run (fun () ->
        let sheet = ExportSheet(look (), "旅行计划", privacy)
        let window = host 900.0 sheet
        let saves = ResizeArray<bool>()
        sheet.SaveClicked.Add saves.Add
        sheet.Render ready
        let texts = window.GetVisualDescendants() |> Seq.choose (function :? TextBlock as t -> Option.ofObj t.Text | _ -> None) |> String.concat "\n"
        Assert.Contains("不含 API 密钥", texts)
        Assert.Contains("保存 JSON", texts)
        invoke sheet.SaveJsonButton
        invoke sheet.SaveButton
        Assert.Equal<bool list>([ true; false ], List.ofSeq saves)
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(window.GetVisualDescendants() |> Seq.pick (function :? TextBlock as t when t.Text = "已读取 5 条消息。" -> Some t | _ -> None)))
        window.Close())

// 字号 / 行高 / 间距走主题动态资源键（不再经 Appearance 记录传活值）。
[<Fact>]
let ``字号行高与间距走主题资源键`` () =
    Headless.run (fun () ->
        let r = Avalonia.Application.Current.Resources
        r["Metric.FontTitle"] <- box 18.0
        r["Metric.FontBody"] <- box 14.0
        r["Metric.FontCaption"] <- box 12.0
        r["Metric.CaptionLineHeight"] <- box 18.0
        r["Space.Xl"] <- box 12.0
        r["Space.Md"] <- box 8.0
        let sheet = ExportSheet(look (), "完整导出", privacy)
        let window = host 480.0 sheet
        let heading =
            window.GetVisualDescendants()
            |> Seq.pick (function :? TextBlock as t when t.Text = "导出对话" -> Some t | _ -> None)
        Assert.Equal(18.0, heading.FontSize)
        let caption =
            window.GetVisualDescendants()
            |> Seq.pick (function :? TextBlock as t when t.Text = privacy -> Some t | _ -> None)
        Assert.Equal(12.0, caption.FontSize)
        Assert.Equal(18.0, caption.LineHeight)
        Assert.Equal(12.0, (sheet.View :?> StackPanel).Spacing)
        window.Close())
