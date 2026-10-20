namespace Wanxiang2.ExportSheet

open System
open Avalonia.Automation
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Threading

/// 动态资源绑定辅助：主题管理者把活值写进 ResourceDictionary，控件绑定即随主题资源生效。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 可点的原生 Button，继承 Button 获得原生自动化、键盘导航与悬停反馈。
type internal Tap(child: Control, name: string, classes: string list, action: unit -> unit) as this =
    inherit Button()

    do
        this.Content <- child
        this.Focusable <- true
        this.VerticalAlignment <- VerticalAlignment.Center
        this.Cursor <- new Cursor(StandardCursorType.Hand)
        for c in classes do this.Classes.Add c
        if not (String.IsNullOrEmpty name) then AutomationProperties.SetName(this, name)
        this.Click.Add(fun _ -> if this.IsEnabled then action ())

    member _.Invoke() = if this.IsEnabled then action ()

    member _.ApplyText(value: string) =
        match this.Content with
        | :? TextBlock as t -> t.Text <- value
        | _ -> ()
        AutomationProperties.SetName(this, value)

    member _.ApplyEnabled(enabled: bool) =
        this.IsEnabled <- enabled

    static member TextButton(text: string, classes: string list, action: unit -> unit) : Tap =
        let t = TextBlock(Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        Tap(t, text, classes, action)

/// 按钮档对应的主题类（旧 Ui.button 的四档）
module internal TapTone =
    let primary = [ "surface"; "button-primary"; "hover-accent" ]
    let secondary = [ "surface"; "button-secondary"; "hover-raised" ]
    let ghost = [ "surface"; "button-ghost"; "hover-plain" ]

/// 状态行颜色档（读取中退一档；已保存 / 出错 / 其余）
[<RequireQualifiedAccess>]
type ExportSheetTone =
    | Muted
    | Text
    | Success
    | Danger

[<RequireQualifiedAccess>]
type ExportSheetProgress =
    | Hidden
    | Indeterminate
    | Determinate of percent: float

[<RequireQualifiedAccess>]
type ExportSaveGate =
    | Disabled
    | Enabled

[<RequireQualifiedAccess>]
type ExportSheetAction =
    | None
    | Retry
    | Save of gate: ExportSaveGate * hasJson: bool

/// 一帧呈现（组合者从 ExportView.decide 得出，字段一一对应）。
type ExportSheetState =
    { StatusText: string
      Tone: ExportSheetTone
      Progress: ExportSheetProgress
      Action: ExportSheetAction
      CloseText: string }

/// 外观。画笔由主题管理者给（同一实例，切主题原地改色）；
/// 字号、行高、间距走主题动态资源键（Metric.FontTitle / Metric.FontBody / Metric.FontCaption /
/// Metric.CaptionLineHeight / Space.Xl / Space.Md），本记录不再有 IObservable 字段。
type ExportSheetAppearance =
    { Text: IBrush
      Muted: IBrush
      Success: IBrush
      Danger: IBrush }

/// 导出对话框的内容（迁自旧 Views/ExportDialog.fs 的界面部分）：标题、对话名、assertive 状态行、进度条、说明、
/// 「取消导出 / 重新导出 / 保存 JSON / 保存 Markdown」四键与左右方向键接线。
/// 分页读取、节流、到期、生成文件与保存窗口由组合者接（ConversationExport、ExportView、ProgressThrottle、ConversationTransfer）。
type ExportSheet(look: ExportSheetAppearance, conversationTitle: string, privacyNote: string) =
    let close = Event<unit>()
    let retry = Event<unit>()
    let save = Event<bool>()

    let caption (text: string) =
        let t = TextBlock(Text = text, Foreground = look.Muted, TextWrapping = TextWrapping.Wrap)
        t.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontCaption") |> ignore
        t.Bind(TextBlock.LineHeightProperty, Dyn.res "Metric.CaptionLineHeight") |> ignore
        t
    let heading = TextBlock(Text = "导出对话", FontWeight = FontWeight.Medium, Foreground = look.Text)
    let status = TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Text)
    let progress = ProgressBar(IsIndeterminate = false, Minimum = 0.0, Maximum = 100.0)
    let closeButton = Tap.TextButton("取消导出", TapTone.ghost, close.Trigger)
    let retryButton = Tap.TextButton("重新导出", TapTone.secondary, retry.Trigger)
    let saveButton = Tap.TextButton("保存 Markdown", TapTone.primary, (fun () -> save.Trigger false))
    let saveJsonButton = Tap.TextButton("保存 JSON", TapTone.secondary, (fun () -> save.Trigger true))
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
        heading.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontTitle") |> ignore
        status.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
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
        buttons.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Md") |> ignore
        for b in rowButtons do buttons.Children.Add b
        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        let privacy = caption privacyNote
        privacy.Name <- "export-privacy-note"
        content.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Xl") |> ignore
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
        match s.Progress with
        | ExportSheetProgress.Hidden ->
            progress.IsVisible <- false
        | ExportSheetProgress.Indeterminate ->
            progress.IsVisible <- true
            progress.IsIndeterminate <- true
        | ExportSheetProgress.Determinate p ->
            progress.IsVisible <- true
            progress.IsIndeterminate <- false
            progress.Value <- p
        let canSave =
            match s.Action with
            | ExportSheetAction.None ->
                retryButton.IsVisible <- false
                saveButton.IsVisible <- false
                saveJsonButton.IsVisible <- false
                false
            | ExportSheetAction.Retry ->
                retryButton.IsVisible <- true
                saveButton.IsVisible <- false
                saveJsonButton.IsVisible <- false
                false
            | ExportSheetAction.Save(gate, hasJson) ->
                let enabled = gate = ExportSaveGate.Enabled
                retryButton.IsVisible <- false
                saveButton.IsVisible <- true
                saveButton.ApplyEnabled enabled
                saveJsonButton.IsVisible <- hasJson
                saveJsonButton.ApplyEnabled (enabled && hasJson)
                enabled
        closeButton.ApplyText s.CloseText
        if not canSave then
            match TopLevel.GetTopLevel content with
            | null -> ()
            | top when isNull top.FocusManager -> ()
            | top ->
                match top.FocusManager.GetFocusedElement() with
                | null -> if closeButton.IsEnabled && closeButton.IsEffectivelyVisible then closeButton.Focus() |> ignore
                | _ -> ()

    /// 打开后的初始焦点：读取中唯一可用的取消导出
    member _.FocusClose() = // 布局首帧排队，非焦点竞态
        Dispatcher.UIThread.Post(fun () -> if closeButton.IsEffectivelyVisible && closeButton.IsEnabled then closeButton.Focus() |> ignore) // 布局首帧排队，非焦点竞态

    /// 关闭键（读取中组合者当作取消，其余当作关闭）
    [<CLIEvent>] member _.CloseClicked = close.Publish
    [<CLIEvent>] member _.RetryClicked = retry.Publish
    /// 保存；参数 true 为 JSON
    [<CLIEvent>] member _.SaveClicked = save.Publish
