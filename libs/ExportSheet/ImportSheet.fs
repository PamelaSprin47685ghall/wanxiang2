namespace Wanxiang2.ImportSheet

open System
open Avalonia
open Avalonia.Automation
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

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 可点的原生 Button，继承 Button 获得原生自动化、键盘导航与悬停反馈。
type internal Tap(child: Control, name: string, classes: string list, action: unit -> unit) as this =
    inherit Button()

    do
        this.Content <- child
        this.Focusable <- true
        this.VerticalAlignment <- VerticalAlignment.Center
        this.Cursor <- new Cursor(StandardCursorType.Hand)

        for c in classes do
            this.Classes.Add c

        if not (String.IsNullOrEmpty name) then
            AutomationProperties.SetName(this, name)

        this.Click.Add(fun _ ->
            if this.IsEnabled then
                action ())

    member _.Invoke() =
        if this.IsEnabled then
            action ()

    member _.ApplyText(value: string) =
        match this.Content with
        | :? TextBlock as t -> t.Text <- value
        | _ -> ()

        AutomationProperties.SetName(this, value)

    member _.ApplyEnabled(enabled: bool) = this.IsEnabled <- enabled

    static member TextButton(text: string, classes: string list, action: unit -> unit) : Tap =
        let t =
            TextBlock(
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        Tap(t, text, classes, action)

/// 按钮档对应的主题类（旧 Ui.button 的四档）
module internal TapTone =
    let primary = [ "surface"; "button-primary"; "hover-accent" ]
    let secondary = [ "surface"; "button-secondary"; "hover-raised" ]
    let ghost = [ "surface"; "button-ghost"; "hover-plain" ]

/// 状态行颜色档
[<RequireQualifiedAccess>]
type ImportTone =
    | Muted
    | Text
    | Danger

[<RequireQualifiedAccess>]
type ImportGate =
    | Hidden
    | Disabled
    | Enabled

/// 导入对话框按键门控呈现（对齐 ImportView.chrome / 状态）
type ImportSheetVS =
    {
        StatusText: string
        Tone: ImportTone
        Details: string list option
        ImportGate: ImportGate
        PickEnabled: bool
        PickText: string
    }

/// 外观。画笔由主题管理者给（同一实例，切主题原地改色）；
/// 字号、行高、间距走主题动态资源键（Metric.FontTitle / Metric.FontBody / Metric.FontSmall /
/// Metric.FontCaption / Metric.CaptionLineHeight / Space.Xl / Space.Md），本记录不再有 IObservable 字段。
type ImportSheetAppearance =
    {
        Text: IBrush
        Muted: IBrush
        Danger: IBrush
    }

/// 导入对话框的内容（迁自旧 Views/ImportDialog.fs 的界面部分）：标题、说明、状态、预览详情、隐私说明、三键。
/// 解析、读文件、提交与超时由组合者接（ImportView、ImportAttempt、ConversationTransfer）；本控件只呈现并发事件。
type ImportSheet(look: ImportSheetAppearance, privacyNote: string) =
    let cancel = Event<unit>()
    let pick = Event<unit>()
    let import = Event<unit>()

    let heading =
        TextBlock(Text = "导入对话", FontWeight = FontWeight.Medium, Foreground = look.Text)

    let caption (text: string) =
        let t =
            TextBlock(Text = text, Foreground = look.Muted, TextWrapping = TextWrapping.Wrap)

        t.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontCaption") |> ignore

        t.Bind(TextBlock.LineHeightProperty, Dyn.res "Metric.CaptionLineHeight")
        |> ignore

        t

    let intro =
        caption "选择从万象「导出（Markdown / JSON）」里保存的 JSON 文件。导入后是这台主机上的一个新对话，不会自动生成回复。"

    let status =
        TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Muted, Name = "import-status")

    let details =
        TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Muted, IsVisible = false, Name = "import-details")

    let privacy = caption privacyNote
    let closeButton = Tap.TextButton("取消", TapTone.ghost, cancel.Trigger)
    let pickButton = Tap.TextButton("选择 JSON 文件…", TapTone.secondary, pick.Trigger)
    let importButton = Tap.TextButton("导入为新对话", TapTone.primary, import.Trigger)
    let content = StackPanel(Orientation = Orientation.Vertical)

    do
        heading.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontTitle") |> ignore
        status.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
        details.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontSmall") |> ignore
        privacy.Name <- "import-privacy-note"
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        // 导入对话框由 overlay.ShowDialog 承载，Esc 会关掉它（ImportDesk 的取消即关弹层）。
        // 取消键的提示必须写明 Esc 可用（a11y 回归「取消按钮的提示文案都写明 Esc」）。
        AutomationProperties.SetHelpText(closeButton, "取消并关闭导入对话框 (Esc)")
        ToolTip.SetTip(closeButton, "取消并关闭导入对话框 (Esc)")
        AutomationProperties.SetName(pickButton, "选择要导入的 JSON 文件")
        AutomationProperties.SetName(importButton, "导入为新对话")
        AutomationProperties.SetHelpText(importButton, "在这台主机上新建一个对话并写入文件里的消息")
        importButton.IsVisible <- false

        let buttons =
            StackPanel(
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            )

        buttons.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Md") |> ignore

        for b in [ closeButton; pickButton; importButton ] do
            buttons.Children.Add b

        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        content.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Xl") |> ignore

        for c in [ heading :> Control; intro; status; details; divider; privacy; buttons ] do
            content.Children.Add c

    /// 对话框内容（组合者交给 OverlayLayer.ShowDialog，旧宽 480）
    member _.View: Control = content
    member _.StatusText = status.Text
    member _.DetailsText = details.Text
    member _.ImportButton: Control = importButton
    member _.PickButton: Control = pickButton
    member _.CloseButton: Control = closeButton

    /// 单入口幂等渲染
    member _.Render(vs: ImportSheetVS) =
        status.Text <- vs.StatusText

        status.Foreground <-
            match vs.Tone with
            | ImportTone.Muted -> look.Muted
            | ImportTone.Text -> look.Text
            | ImportTone.Danger -> look.Danger

        match vs.Details with
        | Some ls ->
            details.Text <- String.Join("\n", ls)
            details.IsVisible <- true
        | None -> details.IsVisible <- false

        match vs.ImportGate with
        | ImportGate.Hidden -> importButton.IsVisible <- false
        | ImportGate.Disabled ->
            importButton.IsVisible <- true
            importButton.ApplyEnabled false
        | ImportGate.Enabled ->
            importButton.IsVisible <- true
            importButton.ApplyEnabled true

        pickButton.ApplyEnabled vs.PickEnabled
        pickButton.ApplyText vs.PickText

    /// 打开后与预览就绪后的初始焦点（投递到下一轮，按钮可见才聚焦）
    member _.FocusPick() =
        Dispatcher.UIThread.Post(fun () ->
            if pickButton.IsEffectivelyVisible then
                pickButton.Focus() |> ignore) // 布局首帧排队，非焦点竞态

    member _.FocusImport() =
        Dispatcher.UIThread.Post(fun () ->
            if importButton.IsEffectivelyVisible then
                importButton.Focus() |> ignore) // 布局首帧排队，非焦点竞态

    [<CLIEvent>]
    member _.CancelClicked = cancel.Publish

    [<CLIEvent>]
    member _.PickClicked = pick.Publish

    [<CLIEvent>]
    member _.ImportClicked = import.Publish
