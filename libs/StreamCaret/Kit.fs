namespace Wanxiang2.StreamCaret

open System
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading

/// 活值只在控件挂进可视树期间订阅，摘下即退订；构造时先按当前值应用一次。
module internal Follow =
    let now (source: IObservable<'T>) =
        let mutable v = Unchecked.defaultof<'T>
        (source.Subscribe(fun x -> v <- x)).Dispose()
        v

    let live (control: Control) (source: IObservable<'T>) (apply: 'T -> unit) =
        apply (now source)
        let mutable sub: IDisposable = null
        control.AttachedToVisualTree.Add(fun _ -> if isNull sub then sub <- source.Subscribe apply)
        control.DetachedFromVisualTree.Add(fun _ ->
            if not (isNull sub) then
                sub.Dispose()
                sub <- null)

    let live2 control (a: IObservable<'A>) (b: IObservable<'B>) (apply: 'A -> 'B -> unit) =
        let mutable va = now a
        let mutable vb = now b
        live control a (fun x -> va <- x; apply va vb)
        live control b (fun y -> vb <- y; apply va vb)

    let live3 control (a: IObservable<'A>) (b: IObservable<'B>) (c: IObservable<'C>) (apply: 'A -> 'B -> 'C -> unit) =
        let mutable va = now a
        let mutable vb = now b
        let mutable vc = now c
        live control a (fun x -> va <- x; apply va vb vc)
        live control b (fun y -> vb <- y; apply va vb vc)
        live control c (fun z -> vc <- z; apply va vb vc)

/// 可点的自绘 Border（旧 ActionBorder）：几何与外观仍是 Border，自动化层报按钮并给 Invoke。
type internal Spot() as this =
    inherit Border()
    let mutable action: unit -> unit = ignore
    do
        this.PointerReleased.Add(fun e ->
            if not e.Handled && this.IsEnabled && this.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if not e.Handled && this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())
    override _.StyleKeyOverride = typeof<Border>
    member _.OnInvoke(f: unit -> unit) = action <- f
    member _.Invoke() = if this.IsEnabled then action ()
    override _.OnCreateAutomationPeer() = SpotPeer(this) :> AutomationPeer

and internal SpotPeer(owner: Spot) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())
