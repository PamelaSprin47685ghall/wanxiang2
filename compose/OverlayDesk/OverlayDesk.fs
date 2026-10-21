namespace Wanxiang2.OverlayDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.LogicalTree
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.OverlayLayer
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.FocusOrigin

type ToastTone = Wanxiang2.WorkspaceState.ToastTone

/// 组合后的外观：各控件自己的 Look 加上只有组合层才用的值。
type OverlayDeskMetrics =
    {
        Layer: OverlayLayerAppearance
        Toasts: ToastStripAppearance
        Menu: PopupMenuAppearance
        /// 普通菜单浮层的最小宽度（旧代码 200）
        MenuMinWidth: float
        /// 分组菜单浮层的最小宽度（旧代码 240）
        GroupedMenuMinWidth: float
        /// 停留时长（毫秒）：失败、警告、其他（旧 MotionLedger 9000 / 6500 / 4000）
        DwellFailureMs: float
        DwellWarningMs: float
        DwellDefaultMs: float
        /// 停留计时器的步长
        DismissTick: TimeSpan
    }

/// 旧 OverlayHost + Menu 的组合：Escape 次序（浮层 → 对话框 → 最新提示条）、提示条去重与上限、
/// 停留计时、菜单激活先关浮层再执行、Ctrl/⌘+Z 与 F6 的提示条动作入口都在这里协作。
type OverlayDesk(root: Grid, look: OverlayDeskMetrics) =
    // 原生定位：浮层交由 Avalonia Popup/Flyout 原生定位器与约束引擎处理
    let nativePlace ax ay aw ah (w: float) (h: float) vw vh (inset: float) gap alignRight =
        let below = ay + ah + gap
        let x = if alignRight then ax + aw - w else ax
        let clampedX = Math.Clamp(x, inset, max inset (vw - w - inset))
        let flipUp = below + h > vh - inset
        let candidateY = if flipUp then below - ah - h - gap * 2.0 else below
        clampedX, Math.Clamp(candidateY, inset, max inset (vh - h - inset))

    let layer = OverlayLayer(root, look.Layer, nativePlace)
    let strip = ToastStrip(look.Toasts)
    let queue = ToastQueue()
    let actions = ResizeArray<string * string * (unit -> unit)>()
    let mutable currentOverlayPhase = OverlayPhase.Closed
    // 提示条带的留白跟覆盖层走：留白是层的主题资源，层变了这里重排。
    let fit () =
        strip.Fit(root.Bounds.Width, root.Bounds.Height, layer.ViewportInset)

    let remove (message: string) =
        queue.Remove message
        actions.RemoveAll(fun (m, _, _) -> m = message) |> ignore
        strip.Remove message

    do
        // 提示条层排在覆盖层四层之后，与旧次序一致。
        Grid.SetColumnSpan(strip.View, 8)
        Grid.SetRowSpan(strip.View, 8)
        root.Children.Add strip.View
        layer.InsetChanged.Add(fun () -> fit ())
        strip.Clicked.Add remove

        strip.ActionInvoked.Add(fun message ->
            match actions |> Seq.tryFind (fun (m, _, _) -> m = message) with
            | Some(_, _, run) ->
                remove message
                run ()
            | None -> remove message)

    member _.Layer = layer
    member _.Toasts = strip

    /// scrollWholeContent = false：内容自带滚动区与固定页脚（会话参数），卡片按视口封顶、页脚不被挤出。
    member _.ShowDialog
        (
            content: Control,
            width: float,
            ?onClosed: unit -> unit,
            ?canDismiss: unit -> bool,
            ?initialFocus: Control,
            ?scrollWholeContent: bool
        ) =
        layer.ShowDialog(
            content,
            width,
            ?onClosed = onClosed,
            ?canDismiss = canDismiss,
            ?initialFocus = initialFocus,
            ?scrollWholeContent = scrollWholeContent
        )

    member _.CloseDialog() = layer.CloseDialog()
    member _.ClosePopup() = layer.ClosePopup()
    member _.TrapTab(content, e) = layer.TrapTab(content, e)

    member this.WireDismiss() =
        layer.WireDismiss()

        root.PropertyChanged.Add(fun args ->
            if args.Property = Visual.BoundsProperty then
                fit ())

    /// 单入口：幂等渲染 OverlayVS 状态
    member this.Render(overlayVS: OverlayVS) =
        match overlayVS with
        | OverlayVS.Closed ->
            if layer.IsPopupOpen then
                layer.ClosePopup()

            if layer.IsDialogOpen then
                layer.CloseDialog()

            currentOverlayPhase <- OverlayPhase.Closed
        | OverlayVS.Drawer _ -> currentOverlayPhase <- OverlayPhase.DrawerOpen
        | OverlayVS.Dialog d -> currentOverlayPhase <- OverlayPhase.DialogOpen d.Kind
        | OverlayVS.Popup p -> currentOverlayPhase <- OverlayPhase.PopupOpen p.Kind

    /// 单入口：幂等渲染 ToastVS 列表（计时由 WorkspaceState 的 MsgTick / ExpiresAt 管）
    member this.RenderToasts(toasts: ToastVS list) =
        strip.Render toasts
        fit ()

    member this.Toast(message: string, tone: ToastTone) = this.ToastWith(message, tone, None)

    /// 带一个行内动作的提示条；点动作先关条再执行动作。
    member _.ToastWith(message: string, tone: ToastTone, action: (string * (unit -> unit)) option) =
        if not (String.IsNullOrWhiteSpace message) then
            // 同文只留最新一条、新条挤掉最旧的：被撤下的由 ToastQueue 给出。
            for dismissed in queue.Admit message do
                actions.RemoveAll(fun (m, _, _) -> m = dismissed) |> ignore
                strip.Remove dismissed

            action |> Option.iter (fun (label, run) -> actions.Add(message, label, run))
            strip.Add(message, tone, action |> Option.map fst)
            fit ()

    /// 触发最新一条仍可见、动作名为 label 的提示条动作；没有则 false。
    member _.TryInvokeToastAction(label: string) : bool =
        match actions |> Seq.filter (fun (_, l, _) -> l = label) |> Seq.tryLast with
        | Some(message, _, run) ->
            remove message
            run ()
            true
        | None -> false

    member _.HasToastAction(label: string) =
        actions |> Seq.exists (fun (_, l, _) -> l = label)

    member _.FocusToastAction() = strip.FocusAction()
    /// 提示条动作按钮失焦后的归宿（组合者填：通常是输入区）。
    member _.WireFocusHome(home: unit -> unit) = strip.WireFocusHome home
    member _.ToastActionButtons = strip.ActionButtons

    /// 唯一的 Escape 归属（topmost-first：浮层 → 对话框 → 最新提示条）。
    member _.HandleEscape() : bool =
        if layer.HandleEscape() then
            true
        else
            match List.tryLast queue.Showing with
            | Some newest ->
                remove newest
                true
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
        | Some content ->
            this.Mark content
            layer.ShowPopup(anchor, content, alignRight, look.MenuMinWidth)
        | None -> ()

    member this.ShowGroupedMenu(anchor: Control, alignRight: bool, groups: (string * PopupEntry list) list) =
        match PopupMenu.buildGrouped look.Menu this.Activate groups with
        | Some content ->
            this.Mark content
            layer.ShowPopup(anchor, content, alignRight, look.GroupedMenuMinWidth)
        | None -> ()

    /// 下拉选择器：点击或 Down/Up 弹出分组候选。
    member this.SelectButton(initialText: string, optionsOf: unit -> (string * PopupEntry list) list) =
        let b = Wanxiang2.PopupMenu.SelectButton(look.Menu, initialText)
        b.OpenRequested.Add(fun () -> this.ShowGroupedMenu(b.View, false, optionsOf ()))
        b
