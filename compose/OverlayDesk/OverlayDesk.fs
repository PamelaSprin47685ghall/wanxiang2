namespace Wanxiang2.OverlayDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.OverlayLayer
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.FocusOrigin

/// 组合后的外观：各控件自己的 Look 加上只有组合层才用的值。
type OverlayDeskLook =
    { Layer: OverlayLayerLook
      Toasts: ToastStripLook
      Menu: PopupMenuLook
      /// 普通菜单浮层的最小宽度（旧代码 200）
      MenuMinWidth: IObservable<float>
      /// 分组菜单浮层的最小宽度（旧代码 240）
      GroupedMenuMinWidth: IObservable<float>
      /// 停留时长（毫秒）：失败、警告、其他（旧 MotionLedger 9000 / 6500 / 4000）
      DwellFailureMs: IObservable<float>
      DwellWarningMs: IObservable<float>
      DwellDefaultMs: IObservable<float>
      /// 停留计时器的步长
      DismissTick: TimeSpan }

/// 旧 OverlayHost + Menu 的组合：Escape 次序（浮层 → 对话框 → 最新提示条）、提示条去重与上限、
/// 停留计时、菜单激活先关浮层再执行、Ctrl/⌘+Z 与 F6 的提示条动作入口都在这里协作。
type OverlayDesk(root: Grid, look: OverlayDeskLook) =
    let layer = OverlayLayer(root, look.Layer, PopupPlacement.place)
    let strip = ToastStrip(look.Toasts)
    let queue = ToastQueue()
    let timers = Collections.Generic.Dictionary<string, DispatcherTimer>()
    let actions = ResizeArray<string * string * (unit -> unit)>()
    let mutable inset = 0.0
    let mutable menuMin = 0.0
    let mutable groupedMin = 0.0
    let mutable dwellFailure = 0.0
    let mutable dwellWarning = 0.0
    let mutable dwellDefault = 0.0

    let fit () = strip.Fit(root.Bounds.Width, root.Bounds.Height, inset)

    let remove (message: string) =
        match timers.TryGetValue message with
        | true, t ->
            t.Stop()
            timers.Remove message |> ignore
        | _ -> ()
        queue.Remove message
        actions.RemoveAll(fun (m, _, _) -> m = message) |> ignore
        strip.Remove message

    do
        // 提示条层排在覆盖层四层之后，与旧次序一致。
        Grid.SetColumnSpan(strip.View, 8)
        Grid.SetRowSpan(strip.View, 8)
        root.Children.Add strip.View
        look.Layer.ViewportInset.Subscribe(fun v -> inset <- v; fit ()) |> ignore
        look.MenuMinWidth.Subscribe(fun v -> menuMin <- v) |> ignore
        look.GroupedMenuMinWidth.Subscribe(fun v -> groupedMin <- v) |> ignore
        look.DwellFailureMs.Subscribe(fun v -> dwellFailure <- v) |> ignore
        look.DwellWarningMs.Subscribe(fun v -> dwellWarning <- v) |> ignore
        look.DwellDefaultMs.Subscribe(fun v -> dwellDefault <- v) |> ignore
        strip.Clicked.Add remove
        strip.ActionInvoked.Add(fun message ->
            match actions |> Seq.tryFind (fun (m, _, _) -> m = message) with
            | Some(_, _, run) -> remove message; run ()
            | None -> remove message)

    member _.Layer = layer
    member _.Toasts = strip

    member _.ShowDialog(content: Control, width: float, ?onClosed: unit -> unit, ?canDismiss: unit -> bool, ?initialFocus: Control) =
        layer.ShowDialog(content, width, ?onClosed = onClosed, ?canDismiss = canDismiss, ?initialFocus = initialFocus)
    member _.CloseDialog() = layer.CloseDialog()
    member _.ClosePopup() = layer.ClosePopup()
    member _.TrapTab(content, e) = layer.TrapTab(content, e)

    member this.WireDismiss() =
        layer.WireDismiss()
        root.PropertyChanged.Add(fun args -> if args.Property = Visual.BoundsProperty then fit ())

    member this.Toast(message: string, tone: ToastTone) = this.ToastWith(message, tone, None)

    /// 带一个行内动作的提示条；点动作先关条再执行动作。
    member _.ToastWith(message: string, tone: ToastTone, action: (string * (unit -> unit)) option) =
        if not (String.IsNullOrWhiteSpace message) then
            // 同文只留最新一条、新条挤掉最旧的：被撤下的由 ToastQueue 给出。
            for dismissed in queue.Admit message do
                match timers.TryGetValue dismissed with
                | true, t -> t.Stop(); timers.Remove dismissed |> ignore
                | _ -> ()
                actions.RemoveAll(fun (m, _, _) -> m = dismissed) |> ignore
                strip.Remove dismissed
            let dwell =
                ToastDwell(
                    (match tone with
                     | ToastTone.Failure -> dwellFailure
                     | ToastTone.Warning -> dwellWarning
                     | _ -> dwellDefault),
                    action.IsSome)
            action |> Option.iter (fun (label, run) -> actions.Add(message, label, run))
            strip.Add(message, tone, action |> Option.map fst)
            fit ()
            let expire = DispatcherTimer(Interval = look.DismissTick)
            timers[message] <- expire
            expire.Tick.Add(fun _ -> if dwell.Tick expire.Interval.TotalMilliseconds then remove message)
            expire.Start()
            // 暂停规则在 ToastDwell：只把本条的指针与焦点事件转给它。
            let mine f = fun m -> if m = message && timers.ContainsKey message && obj.ReferenceEquals(timers[message], expire) then f ()
            strip.PointerEnteredToast.Add(mine dwell.PointerEntered)
            strip.PointerExitedToast.Add(fun (m, focused) -> mine (fun () -> dwell.PointerExited focused) m)
            strip.ActionFocused.Add(mine dwell.ActionFocused)
            strip.ActionUnfocused.Add(mine dwell.ActionUnfocused)

    /// 触发最新一条仍可见、动作名为 label 的提示条动作；没有则 false。
    member _.TryInvokeToastAction(label: string) : bool =
        match actions |> Seq.filter (fun (_, l, _) -> l = label) |> Seq.tryLast with
        | Some(message, _, run) -> remove message; run (); true
        | None -> false

    member _.HasToastAction(label: string) = actions |> Seq.exists (fun (_, l, _) -> l = label)
    member _.FocusToastAction() = strip.FocusAction()
    member _.SetToastFocusHome(home: unit -> unit) = strip.SetFocusHome home
    member _.ToastActionButtons = strip.ActionButtons

    /// 唯一的 Escape 归属（topmost-first：浮层 → 对话框 → 最新提示条）。
    member _.HandleEscape() : bool =
        if layer.HandleEscape() then true
        else
            match List.tryLast queue.Showing with
            | Some newest -> remove newest; true
            | None -> false

    member private _.Activate(entry: PopupEntry) =
        layer.ClosePopup()
        entry.Action()

    member private this.Mark(content: Control) =
        // 菜单项挂上焦点来源标记（旧 Interaction.reportFocusOrigin）。
        for c in content.GetLogicalDescendants() do
            match c with
            | :? Border as b when b.Classes.Contains PopupMenu.itemClass -> FocusOrigin.report b
            | _ -> ()

    /// 打开一个菜单；条目为空时不打开。
    member this.ShowMenu(anchor: Control, alignRight: bool, entries: PopupEntry list) =
        match PopupMenu.build look.Menu this.Activate entries with
        | Some content -> this.Mark content; layer.ShowPopup(anchor, content, alignRight, menuMin)
        | None -> ()

    member this.ShowGroupedMenu(anchor: Control, alignRight: bool, groups: (string * PopupEntry list) list) =
        match PopupMenu.buildGrouped look.Menu this.Activate groups with
        | Some content -> this.Mark content; layer.ShowPopup(anchor, content, alignRight, groupedMin)
        | None -> ()

    /// 下拉选择器：点击或 Down/Up 弹出分组候选。
    member this.SelectButton(initialText: string, optionsOf: unit -> (string * PopupEntry list) list) =
        let b = Wanxiang2.PopupMenu.SelectButton(look.Menu, initialText)
        b.OpenRequested.Add(fun () -> this.ShowGroupedMenu(b.View, false, optionsOf ()))
        b
