namespace Wanxiang2.OverlayLayer

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 覆盖层的外观与时长。全部由主题管理者提供活的实例；本库不写字面样式值。
type OverlayLayerAppearance =
    { Scrim: IBrush
      /// 对话框与浮层卡的底
      Surface: IBrush
      /// 卡片描边（最轻 hairline）
      Edge: IBrush
      EdgeWidth: IObservable<Thickness>
      DialogRadius: IObservable<CornerRadius>
      PopupRadius: IObservable<CornerRadius>
      DialogPadding: IObservable<Thickness>
      PopupPadding: IObservable<Thickness>
      DialogShadow: IObservable<BoxShadows>
      PopupShadow: IObservable<BoxShadows>
      /// 视口留白：对话框、浮层钳位的唯一依据
      ViewportInset: IObservable<float>
      /// 浮层与锚点的间距
      PopupGap: IObservable<float>
      /// 浮层默认最小宽度
      PopupMinWidth: IObservable<float>
      /// 遮罩淡入淡出时长；减弱动效时由主题给 0
      ScrimFade: IObservable<TimeSpan>
      ScrimEasing: Avalonia.Animation.Easings.Easing }

/// 浮层落点（插槽）：锚点左上角 x、y、锚点宽高、浮层宽高、视口宽高、留白、间距、是否右对齐 → 浮层左上角。
/// 组合者填入 PopupPlacement.place。
type PopupPlace = float -> float -> float -> float -> float -> float -> float -> float -> float -> float -> bool -> float * float

/// 覆盖层：对话框（含嵌套栈与关闭守卫）、浮层、遮罩、焦点记忆与 Tab 圈定
/// （迁自旧 Controls/OverlayHost.fs 的对话框与浮层部分；提示条在 ToastStrip，Esc 的最后一层由组合者接）。
/// 层按 遮罩、对话框卡、浮层捕获层、浮层卡 的次序加进 root。
type OverlayLayer(root: Grid, look: OverlayLayerAppearance, place: PopupPlace) =
    let mutable viewportInset = 0.0
    let mutable popupGap = 0.0
    let mutable defaultPopupMinWidth = 0.0
    let mutable scrimFade = TimeSpan.Zero

    let scrim = Border(Background = look.Scrim, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch)
    let dialogCard =
        Border(Background = look.Surface, BorderBrush = look.Edge, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsVisible = false)
    let popupCatcher =
        Border(Background = Brushes.Transparent, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch)
    let popupCard =
        Border(Background = look.Surface, BorderBrush = look.Edge, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top)

    let mutable onDialogClosed: unit -> unit = id
    let dialogStack = ResizeArray<Control * float * (unit -> unit) * (unit -> bool)>()
    let mutable dialogGuard: unit -> bool = fun () -> true
    let mutable previousDialogFocus: IInputElement option = None
    let mutable previousPopupFocus: IInputElement option = None
    let mutable lastPopupFocus: IInputElement option = None
    let mutable lastPopupAnchor: Control option = None
    let mutable dialogPreferredWidth = 0.0
    let mutable popupPreferredMinWidth = 0.0
    let mutable popupAnchor: Control option = None
    let mutable popupAlignRight = false
    let mutable onPopupClosed: (unit -> unit) option = None
    let currentFocus () =
        match TopLevel.GetTopLevel root with
        | null -> None
        | top ->
            match top.FocusManager with
            | null -> None
            | manager -> manager.GetFocusedElement() |> Option.ofObj


    /// 从浮层菜单里打开对话框时，菜单项已脱离视觉树：回退到浮层锚点或上次浮层焦点。
    let rememberDialogFocus () =
        let cur = currentFocus ()
        let usable =
            match cur with
            | Some(:? Control as c) when c.IsEnabled -> true
            | _ -> false
        previousDialogFocus <-
            if usable then cur
            else
                match lastPopupAnchor with
                | Some a when a.IsEnabled -> Some(a :> IInputElement)
                | _ ->
                    match lastPopupFocus with
                    | Some(:? Control as c) when c.IsEnabled -> lastPopupFocus
                    | _ -> None

    let rememberPopupFocus () =
        let cur = currentFocus ()
        previousPopupFocus <- cur
        if cur.IsSome then lastPopupFocus <- cur

    let restoreDialogFocus () =
        let previous = previousDialogFocus
        previousDialogFocus <- None
        match previous with
        | Some(:? Control as c) -> Dispatcher.UIThread.Post(fun () -> if c.IsEnabled then c.Focus() |> ignore) // 跨线程/布局归还焦点，非焦点竞态
        | _ -> ()

    let restorePopupFocus () =
        let previous = previousPopupFocus
        previousPopupFocus <- None
        match previous with
        | Some(:? Control as c) -> Dispatcher.UIThread.Post(fun () -> if c.IsEnabled then c.Focus() |> ignore) // 跨线程/布局归还焦点，非焦点竞态
        | _ -> ()

    let rec descendants (control: Control) =
        seq {
            yield control
            match control with
            | :? Panel as panel ->
                for child in panel.Children do
                    yield! descendants child
            | :? Decorator as decorator when not (isNull decorator.Child) -> yield! descendants decorator.Child
            | :? ContentControl as host ->
                match host.Content with
                | :? Control as child -> yield! descendants child
                | _ -> ()
            | _ -> ()
        }

    /// 有效可见（而非本地 IsVisible）：折叠容器里的字段不进 Tab 序。
    let focusables (content: Control) =
        descendants content |> Seq.filter (fun c -> c.Focusable && c.IsEffectivelyVisible && c.IsEnabled) |> Array.ofSeq

    /// 聚焦首个元素：若立即可聚焦则同步，否则安排在 UIThread 布局完成后执行
    let focusFirst (content: Control) =
        match focusables content |> Array.tryHead with
        | Some c -> c.Focus() |> ignore
        | None ->
            // 跨线程/布局排队等待首帧排版完成，非焦点竞态
            Dispatcher.UIThread.Post(fun () -> focusables content |> Array.tryHead |> Option.iter (fun c -> c.Focus() |> ignore)) // 跨线程/布局排队，非焦点竞态

    let wrapForDialog (content: Control) : Control =
        match content with
        | :? ScrollViewer -> content
        | _ ->
            ScrollViewer(
                Content = content,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
            :> Control

    let trapTab (content: Control) (e: KeyEventArgs) =
        if e.Key = Key.Tab then
            let items = focusables content
            if items.Length > 0 then
                let focused = currentFocus () |> Option.toObj
                let current = items |> Array.tryFindIndex (fun item -> obj.ReferenceEquals(item, focused)) |> Option.defaultValue -1
                let backwards = e.KeyModifiers.HasFlag KeyModifiers.Shift
                let next =
                    if backwards then (if current <= 0 then items.Length - 1 else current - 1)
                    elif current < 0 || current >= items.Length - 1 then 0
                    else current + 1
                e.Handled <- true
                items[next].Focus(NavigationMethod.Tab) |> ignore

    let fitDialogToViewport () =
        let width = max 0.0 (root.Bounds.Width - viewportInset * 2.0)
        let height = max 0.0 (root.Bounds.Height - viewportInset * 2.0)
        if width > 0.0 then
            dialogCard.MaxWidth <- width
            if dialogPreferredWidth > 0.0 then dialogCard.Width <- min dialogPreferredWidth width
        if height > 0.0 then dialogCard.MaxHeight <- height

    let fitPopupToViewport () =
        let width = max 0.0 (root.Bounds.Width - viewportInset * 2.0)
        let height = max 0.0 (root.Bounds.Height - viewportInset * 2.0)
        if width > 0.0 then
            popupCard.MaxWidth <- width
            popupCard.MinWidth <- min popupPreferredMinWidth width
        if height > 0.0 then popupCard.MaxHeight <- height

    let positionPopup () =
        match popupAnchor with
        | Some anchor when popupCard.IsVisible ->
            try
                let point = anchor.TranslatePoint(Point(0.0, anchor.Bounds.Height + popupGap), root)
                if point.HasValue then
                    let p = point.Value
                    fitPopupToViewport ()
                    let width = min popupCard.MaxWidth (max popupCard.Bounds.Width popupCard.MinWidth)
                    let height = min popupCard.MaxHeight popupCard.Bounds.Height
                    let x, y =
                        place p.X (p.Y - anchor.Bounds.Height - popupGap) anchor.Bounds.Width anchor.Bounds.Height
                            width height root.Bounds.Width root.Bounds.Height viewportInset popupGap popupAlignRight
                    popupCard.Margin <- Thickness(x, y, 0.0, 0.0)
            with _ -> ()
        | _ -> ()

    let closePopupNow () =
        popupCatcher.IsVisible <- false
        popupCard.IsVisible <- false
        popupCard.Child <- null
        popupAnchor <- None
        restorePopupFocus ()
        onPopupClosed |> Option.iter (fun f -> f ())

    let scrimTransitions () =
        let fade = Avalonia.Animation.DoubleTransition()
        fade.Property <- Visual.OpacityProperty
        fade.Duration <- scrimFade
        fade.Easing <- look.ScrimEasing
        let t = Avalonia.Animation.Transitions()
        t.Add fade
        t

    do
        look.ViewportInset.Subscribe(fun v ->
            viewportInset <- v
            dialogCard.Margin <- Thickness v
            fitDialogToViewport ()
            fitPopupToViewport ()) |> ignore
        look.PopupGap.Subscribe(fun v -> popupGap <- v) |> ignore
        look.PopupMinWidth.Subscribe(fun v -> defaultPopupMinWidth <- v) |> ignore
        look.ScrimFade.Subscribe(fun v ->
            scrimFade <- v
            scrim.Transitions <- scrimTransitions ()) |> ignore
        for card, radius, padding, shadow in
            [ dialogCard, look.DialogRadius, look.DialogPadding, look.DialogShadow
              popupCard, look.PopupRadius, look.PopupPadding, look.PopupShadow ] do
            card.Bind(Border.BorderThicknessProperty, look.EdgeWidth) |> ignore
            card.Bind(Border.CornerRadiusProperty, radius) |> ignore
            card.Bind(Decorator.PaddingProperty, padding) |> ignore
            card.Bind(Border.BoxShadowProperty, shadow) |> ignore
        scrim.Opacity <- 0.0
        for layer in [ scrim :> Control; dialogCard :> Control; popupCatcher :> Control; popupCard :> Control ] do
            Grid.SetColumnSpan(layer, 8)
            Grid.SetRowSpan(layer, 8)
            root.Children.Add layer
        // 层外滚轮与层外点击是同一类「取消浮层」手势：关闭并消费，免得同一脉冲穿透到底层滚动区；
        // 落在浮层卡内的滚轮放行给浮层自己的滚动条。
        root.PointerWheelChanged.Add(fun e ->
            if popupCard.IsVisible then
                let inside = try popupCard.Bounds.Contains(e.GetPosition root) with _ -> false
                if not inside then
                    e.Handled <- true
                    closePopupNow ())

    member private _.HideDialog() =
        dialogCard.IsVisible <- false
        dialogCard.Child <- null
        // 遮罩淡出统一经 Avalonia Transitions 处理，不再启动手搓 DispatcherTimer
        scrim.Opacity <- 0.0
        scrim.IsVisible <- false
        scrim.IsHitTestVisible <- true

    member _.Root = root
    member _.DialogCard = dialogCard
    member _.PopupCard = popupCard

    member this.CloseDialog() =
        let callback = onDialogClosed
        onDialogClosed <- id
        if dialogStack.Count > 0 then
            // 嵌套关闭：恢复被盖住的内容、onClosed 与守卫；焦点记忆仍指向最初的触发源。
            let child, width, closed, guard = dialogStack[dialogStack.Count - 1]
            dialogStack.RemoveAt(dialogStack.Count - 1)
            dialogCard.Child <- child
            dialogPreferredWidth <- width
            onDialogClosed <- closed
            dialogGuard <- guard
            fitDialogToViewport ()
            if not (isNull child) then focusFirst child
            callback ()
        else
            this.HideDialog()
            restoreDialogFocus ()
            callback ()

    member this.ShowDialog(content: Control, width: float, ?onClosed: unit -> unit, ?canDismiss: unit -> bool, ?initialFocus: Control, ?scrollWholeContent: bool) =
        if dialogCard.IsVisible then dialogStack.Add((dialogCard.Child, dialogPreferredWidth, onDialogClosed, dialogGuard))
        else rememberDialogFocus ()
        onDialogClosed <- defaultArg onClosed id
        dialogGuard <- defaultArg canDismiss (fun () -> true)
        let hosted = if defaultArg scrollWholeContent true then wrapForDialog content else content
        dialogCard.Child <- hosted
        dialogPreferredWidth <- width
        fitDialogToViewport ()
        if root.Bounds.Width <= 0.0 then dialogCard.Width <- width
        dialogCard.IsVisible <- true
        scrim.IsVisible <- true
        scrim.IsHitTestVisible <- true
        scrim.Opacity <- 1.0
        hosted.KeyDown.Add(fun e -> trapTab hosted e)
        match initialFocus with
        | Some target -> Dispatcher.UIThread.Post(fun () -> target.Focus() |> ignore)
        // 跨线程/布局排队等待首帧排版完成，非焦点竞态
        | None -> Dispatcher.UIThread.Post(fun () -> focusFirst content) // 跨线程/布局排队，非焦点竞态

    member _.IsDialogOpen = dialogCard.IsVisible

    member _.ClosePopup() = closePopupNow ()

    member _.SetOnPopupClosed(f: unit -> unit) = onPopupClosed <- Some f

    /// 在 anchor 附近打开浮层；alignRight：浮层右边缘与锚点右边缘对齐。
    member _.ShowPopup(anchor: Control, content: Control, alignRight: bool, ?minWidth: float) =
        if not popupCard.IsVisible then rememberPopupFocus ()
        popupCard.Child <- content
        popupAnchor <- Some anchor
        lastPopupAnchor <- Some anchor
        popupAlignRight <- alignRight
        popupPreferredMinWidth <- defaultArg minWidth defaultPopupMinWidth
        fitPopupToViewport ()
        if root.Bounds.Width <= 0.0 then popupCard.MinWidth <- popupPreferredMinWidth
        popupCatcher.IsVisible <- true
        popupCard.IsVisible <- true
        content.KeyDown.Add(fun e -> trapTab content e)
        // 平台原生排版与定位：测量后同步布局，非焦点竞态
        Dispatcher.UIThread.Post(fun () ->
            positionPopup ()
            focusFirst content
            Dispatcher.UIThread.Post(fun () -> if popupCard.IsVisible then positionPopup ()) // 跨线程/布局排队，非焦点竞态
        )

    member _.IsPopupOpen = popupCard.IsVisible
    member _.LastPopupFocus = lastPopupFocus
    member _.LastPopupAnchor = lastPopupAnchor
    member _.Focusables(content: Control) = focusables content

    /// Tab 圈定：Tab / Shift+Tab 在 content 的可聚焦集合内循环。compact 抽屉也用它。
    member _.TrapTab(content: Control, e: KeyEventArgs) = trapTab content e

    /// Esc：浮层 → 对话框。守卫拦下的关闭也算已处理。都没有返回 false（提示条由组合者接着处理）。
    member this.HandleEscape() : bool =
        if popupCard.IsVisible then
            this.ClosePopup()
            true
        elif dialogCard.IsVisible then
            if dialogGuard () then this.CloseDialog()
            true
        else false

    /// 层外点击关闭、遮罩与 Esc 同守卫、视口变化时重新钳位。
    member this.WireDismiss() =
        popupCatcher.PointerPressed.Add(fun _ -> this.ClosePopup())
        scrim.PointerPressed.Add(fun _ -> if dialogGuard () then this.CloseDialog())
        root.PropertyChanged.Add(fun args ->
            if args.Property = Visual.BoundsProperty then
                fitDialogToViewport ()
                fitPopupToViewport ()
                if popupCard.IsVisible then positionPopup ())
