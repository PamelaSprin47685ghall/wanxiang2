namespace Wanxiang2.ConfirmPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 破坏性操作确认的内容（迁自旧 Views/Dialogs.fs 的 confirmWithFocus / confirm）。
/// 主键是 Danger 语气 + 警示提示；没有内容级 Enter/Space 快捷，必须落在确认键上激活。
/// 默认焦点由 dangerFirst 定（false 落取消，即安全项）。开关对话框与 Escape 归组合者。
type ConfirmPane(?title: string, ?body: string, ?confirmLabel: string, ?dangerFirst: bool) =
    let confirmed = Event<unit>()
    let cancelled = Event<unit>()
    let mutable dangerFirstVal = defaultArg dangerFirst false

    let titleBlock = TextBlock(FontWeight = FontWeight.Medium)
    do titleBlock.Classes.Add "dialog-title"

    let messageBlock = TextBlock(TextWrapping = TextWrapping.Wrap)
    do
        messageBlock.Classes.Add "dialog-message"
        messageBlock.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
        messageBlock.Bind(TextBlock.LineHeightProperty, Dyn.res "Metric.UiBodyLineHeight") |> ignore

    let cancelButton = Button(Content = "取消")
    do
        cancelButton.Classes.Add "ghost"
        cancelButton.Classes.Add "button-ghost"
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭对话框 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭对话框 (Esc)")
        cancelButton.Click.Add(fun _ -> cancelled.Trigger())

    let confirmButton = Button()
    do
        AutomationProperties.SetName(confirmButton, defaultArg confirmLabel "确认")
        confirmButton.Classes.Add "danger"
        confirmButton.Classes.Add "button-danger"
        confirmButton.Click.Add(fun _ -> confirmed.Trigger())

    let footerRow = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8.0)
    do
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Avalonia.Input.Key.Right && confirmButton.IsEnabled then
                e.Handled <- true
                confirmButton.Focus Avalonia.Input.NavigationMethod.Directional |> ignore)
        confirmButton.KeyDown.Add(fun e ->
            if e.Key = Avalonia.Input.Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus Avalonia.Input.NavigationMethod.Directional |> ignore)
        footerRow.Children.Add cancelButton
        footerRow.Children.Add confirmButton

    let view =
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        panel.Children.Add titleBlock
        panel.Children.Add messageBlock
        panel.Children.Add footerRow
        panel

    let apply (t: string) (b: string) (label: string) (danger: bool) (isPending: bool) =
        dangerFirstVal <- danger
        titleBlock.Text <- t
        messageBlock.Text <- b
        AutomationProperties.SetName(messageBlock, b)
        AutomationProperties.SetHelpText(messageBlock, b)
        confirmButton.Content <- label
        AutomationProperties.SetName(confirmButton, label)
        let tip = sprintf "确认“%s”，此操作可能无法撤销" label
        AutomationProperties.SetHelpText(confirmButton, sprintf "%s · %s" tip b)
        ToolTip.SetTip(confirmButton, tip)
        confirmButton.IsEnabled <- not isPending
        cancelButton.IsEnabled <- not isPending

    do
        let initTitle = defaultArg title ""
        let initBody = defaultArg body ""
        let initLabel = defaultArg confirmLabel "确认"
        apply initTitle initBody initLabel dangerFirstVal false

    member _.View = view :> Control
    member _.CancelButton = cancelButton :> Control
    member _.ConfirmButton = confirmButton :> Control
    member _.InitialFocus = (if dangerFirstVal then confirmButton else cancelButton) :> Control
    [<CLIEvent>] member _.Confirmed = confirmed.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish

    member this.Render(vs: DialogVS) =
        match vs.Confirm with
        | Some c -> apply vs.Title c.Body c.ConfirmLabel c.DangerFirst c.IsPending
        | None -> ()
