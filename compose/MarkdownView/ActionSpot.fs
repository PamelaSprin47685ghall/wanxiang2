namespace Wanxiang2.MarkdownView

open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading

/// 可点的自绘 Border（旧 Primitives.ActionBorder）：几何与外观仍是 Border，自动化层报按钮并给 Invoke。
/// StyleKeyOverride = Border 是必需的：否则 OfType<Border>() 的共享样式选不中本类型。
type ActionSpot() as this =
    inherit Border()

    let mutable action: unit -> unit = ignore

    do
        this.PointerReleased.Add(fun e ->
            if this.IsEnabled && this.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())

    override _.StyleKeyOverride = typeof<Border>

    /// 点击、Enter/Space、读屏 Invoke 都走这一个动作
    member _.OnInvoke(f: unit -> unit) = action <- f

    member _.Invoke() = if this.IsEnabled then action ()

    override _.OnCreateAutomationPeer() = ActionSpotPeer(this) :> AutomationPeer

and internal ActionSpotPeer(owner: ActionSpot) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        // 跨线程 marshal，非焦点竞态（UI Automation 调用方可能来自辅助功能后台线程）
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())
