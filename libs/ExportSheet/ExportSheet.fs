namespace Wanxiang2.ExportSheet

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 状态行颜色档（读取中退一档；已保存 / 出错 / 其余）
[<RequireQualifiedAccess>]
type ExportSheetTone =
    | Muted
    | Text
    | Success
    | Danger

/// 一帧呈现（组合者从 ExportView.decide 得出，字段一一对应）。
type ExportSheetState =
    { StatusText: string
      Tone: ExportSheetTone
      ProgressVisible: bool
      Indeterminate: bool
      ProgressValue: float
      RetryVisible: bool
      SaveVisible: bool
      SaveJsonVisible: bool
      SaveEnabled: bool
      SaveJsonEnabled: bool
      CloseText: string }

/// 外观。全部由主题管理者提供活的实例。
type ExportSheetLook =
    { Text: IBrush
      Muted: IBrush
      Success: IBrush
      Danger: IBrush
      TitleFontSize: IObservable<float>
      BodyFontSize: IObservable<float>
      CaptionFontSize: IObservable<float>
      CaptionLineHeight: IObservable<float>
      StackSpacing: IObservable<float>
      ButtonSpacing: IObservable<float> }

/// 导出对话框的内容（迁自旧 Views/ExportDialog.fs 的界面部分）：标题、对话名、assertive 状态行、进度条、说明、
/// 「取消导出 / 重新导出 / 保存 JSON / 保存 Markdown」四键与左右方向键接线。
/// 分页读取、节流、到期、生成文件与保存窗口由组合者接（ConversationExport、ExportView、ProgressThrottle、ConversationTransfer）。
type ExportSheet(look: ExportSheetLook, conversationTitle: string, privacyNote: string) =
    let live (o: IObservable<'T>) f = o.Subscribe(fun v -> f v) |> ignore
    let close = Event<unit>()
    let retry = Event<unit>()
    let save = Event<bool>()

    let caption (text: string) =
        let t = TextBlock(Text = text, Foreground = look.Muted, TextWrapping = TextWrapping.Wrap)
        live look.CaptionFontSize (fun v -> t.FontSize <- v)
        live look.CaptionLineHeight (fun v -> t.LineHeight <- v)
        t
    let heading = TextBlock(Text = "导出对话", FontWeight = FontWeight.Medium, Foreground = look.Text)
    let status = TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Text)
    let progress = ProgressBar(IsIndeterminate = false, Minimum = 0.0, Maximum = 100.0)
    let closeButton = Tap("取消导出", TapTone.ghost, close.Trigger)
    let retryButton = Tap("重新导出", TapTone.secondary, retry.Trigger)
    let saveButton = Tap("保存 Markdown", TapTone.primary, (fun () -> save.Trigger false))
    let saveJsonButton = Tap("保存 JSON", TapTone.secondary, (fun () -> save.Trigger true))
    let rowButtons: Tap list = [ closeButton; retryButton; saveJsonButton; saveButton ]
    let content = StackPanel(Orientation = Orientation.Vertical)

    /// 沿行内找下一个「可见且可用」的键：中间态被藏掉的键不是必经之路。
    let step (from: Tap) (dir: int) =
        match rowButtons |> List.tryFindIndex (fun b -> obj.ReferenceEquals(b, from)) with
        | None -> None
        | Some idx ->
            let order = if dir > 0 then [ idx + 1 .. rowButtons.Length - 1 ] else [ idx - 1 .. -1 .. 0 ]
            order |> List.tryPick (fun i -> let b = rowButtons[i] in if b.IsEnabled && b.IsEffectivelyVisible then Some b else None)

    do
        live look.TitleFontSize (fun v -> heading.FontSize <- v)
        live look.BodyFontSize (fun v -> status.FontSize <- v)
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Assertive)
        AutomationProperties.SetHelpText(closeButton, "读取中取消导出（对话框保持打开，可重新导出）；取消后此按钮为关闭 (Esc 随时关闭)")
        AutomationProperties.SetHelpText(retryButton, "重新读取并导出当前对话")
        AutomationProperties.SetHelpText(saveButton, "保存导出的 Markdown 文件")
        AutomationProperties.SetHelpText(saveJsonButton, "保存可再导入的 JSON 文件，含分叉与版本信息")
        for b in rowButtons do
            b.KeyDown.Add(fun e ->
                let target = if e.Key = Key.Right then step b 1 elif e.Key = Key.Left then step b -1 else None
                match target with
                | Some next ->
                    e.Handled <- true
                    next.Focus(NavigationMethod.Directional) |> ignore
                | None -> ())
        let buttons = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center)
        live look.ButtonSpacing (fun v -> buttons.Spacing <- v)
        for b in rowButtons do buttons.Children.Add b
        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        let privacy = caption privacyNote
        privacy.Name <- "export-privacy-note"
        live look.StackSpacing (fun v -> content.Spacing <- v)
        for c in
            [ heading :> Control
              caption conversationTitle
              status
              progress
              divider
              caption "导出开始时已保存的消息、工具记录和附件说明。未完成的生成、排队消息及附件文件本体不包含在内；这不是完整备份。"
              caption "Markdown 适合阅读和分享；JSON 保留完整结构和分叉信息，可以在任意万象主机上「导入对话」。"
              privacy
              buttons ] do
            content.Children.Add c

    /// 对话框内容（组合者交给 OverlayLayer.ShowDialog，旧宽 480）
    member _.View: Control = content
    member _.StatusText = status.Text
    member _.Progress = progress
    member _.CloseButton: Control = closeButton
    member _.RetryButton: Control = retryButton
    member _.SaveButton: Control = saveButton
    member _.SaveJsonButton: Control = saveJsonButton

    /// 把一帧呈现同步到控件。保存键不可用且全局焦点已空时，焦点补到关闭键。
    member _.Render(s: ExportSheetState) =
        status.Text <- s.StatusText
        status.Foreground <-
            match s.Tone with
            | ExportSheetTone.Muted -> look.Muted
            | ExportSheetTone.Success -> look.Success
            | ExportSheetTone.Danger -> look.Danger
            | ExportSheetTone.Text -> look.Text
        if s.ProgressVisible then
            progress.IsIndeterminate <- s.Indeterminate
            progress.Value <- s.ProgressValue
        progress.IsVisible <- s.ProgressVisible
        retryButton.IsVisible <- s.RetryVisible
        saveButton.IsVisible <- s.SaveVisible
        saveJsonButton.IsVisible <- s.SaveJsonVisible
        saveButton.SetEnabled s.SaveEnabled
        saveJsonButton.SetEnabled s.SaveJsonEnabled
        closeButton.SetText s.CloseText
        if not (s.SaveVisible && s.SaveEnabled) then
            match TopLevel.GetTopLevel content with
            | null -> ()
            | top when isNull top.FocusManager -> ()
            | top ->
                match top.FocusManager.GetFocusedElement() with
                | null -> if closeButton.IsEnabled && closeButton.IsEffectivelyVisible then closeButton.Focus() |> ignore
                | _ -> ()

    /// 打开后的初始焦点：读取中唯一可用的取消导出
    member _.FocusClose() =
        Dispatcher.UIThread.Post(fun () -> if closeButton.IsEffectivelyVisible && closeButton.IsEnabled then closeButton.Focus() |> ignore)

    /// 关闭键（读取中组合者当作取消，其余当作关闭）
    [<CLIEvent>] member _.CloseClicked = close.Publish
    [<CLIEvent>] member _.RetryClicked = retry.Publish
    /// 保存；参数 true 为 JSON
    [<CLIEvent>] member _.SaveClicked = save.Publish
