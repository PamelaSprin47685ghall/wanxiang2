namespace Wanxiang2.TextEditPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Wanxiang2.WorkspaceState

/// 长文本编辑的内容（迁自旧 Views/Dialogs.fs 的 editText：编辑消息并分叉）。
/// Ctrl/⌘ + Enter 或确认键提交原文；打开后光标落在末尾。
type TextEditPane(?title: string, ?initial: string, ?confirmLabel: string) =
    let submitted = Event<string>()
    let cancelled = Event<unit>()
    let box = TextBox(AcceptsReturn = true, TextWrapping = Media.TextWrapping.Wrap)

    do
        box.Classes.Add "control-stroke"
        box.Classes.Add "text-area"
        box.MinHeight <- 160.0

    let submit () =
        submitted.Trigger(if isNull box.Text then "" else box.Text)

    let mutable currentLabel = defaultArg confirmLabel "分叉"

    let cancelButton = Button(Content = "取消")

    do
        cancelButton.Classes.Add "ghost"
        cancelButton.Classes.Add "button-ghost"
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭对话框 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭对话框 (Esc)")
        cancelButton.Click.Add(fun _ -> cancelled.Trigger())

    let confirmButton = Button(Content = currentLabel)

    do
        AutomationProperties.SetName(confirmButton, currentLabel)
        confirmButton.Classes.Add "primary"
        confirmButton.Classes.Add "button-primary"
        confirmButton.Click.Add(fun _ -> submit ())

    let footerRow =
        StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8.0)

    do
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Key.Right && confirmButton.IsEnabled then
                e.Handled <- true
                confirmButton.Focus NavigationMethod.Directional |> ignore)

        confirmButton.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus NavigationMethod.Directional |> ignore)

        footerRow.Children.Add cancelButton
        footerRow.Children.Add confirmButton

    let titleBlock =
        TextBlock(Text = defaultArg title "", FontWeight = Media.FontWeight.Medium)

    let captionBlock =
        TextBlock(Text = "会以你编辑后的内容新建一个分叉对话，原对话保持不变。", TextWrapping = Media.TextWrapping.Wrap)

    do captionBlock.Classes.Add "muted"

    let view =
        box.Text <- defaultArg initial ""
        AutomationProperties.SetName(box, "消息内容")
        let tip = sprintf "确认“%s”（Ctrl / ⌘ + Enter）" currentLabel
        AutomationProperties.SetHelpText(confirmButton, tip)
        ToolTip.SetTip(confirmButton, tip)
        // 隧道阶段接 Ctrl/⌘ + Enter：多行框自己会先把它当换行吃掉（旧实现冒泡接，提交的文字多一个换行）
        box.AddHandler(
            InputElement.KeyDownEvent,
            (fun _ (e: KeyEventArgs) ->
                let ctrl =
                    e.KeyModifiers.HasFlag KeyModifiers.Control
                    || e.KeyModifiers.HasFlag KeyModifiers.Meta

                if e.Key = Key.Enter && ctrl then
                    e.Handled <- true
                    submit ()),
            Avalonia.Interactivity.RoutingStrategies.Tunnel
        )

        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        panel.Children.Add titleBlock
        panel.Children.Add captionBlock
        panel.Children.Add box
        panel.Children.Add footerRow
        panel

    member _.View = view :> Control
    member _.Box = box
    member _.CancelButton = cancelButton :> Control
    member _.ConfirmButton = confirmButton :> Control

    /// 聚焦并把光标放到末尾（打开后由组合者 Post 调用）
    member _.FocusEnd() =
        box.Focus() |> ignore
        box.CaretIndex <- (if isNull box.Text then 0 else box.Text.Length)

    member this.Render(vs: DialogVS) =
        titleBlock.Text <- vs.Title

        match vs.TextEdit with
        | Some te ->
            currentLabel <- te.ConfirmLabel
            AutomationProperties.SetName(confirmButton, currentLabel)
            confirmButton.Content <- currentLabel
            let tip = sprintf "确认“%s”（Ctrl / ⌘ + Enter）" currentLabel
            AutomationProperties.SetHelpText(confirmButton, tip)
            ToolTip.SetTip(confirmButton, tip)

            if box.Text <> te.Initial && not (String.IsNullOrEmpty te.Initial) then
                box.Text <- te.Initial

            confirmButton.IsEnabled <- not te.IsPending
            cancelButton.IsEnabled <- not te.IsPending
        | None -> ()

    [<CLIEvent>]
    member _.Submitted = submitted.Publish

    [<CLIEvent>]
    member _.Cancelled = cancelled.Publish
