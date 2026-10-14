namespace Wanxiang2.InputSense

open System
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity

/// 最近一次输入来自触屏还是键盘/鼠标（迁自旧 Controls/Interaction.fs 的 InputModality，第 212 节）。
/// 旧实现是模块级单例；这里是对象，宿主持有一个实例。
/// browserDefault：还没有任何输入时是否按触屏对待（浏览器里多为手机，传 OperatingSystem.IsBrowser()）。
type InputSense(browserDefault: bool) =
    let mutable lastWasTouch: bool option = None
    let changed = Event<bool>()

    new() = InputSense(OperatingSystem.IsBrowser())

    /// 输入方式翻转时通知（决策 276）。
    [<CLIEvent>]
    member _.Changed = changed.Publish

    /// 报告一次真实输入：触屏按下为 true，键盘 / 鼠标为 false。
    member _.Report(isTouch: bool) =
        if lastWasTouch <> Some isTouch then
            lastWasTouch <- Some isTouch
            changed.Trigger isTouch

    member _.Reset() = lastWasTouch <- None

    member _.PrefersTouch =
        match lastWasTouch with
        | Some v -> v
        | None -> browserDefault

    /// 在根控件上挂隧道监听，记录输入方式（含已处理事件）。
    member this.Track(root: Control) =
        root.AddHandler(
            InputElement.PointerPressedEvent,
            (fun _ (e: PointerPressedEventArgs) -> this.Report(e.Pointer.Type = PointerType.Touch)),
            RoutingStrategies.Tunnel,
            true)
        root.AddHandler(InputElement.KeyDownEvent, (fun _ (_: KeyEventArgs) -> this.Report false), RoutingStrategies.Tunnel, true)

    /// 触屏上不显示该控件的悬停提示（无障碍名称不受影响）。
    static member SuppressTooltipOnTouch(control: Control) =
        let sync (pointerType: PointerType) =
            let touch = pointerType = PointerType.Touch
            ToolTip.SetServiceEnabled(control, not touch)
            if touch then ToolTip.SetIsOpen(control, false)
        control.AddHandler(
            InputElement.PointerEnteredEvent,
            (fun _ (e: PointerEventArgs) -> sync e.Pointer.Type),
            RoutingStrategies.Direct ||| RoutingStrategies.Bubble,
            true)
        control.AddHandler(
            InputElement.PointerPressedEvent,
            (fun _ (e: PointerPressedEventArgs) -> sync e.Pointer.Type),
            RoutingStrategies.Tunnel ||| RoutingStrategies.Bubble,
            true)
