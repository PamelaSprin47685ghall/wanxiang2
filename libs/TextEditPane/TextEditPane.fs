namespace Wanxiang2.TextEditPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input

/// 长文本编辑的内容（迁自旧 Views/Dialogs.fs 的 editText：编辑消息并分叉）。
/// Ctrl/⌘ + Enter 或确认键提交原文；打开后光标落在末尾。
type TextEditPane(look: TextEditPaneLook, title: string, initial: string, confirmLabel: string) =
    let kit = Kit(look)
    let submitted = Event<string>()
    let cancelled = Event<unit>()
    let shell, box = kit.TextArea("消息内容", look.TextAreaLongMinHeight)
    let submit () = submitted.Trigger(if isNull box.Text then "" else box.Text)
    let footer = Footer(kit, confirmLabel, Tone.Primary, (fun () -> cancelled.Trigger()), submit)
    let view =
        box.Text <- initial
        AutomationProperties.SetName(box, "消息内容")
        let tip = sprintf "确认“%s”（Ctrl / ⌘ + Enter）" confirmLabel
        AutomationProperties.SetHelpText(footer.Confirm, tip)
        ToolTip.SetTip(footer.Confirm, tip)
        kit.Live(look.SpaceMd, footer.Spacing)
        // 隧道阶段接 Ctrl/⌘ + Enter：多行框自己会先把它当换行吃掉（旧实现冒泡接，提交的文字多一个换行）
        box.AddHandler(
            InputElement.KeyDownEvent,
            (fun _ (e: KeyEventArgs) ->
                let ctrl = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
                if e.Key = Key.Enter && ctrl then
                    e.Handled <- true
                    submit ()),
            Avalonia.Interactivity.RoutingStrategies.Tunnel)
        kit.VStack(
            look.Space2xl,
            [ kit.Title title :> Control
              kit.Caption "会以你编辑后的内容新建一个分叉对话，原对话保持不变。" :> Control
              shell :> Control
              footer.Row :> Control ])

    member _.View = view :> Control
    member _.Box = box
    member _.CancelButton = footer.Cancel :> Control
    member _.ConfirmButton = footer.Confirm :> Control

    /// 聚焦并把光标放到末尾（打开后由组合者 Post 调用）
    member _.FocusEnd() =
        box.Focus() |> ignore
        box.CaretIndex <- (if isNull box.Text then 0 else box.Text.Length)

    [<CLIEvent>] member _.Submitted = submitted.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
