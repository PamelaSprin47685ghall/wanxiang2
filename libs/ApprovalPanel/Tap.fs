namespace Wanxiang2.ApprovalPanel

open System
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Threading

/// 可点的 Border（与旧 ActionBorder 同义）：读屏得到 Button 与 Invoke；Enter / Space / 左键松开都触发。
/// 样式按 Border 查（StyleKeyOverride），外观全靠主题类名。
type internal Tap(text: string, classes: string list, action: unit -> unit) as this =
    inherit Border()

    do
        this.Child <- TextBlock(Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        this.Focusable <- true
        this.VerticalAlignment <- VerticalAlignment.Center
        this.Cursor <- new Cursor(StandardCursorType.Hand)
        for c in classes do this.Classes.Add c
        AutomationProperties.SetName(this, text)
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

    member internal _.Invoke() = if this.IsEnabled then action ()

    /// 换文字（读屏名同步）
    member _.SetText(value: string) =
        (this.Child :?> TextBlock).Text <- value
        AutomationProperties.SetName(this, value)

    /// 与旧 Ui.setEnabled 同义：不可用时也不可聚焦；透明度由主题的 :disabled 给
    member _.SetEnabled(enabled: bool) =
        this.IsEnabled <- enabled
        this.Focusable <- enabled

and internal TapPeer(owner: Tap) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())

/// 按钮档对应的主题类（旧 Ui.button 的四档）
module internal TapTone =
    let primary = [ "surface"; "button-primary"; "hover-accent" ]
    let secondary = [ "surface"; "button-secondary"; "hover-raised" ]
    let ghost = [ "surface"; "button-ghost"; "hover-plain" ]
