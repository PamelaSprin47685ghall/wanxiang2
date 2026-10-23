namespace Wanxiang.UI

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Wanxiang.Protocol
open Wanxiang2.ToastStrip
open Wanxiang2.ThemePalette
open Wanxiang.UI
open Wanxiang2.WorkspaceState

/// 焦点目标类型别名（直接消费 Wanxiang2.WorkspaceState.FocusTarget）
type FocusTarget = Wanxiang2.WorkspaceState.FocusTarget
/// 聊天区滚动命令别名（直接消费 Wanxiang2.WorkspaceState.ScrollCommand）
type ScrollCommand = Wanxiang2.WorkspaceState.ScrollCommand
/// 主题类型别名（直接消费 Wanxiang2.WorkspaceState.ThemeKind）
type ThemeKind = Wanxiang2.WorkspaceState.ThemeKind
/// 提示条语气别名（直接消费 Wanxiang2.WorkspaceState.ToastTone）
type ToastTone = Wanxiang2.WorkspaceState.ToastTone
/// 契约中的副作用类型别名（直接消费 Wanxiang2.WorkspaceState.Effect）
type Effect = Wanxiang2.WorkspaceState.Effect

/// 头插前置历史时的偏移量计算（纯函数，易于单元测试钉住）。
/// 给定前置前的 (oldExtent, oldOffset) 与前置后的 newExtent，
/// 恢复时的 offset 应增加高度差 (newExtent - oldExtent)。
module ScrollAnchorMath =

    /// 计算前置后应该恢复的目标 Y 偏移量。
    /// 数学关系：newOffset = max 0.0 (oldOffset + max 0.0 (newExtent - oldExtent))
    let computeRestoredOffset (oldExtent: float) (oldOffset: float) (newExtent: float) : float =
        let delta = max 0.0 (newExtent - oldExtent)
        max 0.0 (oldOffset + delta)

/// 副作用执行器接收槽：各字段与契约 Effect 变体一一对应。
type EffectSink =
    {
        Focus: FocusTarget -> unit
        ScrollChat: ScrollCommand -> unit
        SendWire: WireEvent -> unit
        SendCommand: Wanxiang.Core.ClientCommand -> unit
        After: int * obj -> unit
        Every: string * int -> unit
        StopEvery: string -> unit
        ShowToast: string * ToastTone -> unit
        Clipboard: string -> unit
        OpenLink: string -> unit
        SaveDownload: string * byte array -> unit
        ApplyTheme: ThemeKind -> unit
        SetWindowBusy: bool -> unit
        CloseWindow: unit -> unit
        PrefsSave: UiPrefs -> unit
        PrefsLoad: unit -> unit
        DraftWrite: DraftWrite -> unit
        BrowserHook: string -> unit
    }

    /// 按序执行 Effect 列表。
    /// 对于 Focus 和 ScrollChat，内部经 Dispatcher.UIThread.Post(..., Input) 保证布局完成后落地
    /// （全仓唯一允许的 Post 汇聚点）。其余副作用直接分派。
    member this.Run(effects: Effect list) : unit =
        for effect in effects do
            match effect with
            | Effect.Focus target -> Dispatcher.UIThread.Post((fun () -> this.Focus target), DispatcherPriority.Input)
            | Effect.ScrollChat cmd ->
                Dispatcher.UIThread.Post((fun () -> this.ScrollChat cmd), DispatcherPriority.Input)
            | Effect.SendWire ev -> this.SendWire ev
            | Effect.SendCommand cmd -> this.SendCommand cmd
            | Effect.After(ms, msg) -> this.After(ms, msg)
            | Effect.Every(handle, ms) -> this.Every(handle, ms)
            | Effect.StopEvery handle -> this.StopEvery handle
            | Effect.ShowToast(text, tone) -> this.ShowToast(text, tone)
            | Effect.Clipboard text -> this.Clipboard text
            | Effect.OpenLink url -> this.OpenLink url
            | Effect.SaveDownload(fileName, bytes) -> this.SaveDownload(fileName, bytes)
            | Effect.ApplyTheme theme -> this.ApplyTheme theme
            | Effect.SetWindowBusy busy -> this.SetWindowBusy busy
            | Effect.CloseWindow -> this.CloseWindow()
            | Effect.PrefsSave prefs -> this.PrefsSave prefs
            | Effect.PrefsLoad -> this.PrefsLoad()
            | Effect.DraftWrite dw -> this.DraftWrite dw
            | Effect.BrowserHook hookId -> this.BrowserHook hookId

    /// 纯执行（在测试中不经 UI 调度器，直接顺序调用各委托）
    member this.RunSync(effects: Effect list) : unit =
        for effect in effects do
            match effect with
            | Effect.Focus target -> this.Focus target
            | Effect.ScrollChat cmd -> this.ScrollChat cmd
            | Effect.SendWire ev -> this.SendWire ev
            | Effect.SendCommand cmd -> this.SendCommand cmd
            | Effect.After(ms, msg) -> this.After(ms, msg)
            | Effect.Every(handle, ms) -> this.Every(handle, ms)
            | Effect.StopEvery handle -> this.StopEvery handle
            | Effect.ShowToast(text, tone) -> this.ShowToast(text, tone)
            | Effect.Clipboard text -> this.Clipboard text
            | Effect.OpenLink url -> this.OpenLink url
            | Effect.SaveDownload(fileName, bytes) -> this.SaveDownload(fileName, bytes)
            | Effect.ApplyTheme theme -> this.ApplyTheme theme
            | Effect.SetWindowBusy busy -> this.SetWindowBusy busy
            | Effect.CloseWindow -> this.CloseWindow()
            | Effect.PrefsSave prefs -> this.PrefsSave prefs
            | Effect.PrefsLoad -> this.PrefsLoad()
            | Effect.DraftWrite dw -> this.DraftWrite dw
            | Effect.BrowserHook hookId -> this.BrowserHook hookId

/// 控件名字典 / 引用查找接口，用于根据 FocusTarget 查找 IControl 并聚焦。
type FocusResolver =
    {
        /// 通过名称或标识查找控件；未找到返回 None
        FindNamedControl: string -> Control option
        /// 输入区控件
        ComposerInput: unit -> Control option
        /// 侧栏搜索控件
        SidebarSearch: unit -> Control option
        /// 对话行控件
        ConversationRow: Guid -> Control option
        /// 问答卡片控件
        ExchangeMessage: string -> Control option
        /// 对话框初始焦点控件
        DialogInitial: unit -> Control option
        /// 侧栏展开/收起切换钮
        DrawerToggle: unit -> Control option
        /// 提示条焦点控件
        Toast: unit -> Control option
    }

    static member Empty =
        {
            FindNamedControl = fun _ -> None
            ComposerInput = fun () -> None
            SidebarSearch = fun () -> None
            ConversationRow = fun _ -> None
            ExchangeMessage = fun _ -> None
            DialogInitial = fun () -> None
            DrawerToggle = fun () -> None
            Toast = fun () -> None
        }

module EffectRunner =

    /// 执行焦点分派（在 UI 线程上运行）
    let dispatchFocus (resolver: FocusResolver) (target: FocusTarget) : unit =
        let controlOpt =
            match target with
            | FocusTarget.ComposerInput -> resolver.ComposerInput()
            | FocusTarget.SidebarSearch -> resolver.SidebarSearch()
            | FocusTarget.Row convId -> resolver.ConversationRow convId
            | FocusTarget.Exchange msgId -> resolver.ExchangeMessage msgId
            | FocusTarget.DialogInitial -> resolver.DialogInitial()
            | FocusTarget.DrawerToggle -> resolver.DrawerToggle()
            | FocusTarget.Toast -> resolver.Toast()
            | FocusTarget.None_ -> None

        controlOpt
        |> Option.iter (fun c ->
            try
                if c.Focusable && c.IsEnabled && c.IsEffectivelyVisible then
                    c.Focus() |> ignore
            with _ ->
                ())

    /// 执行滚动分派（在 UI 线程上运行）
    let dispatchScrollChat
        (scroller: ScrollViewer option)
        (anchorRef: (float * float) option ref)
        (cmd: ScrollCommand)
        : unit =
        match scroller with
        | None -> ()
        | Some viewer ->
            match cmd with
            | ScrollCommand.End -> viewer.ScrollToEnd()
            | ScrollCommand.Beginning -> viewer.ScrollToHome()
            | ScrollCommand.ToExchange _msgId ->
                // 查到该消息并滚至可视区（由调用方后续或在此处扩展）
                ()
            | ScrollCommand.PreparePrepend ->
                // 记下当前 extent 与 offset
                anchorRef.Value <- Some(viewer.Extent.Height, viewer.Offset.Y)
            | ScrollCommand.RestorePrepend ->
                // 补偿高度差并恢复
                match anchorRef.Value with
                | None -> ()
                | Some(oldExtent, oldOffset) ->
                    anchorRef.Value <- None

                    let newOffset =
                        ScrollAnchorMath.computeRestoredOffset oldExtent oldOffset viewer.Extent.Height

                    viewer.Offset <- Vector(viewer.Offset.X, newOffset)
