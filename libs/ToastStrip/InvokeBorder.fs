namespace Wanxiang2.ToastStrip

open System
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Threading

/// 可激活的 Border（与旧 ActionBorder 同义）：读屏得到 Invoke 模式，角色取 ControlTypeOverride（缺省 Button）。
/// 样式仍按 Border 查（StyleKeyOverride），外观全靠主题类名；键盘与指针激活由调用方照旧接。
type InvokeBorder() as this =
    inherit Border()
    let mutable action: unit -> unit = ignore
    override _.StyleKeyOverride = typeof<Border>
    override _.OnCreateAutomationPeer() = InvokeBorderPeer(this) :> AutomationPeer
    /// 读屏 Invoke 时执行的动作（与 Enter / 点击同一个）
    member _.SetAction(a: unit -> unit) = action <- a
    member internal _.Invoke() = if this.IsEnabled then action ()

and InvokeBorderPeer(owner: InvokeBorder) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() =
        let o = AutomationProperties.GetControlTypeOverride owner
        if o.HasValue then o.Value else AutomationControlType.Button
    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())
