namespace Wanxiang2.ConfirmPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media

/// 破坏性操作确认的内容（迁自旧 Views/Dialogs.fs 的 confirmWithFocus / confirm）。
/// 主键是 Danger 语气 + 警示提示；没有内容级 Enter/Space 快捷，必须落在确认键上激活。
/// 默认焦点由 dangerFirst 定（false 落取消，即安全项）。开关对话框与 Escape 归组合者。
type ConfirmPane(look: ConfirmPaneLook, title: string, body: string, confirmLabel: string, dangerFirst: bool) =
    let kit = Kit(look)
    let confirmed = Event<unit>()
    let cancelled = Event<unit>()
    let footer = kit.DialogFooter(confirmLabel, Tone.Danger, (fun () -> cancelled.Trigger()), (fun () -> confirmed.Trigger()))
    let message =
        let t = kit.Text(body, look.BodyFontSize, look.Muted)
        t.TextWrapping <- TextWrapping.Wrap
        kit.Live(look.BodyLineHeight, fun v -> t.LineHeight <- v)
        AutomationProperties.SetName(t, body)
        AutomationProperties.SetHelpText(t, body)
        t
    let view =
        kit.Live(look.SpaceMd, footer.Spacing)
        let tip = ToolTip.GetTip footer.Confirm :?> string
        AutomationProperties.SetHelpText(footer.Confirm, sprintf "%s · %s" tip body)
        kit.VStack(look.Space2xl, [ kit.Title title :> Control; message :> Control; footer.Row :> Control ])

    member _.View = view :> Control
    member _.CancelButton = footer.Cancel :> Control
    member _.ConfirmButton = footer.Confirm :> Control
    /// 打开后应聚焦的键
    member _.InitialFocus = (if dangerFirst then footer.Confirm else footer.Cancel) :> Control
    [<CLIEvent>] member _.Confirmed = confirmed.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
