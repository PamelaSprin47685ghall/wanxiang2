namespace Wanxiang2.TextEditPane

open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout

/// 对话框页脚：取消 + 主键（旧 Dialogs.actionRowWithDefault）。左右键在两键间移动；
/// 非破坏性页脚的行级 Enter/Space 在焦点不在取消上时即确认；Danger 不设行级确认。Escape 归覆盖层。
type internal Footer(kit: Kit, confirmLabel: string, tone: Tone, onCancel: unit -> unit, onConfirm: unit -> unit) =
    let cancel = kit.Button(Tone.Ghost, "取消", onCancel)
    let confirm = kit.Button(tone, confirmLabel, onConfirm)
    let row = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right)
    do
        AutomationProperties.SetHelpText(cancel, "取消并关闭对话框 (Esc)")
        ToolTip.SetTip(cancel, "取消并关闭对话框 (Esc)")
        let tip =
            match tone with
            | Tone.Danger -> sprintf "确认“%s”，此操作可能无法撤销" confirmLabel
            | _ -> confirmLabel
        AutomationProperties.SetHelpText(confirm, tip)
        ToolTip.SetTip(confirm, tip)
        cancel.KeyDown.Add(fun e ->
            if e.Key = Key.Right && confirm.IsEnabled then
                e.Handled <- true
                confirm.Focus NavigationMethod.Directional |> ignore)
        confirm.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancel.IsEnabled then
                e.Handled <- true
                cancel.Focus NavigationMethod.Directional |> ignore)
        row.Children.Add cancel
        row.Children.Add confirm
        row.KeyDown.Add(fun e ->
            if tone <> Tone.Danger && not e.Handled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                if cancel.IsFocused then onCancel () else onConfirm ())

    member _.Row = row
    member _.Cancel = cancel
    member _.Confirm = confirm
    member _.Spacing(v: float) = row.Spacing <- v
