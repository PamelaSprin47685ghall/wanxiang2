namespace Wanxiang2.ImportSheet

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Avalonia.Input
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider

/// 可点的 Border（与旧 ActionBorder 同义）：读屏得到 Button 与 Invoke；Enter / Space / 左键松开都触发。
type internal Tap(child: Control, name: string, classes: string list, action: unit -> unit) as this =
    inherit Border()

    do
        this.Child <- child
        this.Focusable <- true
        this.VerticalAlignment <- VerticalAlignment.Center
        this.Cursor <- new Cursor(StandardCursorType.Hand)
        for c in classes do this.Classes.Add c
        if not (String.IsNullOrEmpty name) then AutomationProperties.SetName(this, name)
        this.PointerReleased.Add(fun e ->
            if this.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())

    override _.StyleKeyOverride = typeof<Border>
    override _.OnCreateAutomationPeer() = TapPeer(this) :> AutomationPeer

    member _.Invoke() = if this.IsEnabled then action ()

    member _.SetText(value: string) =
        match this.Child with
        | :? TextBlock as t -> t.Text <- value
        | _ -> ()
        AutomationProperties.SetName(this, value)

    member _.SetEnabled(enabled: bool) =
        this.IsEnabled <- enabled
        this.Focusable <- enabled

    static member TextButton(text: string, classes: string list, action: unit -> unit) : Tap =
        let t = TextBlock(Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        Tap(t, text, classes, action)

and internal TapPeer(owner: Tap) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        // 跨线程 marshal，非焦点竞态（UI Automation 调用方可能来自辅助功能后台线程）
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())

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

/// 外观。全部由主题管理者提供活的实例。
type ImportSheetAppearance =
    { Text: IBrush
      Muted: IBrush
      Danger: IBrush
      TitleFontSize: IObservable<float>
      BodyFontSize: IObservable<float>
      SmallFontSize: IObservable<float>
      CaptionFontSize: IObservable<float>
      CaptionLineHeight: IObservable<float>
      /// 段间距（旧 spaceXl）
      StackSpacing: IObservable<float>
      /// 按钮间距（旧 spaceMd）
      ButtonSpacing: IObservable<float> }

/// 导入对话框的内容（迁自旧 Views/ImportDialog.fs 的界面部分）：标题、说明、状态、预览详情、隐私说明、三键。
/// 解析、读文件、提交与超时由组合者接（ImportView、ImportAttempt、ConversationTransfer）；本控件只呈现并发事件。
type ImportSheet(look: ImportSheetAppearance, privacyNote: string) =
    let live (o: IObservable<'T>) f = o.Subscribe(fun v -> f v) |> ignore
    let cancel = Event<unit>()
    let pick = Event<unit>()
    let import = Event<unit>()

    let heading = TextBlock(Text = "导入对话", FontWeight = FontWeight.Medium, Foreground = look.Text)
    let caption (text: string) =
        let t = TextBlock(Text = text, Foreground = look.Muted, TextWrapping = TextWrapping.Wrap)
        live look.CaptionFontSize (fun v -> t.FontSize <- v)
        live look.CaptionLineHeight (fun v -> t.LineHeight <- v)
        t
    let intro = caption "选择从万象「导出（Markdown / JSON）」里保存的 JSON 文件。导入后是这台主机上的一个新对话，不会自动生成回复。"
    let status = TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Muted, Name = "import-status")
    let details = TextBlock(TextWrapping = TextWrapping.Wrap, Foreground = look.Muted, IsVisible = false, Name = "import-details")
    let privacy = caption privacyNote
    let closeButton = Tap.TextButton("取消", TapTone.ghost, cancel.Trigger)
    let pickButton = Tap.TextButton("选择 JSON 文件…", TapTone.secondary, pick.Trigger)
    let importButton = Tap.TextButton("导入为新对话", TapTone.primary, import.Trigger)
    let content = StackPanel(Orientation = Orientation.Vertical)

    do
        live look.TitleFontSize (fun v -> heading.FontSize <- v)
        live look.BodyFontSize (fun v -> status.FontSize <- v)
        live look.SmallFontSize (fun v -> details.FontSize <- v)
        privacy.Name <- "import-privacy-note"
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        AutomationProperties.SetName(pickButton, "选择要导入的 JSON 文件")
        AutomationProperties.SetName(importButton, "导入为新对话")
        AutomationProperties.SetHelpText(importButton, "在这台主机上新建一个对话并写入文件里的消息")
        importButton.IsVisible <- false
        let buttons = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center)
        live look.ButtonSpacing (fun v -> buttons.Spacing <- v)
        for b in [ closeButton; pickButton; importButton ] do buttons.Children.Add b
        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        live look.StackSpacing (fun v -> content.Spacing <- v)
        for c in [ heading :> Control; intro; status; details; divider; privacy; buttons ] do content.Children.Add c

    /// 对话框内容（组合者交给 OverlayLayer.ShowDialog，旧宽 480）
    member _.View: Control = content
    member _.StatusText = status.Text
    member _.DetailsText = details.Text
    member _.ImportButton: Control = importButton
    member _.PickButton: Control = pickButton
    member _.CloseButton: Control = closeButton

    /// 按键门控（组合者从 ImportView.chrome 得出）
    member _.SetChrome(importVisible: bool, importEnabled: bool, pickEnabled: bool, pickText: string) =
        importButton.IsVisible <- importVisible
        importButton.SetEnabled importEnabled
        pickButton.SetEnabled pickEnabled
        // 与旧 Ui.setButtonText 相同：读屏名随按钮文字
        pickButton.SetText pickText

    member _.SetStatus(text: string, tone: ImportTone) =
        status.Text <- text
        status.Foreground <-
            match tone with
            | ImportTone.Muted -> look.Muted
            | ImportTone.Text -> look.Text
            | ImportTone.Danger -> look.Danger

    /// 预览详情：Some 行 → 显示；None → 隐藏
    member _.SetDetails(lines: string list option) =
        match lines with
        | Some ls ->
            details.Text <- String.Join("\n", ls)
            details.IsVisible <- true
        | None -> details.IsVisible <- false

    /// 打开后与预览就绪后的初始焦点（投递到下一轮，按钮可见才聚焦）
    member _.FocusPick() = Dispatcher.UIThread.Post(fun () -> if pickButton.IsEffectivelyVisible then pickButton.Focus() |> ignore) // 布局首帧排队，非焦点竞态
    member _.FocusImport() = Dispatcher.UIThread.Post(fun () -> if importButton.IsEffectivelyVisible then importButton.Focus() |> ignore) // 布局首帧排队，非焦点竞态

    [<CLIEvent>] member _.CancelClicked = cancel.Publish
    [<CLIEvent>] member _.PickClicked = pick.Publish
    [<CLIEvent>] member _.ImportClicked = import.Publish
