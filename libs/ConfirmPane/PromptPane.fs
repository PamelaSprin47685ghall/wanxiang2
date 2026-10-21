namespace Wanxiang2.PromptPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Wanxiang2.WorkspaceState

/// 单行输入对话框的内容（迁自旧 Views/Dialogs.fs 的 prompt，重命名等）。
/// 空白时禁用确认并说明原因；回车、确认键、行级 Enter 都提交去空白的值；保存往返中 ApplyPending。
/// 字段错误行由组合者给（validationFor，与 FieldCheck 同款），错误文字经 SetError 写。
type PromptPane
    (?title: string, ?hint: string, ?initial: string, ?confirmLabel: string, ?validationFor: TextBox -> Control option)
    =
    let submitted = Event<string>()
    let cancelled = Event<unit>()
    let blankError = Event<string option>()
    let box = TextBox(PlaceholderText = defaultArg hint "")
    do box.Classes.Add "control-stroke"
    let mutable pending = false
    let mutable canSubmit = false

    let helper =
        TextBlock(Text = "请输入非空内容", IsVisible = false, TextWrapping = Media.TextWrapping.Wrap)

    do helper.Classes.Add "muted"

    let value () =
        (if isNull box.Text then "" else box.Text).Trim()

    let mutable submit: unit -> unit = ignore
    let mutable currentLabel = defaultArg confirmLabel "确定"
    let cancelButton = Button(Content = "取消")

    do
        cancelButton.Classes.Add "ghost"
        cancelButton.Classes.Add "button-ghost"
        AutomationProperties.SetName(cancelButton, "取消")
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

    let update () =
        let raw = if isNull box.Text then "" else box.Text
        let empty = String.IsNullOrWhiteSpace raw

        if empty then
            if raw.Length > 0 then
                blankError.Trigger(Some "输入内容不能全为空白字符。")
                helper.IsVisible <- false
            else
                blankError.Trigger None
                helper.Text <- "内容不能为空，请输入有效文本。"
                helper.IsVisible <- true
        else
            blankError.Trigger None
            helper.IsVisible <- false

        canSubmit <- not pending && not empty
        confirmButton.IsEnabled <- canSubmit

    let view =
        box.Text <- defaultArg initial ""
        AutomationProperties.SetName(box, defaultArg hint "")
        let tip = sprintf "确认“%s”(Enter)" currentLabel
        AutomationProperties.SetName(confirmButton, currentLabel)
        AutomationProperties.SetHelpText(confirmButton, tip)
        ToolTip.SetTip(confirmButton, tip)
        let field = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        field.Children.Add box

        match validationFor with
        | Some vf -> vf box |> Option.iter field.Children.Add
        | None -> ()

        field.Children.Add helper
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        panel.Children.Add titleBlock
        panel.Children.Add field
        panel.Children.Add footerRow
        panel

    do
        submit <-
            fun () ->
                let v = value ()

                if not pending && canSubmit && not (String.IsNullOrWhiteSpace v) then
                    submitted.Trigger v

        box.TextChanged.Add(fun _ -> update ())

        box.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                submit ())

        update ()

    member _.View = view :> Control
    member _.Box = box
    member _.CancelButton = cancelButton :> Control
    member _.ConfirmButton = confirmButton :> Control
    member _.InitialFocus = box :> Control
    member _.IsPending = pending

    /// 保存往返：确认键换「正在保存…」并禁用，取消也禁用
    member _.ApplyPending(value: bool) =
        pending <- value
        let text = if value then "正在保存…" else currentLabel
        confirmButton.Content <- text
        AutomationProperties.SetName(confirmButton, text)
        cancelButton.IsEnabled <- not value
        update ()

    member this.Render(vs: DialogVS) =
        titleBlock.Text <- vs.Title

        match vs.Prompt with
        | Some p ->
            currentLabel <- p.ConfirmLabel
            AutomationProperties.SetName(confirmButton, currentLabel)

            if not (isNull p.Hint) then
                box.PlaceholderText <- p.Hint

            if box.Text <> p.InitialText && not (String.IsNullOrEmpty p.InitialText) then
                box.Text <- p.InitialText

            match p.ErrorText with
            | Some err ->
                helper.Text <- err
                helper.IsVisible <- true
            | None ->
                match p.HelperText with
                | Some h ->
                    helper.Text <- h
                    helper.IsVisible <- true
                | None -> helper.IsVisible <- false

            this.ApplyPending p.IsPending
        | None -> ()

    /// 确认（去空白后的值）；空白或保存中不发
    [<CLIEvent>]
    member _.Submitted = submitted.Publish

    [<CLIEvent>]
    member _.Cancelled = cancelled.Publish

    /// 全空白字符时的行内错误（None = 清除）；组合者写进 FieldCheck
    [<CLIEvent>]
    member _.FieldError = blankError.Publish
