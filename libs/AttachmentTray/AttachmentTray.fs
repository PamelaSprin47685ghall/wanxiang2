namespace Wanxiang2.AttachmentTray

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 用到的笔刷
[<RequireQualifiedAccess>]
type TrayInk =
    | Text
    | TextMuted
    | TextFaint
    | Danger
    | Accent
    | SurfaceContainer
    | Hairline

/// 用到的活度量
[<RequireQualifiedAccess>]
type TrayGauge =
    | SpaceXs
    | SpaceMd
    | SpaceXl
    | RadiusSm
    | RadiusMd
    | BorderWidth
    | FontMicro
    | IconSlot
    | NameMaxWidth
    | StateWidth
    | PadX
    | PadY
    | MaxHeight
    | ScrollGutter
    | CompactPadBottom

/// 用到的图标
[<RequireQualifiedAccess>]
type TrayGlyph =
    | Image
    | File
    | Alert
    | Close
    | Spinner

/// 一条附件的呈现事实（宿主从 PendingAttachment 折出；本库不依赖兄弟库）
type TrayItem =
    { Id: Guid
      FileName: string
      SizeText: string
      StateText: string
      Tip: string
      /// 整块芯片的读屏名
      Name: string
      Failed: bool
      Ready: bool
      IsImage: bool
      ShrinkNote: string option
      Thumbnail: IImage option }

/// 外观
type AttachmentTrayLook =
    { Ink: TrayInk -> IBrush
      Gauge: TrayGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      Glyph: TrayGlyph -> IBrush -> Control
      /// 分组卡样式（旧 Ui.groupingCard）
      DressChip: Border -> unit
      /// 移除图标按钮样式（旧 Ui.iconButton）
      DressRemove: Border -> unit
      Reduced: IObservable<bool> }

/// 输入区附件托盘（旧 Composer.attachmentStrip / RenderAttachments）：可删除小卡；窄屏横向滚。
type AttachmentTray(look: AttachmentTrayLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let strip = WrapPanel(Orientation = Orientation.Horizontal, Focusable = true)
    let scroller =
        ScrollViewer(
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = true,
            IsVisible = false)
    let removes = System.Collections.Generic.Dictionary<Guid, Border>()
    let mutable items: TrayItem list = []
    let mutable compact = false
    let mutable onRemove: Guid -> unit = ignore
    let mutable pendingFocus: Guid option = None
    let removed = Event<Guid>()

    let removeSpot (label: string) =
        let b = Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true)
        look.DressRemove b
        AutomationProperties.SetName(b, label)
        ToolTip.SetTip(b, label)
        b

    let build (item: TrayItem) =
        let iconBrush = if item.Failed then ink TrayInk.Danger else ink TrayInk.TextMuted
        let glyph =
            if item.Failed then look.Glyph TrayGlyph.Alert iconBrush
            elif item.Ready then look.Glyph (if item.IsImage then TrayGlyph.Image else TrayGlyph.File) iconBrush
            else look.Glyph TrayGlyph.Spinner iconBrush
        glyph.VerticalAlignment <- VerticalAlignment.Center
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        let iconSlot =
            match item.Thumbnail with
            | Some bmp when not item.Failed ->
                let side = Follow.now (gauge TrayGauge.IconSlot) * 2.0
                let thumb =
                    Border(
                        Width = side,
                        Height = side,
                        ClipToBounds = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = Image(Source = bmp, Stretch = Stretch.UniformToFill, Opacity = (if item.Ready then 1.0 else 0.6)))
                Follow.live thumb (gauge TrayGauge.RadiusSm) (fun r -> thumb.CornerRadius <- CornerRadius r)
                AutomationProperties.SetName(thumb, sprintf "图片预览 %s" item.FileName)
                thumb :> Control
            | _ ->
                let slot = Border(VerticalAlignment = VerticalAlignment.Center, Child = glyph)
                Follow.live slot (gauge TrayGauge.IconSlot) (fun s -> slot.Width <- s; slot.Height <- s)
                slot :> Control
        let name =
            TextBlock(
                Text = item.FileName,
                Foreground = ink TrayInk.Text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center)
        Follow.live name (gauge TrayGauge.FontMicro) (fun s -> name.FontSize <- s)
        Follow.live name (gauge TrayGauge.NameMaxWidth) (fun w -> name.MaxWidth <- w)
        Follow.live name look.BodyFont (fun f -> name.FontFamily <- f)
        let size =
            TextBlock(
                Text = item.StateText,
                Foreground = (if item.Failed then ink TrayInk.Danger else ink TrayInk.TextFaint),
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center)
        Follow.live size (gauge TrayGauge.FontMicro) (fun s -> size.FontSize <- s)
        Follow.live size (gauge TrayGauge.StateWidth) (fun w -> size.Width <- w)
        Follow.live size look.BodyFont (fun f -> size.FontFamily <- f)
        ToolTip.SetTip(size, item.Tip)
        let shrink =
            item.ShrinkNote
            |> Option.map (fun n ->
                let badge =
                    TextBlock(Text = "已缩小", Foreground = ink TrayInk.Accent, VerticalAlignment = VerticalAlignment.Center)
                Follow.live badge (gauge TrayGauge.FontMicro) (fun s -> badge.FontSize <- s)
                Follow.live badge look.BodyFont (fun f -> badge.FontFamily <- f)
                badge.Margin <- Thickness(0.0, 0.0, 4.0, 0.0)
                ToolTip.SetTip(badge, n)
                AutomationProperties.SetName(badge, n)
                AutomationProperties.SetAutomationId(badge, "attachment-shrink-note")
                badge)
        let removeLabel = sprintf "移除附件 %s" item.FileName
        let remove = removeSpot removeLabel
        let close = look.Glyph TrayGlyph.Close (ink TrayInk.TextMuted)
        close.HorizontalAlignment <- HorizontalAlignment.Center
        close.VerticalAlignment <- VerticalAlignment.Center
        AutomationProperties.SetName(close, "移除")
        remove.Child <- close
        removes.[item.Id] <- remove
        let id = item.Id
        remove.OnInvoke(fun () ->
            let remaining = items |> List.filter (fun a -> a.Id <> id)
            match remaining with
            | [] ->
                pendingFocus <- None
                onRemove id
                removed.Trigger id
            | rest ->
                let idx = items |> List.tryFindIndex (fun a -> a.Id = id) |> Option.defaultValue 0
                let target = rest.[Math.Clamp(idx, 0, rest.Length - 1)].Id
                pendingFocus <- Some target
                onRemove id
                removed.Trigger id
                Dispatcher.UIThread.Post(fun () ->
                    match pendingFocus with
                    | Some targetId ->
                        pendingFocus <- None
                        match removes.TryGetValue targetId with
                        | true, btn when btn.IsEffectivelyVisible && btn.IsEnabled -> btn.Focus() |> ignore
                        | _ ->
                            if strip.IsEffectivelyVisible && strip.IsEnabled then strip.Focus() |> ignore
                    | None -> ()))
        let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live row (gauge TrayGauge.SpaceXs) (fun s -> row.Spacing <- s)
        row.Children.Add iconSlot
        row.Children.Add name
        shrink |> Option.iter (fun b -> row.Children.Add b)
        row.Children.Add size
        row.Children.Add remove
        let chip = Border(Tag = item.Id, Child = row)
        look.DressChip chip
        Follow.live2 chip (gauge TrayGauge.PadX) (gauge TrayGauge.PadY) (fun x y -> chip.Padding <- Thickness(x, y))
        Follow.live chip (gauge TrayGauge.RadiusMd) (fun r -> chip.CornerRadius <- CornerRadius r)
        Follow.live chip (gauge TrayGauge.BorderWidth) (fun w -> chip.BorderThickness <- Thickness w)
        chip.Background <- ink TrayInk.SurfaceContainer
        chip.BorderBrush <- ink TrayInk.Hairline
        AutomationProperties.SetName(chip, item.Name)
        chip :> Control

    let snap () =
        if strip.Children.Count > 0 then
            match strip.Children.[0] with
            | :? Border as chip ->
                let h = chip.Bounds.Height
                if h > 0.0 then
                    let gap = strip.LineSpacing
                    let maxH = Follow.now (gauge TrayGauge.MaxHeight)
                    let rows = max 1 (int ((maxH + gap) / (h + gap)))
                    scroller.MaxHeight <- float rows * h + float (rows - 1) * gap
            | _ -> ()

    let applyCompact () =
        scroller.HorizontalScrollBarVisibility <- if compact then ScrollBarVisibility.Auto else ScrollBarVisibility.Disabled
        scroller.VerticalScrollBarVisibility <- if compact then ScrollBarVisibility.Disabled else ScrollBarVisibility.Auto
        strip.Margin <- if compact then Thickness(0.0, 0.0, 0.0, Follow.now (gauge TrayGauge.CompactPadBottom)) else Thickness 0.0

    let render () =
        removes.Clear()
        strip.Children.Clear()
        for item in items do strip.Children.Add(build item)
        scroller.IsVisible <- not (List.isEmpty items)
        pendingFocus
        |> Option.iter (fun id ->
            pendingFocus <- None
            Dispatcher.UIThread.Post(fun () ->
                match removes.TryGetValue id with
                | true, btn when btn.IsEffectivelyVisible && btn.IsEnabled -> btn.Focus() |> ignore
                | _ -> ()))
        Dispatcher.UIThread.Post(fun () -> snap ())

    do
        Follow.live2 strip (gauge TrayGauge.SpaceMd) (gauge TrayGauge.SpaceMd) (fun a b ->
            strip.ItemSpacing <- a
            strip.LineSpacing <- b)
        Follow.live2 scroller (gauge TrayGauge.ScrollGutter) (gauge TrayGauge.SpaceMd) (fun g m ->
            scroller.Margin <- Thickness(0.0, 0.0, g, m))
        Follow.live scroller (gauge TrayGauge.MaxHeight) (fun h ->
            if List.isEmpty items then scroller.MaxHeight <- h else snap ())
        Follow.live scroller (gauge TrayGauge.CompactPadBottom) (fun _ -> applyCompact ())
        strip.LayoutUpdated.Add(fun _ -> snap ())

    /// 放进布局的根（内部是滚动区）
    member _.Host: Control = scroller
    member _.Strip = strip
    member _.IsShown = scroller.IsVisible
    member _.Count = items.Length
    /// 移除后（宿主同步数据前可先听）
    member _.Removed = removed.Publish

    member _.OnRemove(f: Guid -> unit) = onRemove <- f

    member _.SetCompact(value: bool) =
        compact <- value
        applyCompact ()

    member _.SetItems(next: TrayItem list) =
        if items <> next then
            if pendingFocus.IsNone then
                removes
                |> Seq.tryPick (fun kv -> if kv.Value.IsFocused then Some kv.Key else None)
                |> Option.filter (fun id -> next |> List.exists (fun a -> a.Id = id))
                |> Option.iter (fun id -> pendingFocus <- Some id)
            items <- next
            render ()
