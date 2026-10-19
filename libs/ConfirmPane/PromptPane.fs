namespace Wanxiang2.PromptPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout

/// 单行输入对话框的内容（迁自旧 Views/Dialogs.fs 的 prompt，重命名等）。
/// 空白时禁用确认并说明原因；回车、确认键、行级 Enter 都提交去空白的值；保存往返中 SetPending。
/// 字段错误行由组合者给（validationFor，与 FieldCheck 同款），错误文字经 SetError 写。
type PromptPane(look: PromptPaneLook, title: string, hint: string, initial: string, confirmLabel: string, validationFor: TextBox -> Control option) =
    let kit = Kit(look)
    let submitted = Event<string>()
    let cancelled = Event<unit>()
    let blankError = Event<string option>()
    let shell, box = kit.TextField hint
    let mutable pending = false
    let mutable canSubmit = false
    let helper =
        let t = kit.Caption "请输入非空内容"
        kit.Live(Kit.Pair(look.FieldInsetX, look.SpaceXs), fun (x, y) -> t.Margin <- Thickness(x, y, 0.0, 0.0))
        t.IsVisible <- false
        t
    let value () = (if isNull box.Text then "" else box.Text).Trim()
    let mutable submit: unit -> unit = ignore
    let footer = kit.DialogFooter(confirmLabel, Tone.Primary, (fun () -> cancelled.Trigger()), (fun () -> submit ()))

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
        footer.Confirm.SetEnabled canSubmit

    let view =
        box.Text <- initial
        AutomationProperties.SetName(box, hint)
        AutomationProperties.SetHelpText(footer.Confirm, sprintf "确认“%s”(Enter)" confirmLabel)
        ToolTip.SetTip(footer.Confirm, sprintf "确认“%s”(Enter)" confirmLabel)
        kit.Live(look.PendingMinWidth, fun v -> footer.Confirm.MinWidth <- v)
        kit.Live(look.SpaceMd, footer.Spacing)
        let field =
            StackPanel(Orientation = Orientation.Vertical)
        field.Children.Add shell
        validationFor box |> Option.iter field.Children.Add
        field.Children.Add helper
        kit.VStack(look.Space2xl, [ kit.Title title :> Control; field :> Control; footer.Row :> Control ])

    do
        submit <- fun () ->
            let v = value ()
            if not pending && canSubmit && not (String.IsNullOrWhiteSpace v) then submitted.Trigger v
        box.TextChanged.Add(fun _ -> update ())
        box.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                submit ())
        update ()

    member _.View = view :> Control
    member _.Box = box
    member _.CancelButton = footer.Cancel :> Control
    member _.ConfirmButton = footer.Confirm :> Control
    member _.InitialFocus = box :> Control
    member _.IsPending = pending

    /// 保存往返：确认键换「正在保存…」并禁用，取消也禁用
    member _.SetPending(value: bool) =
        pending <- value
        let text = if value then "正在保存…" else confirmLabel
        footer.Confirm.SetText text
        footer.Cancel.SetEnabled(not value)
        update ()

    /// 确认（去空白后的值）；空白或保存中不发
    [<CLIEvent>] member _.Submitted = submitted.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
    /// 全空白字符时的行内错误（None = 清除）；组合者写进 FieldCheck
    [<CLIEvent>] member _.FieldError = blankError.Publish
