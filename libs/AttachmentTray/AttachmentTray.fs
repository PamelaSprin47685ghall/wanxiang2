namespace Wanxiang2.AttachmentTray

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.IconSet

/// 一条附件的呈现事实
type TrayItem =
    {
        Id: Guid
        FileName: string
        SizeText: string
        StateText: string
        Tip: string
        Name: string
        Failed: bool
        Ready: bool
        IsImage: bool
        ShrinkNote: string option
        Thumbnail: IImage option
    }

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 输入区附件托盘（原生 Button / WrapPanel / ScrollViewer 实现）：
/// 呈现事实全部由调用方经 Render(items: TrayItem list, compact: bool) 一次给全（含缩略图与缩小说明）；
/// 上传进度 chip 动画改声明式 anim-breathe / anim-spin，废除 Look 与 DispatcherTimer。
type AttachmentTray() =
    let strip =
        WrapPanel(Orientation = Orientation.Horizontal, ItemSpacing = 6.0, LineSpacing = 6.0)

    do AutomationProperties.SetName(strip, "附件列表")

    let scroller =
        ScrollViewer(
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AllowAutoHide = true,
            Margin = Thickness(0.0, 0.0, 4.0, 6.0),
            IsVisible = false
        )

    let removes = System.Collections.Generic.Dictionary<Guid, Button>()
    let mutable items: TrayItem list = []
    let mutable compact = false
    let mutable onRemove: Guid -> unit = ignore
    let removed = Event<Guid>()

    let removeButton (label: string) =
        let b = Button(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true)
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.Width <- 18.0
        b.Height <- 18.0
        b.Padding <- Thickness 0.0
        AutomationProperties.SetName(b, label)
        ToolTip.SetTip(b, label)
        b.HorizontalAlignment <- HorizontalAlignment.Center
        b.VerticalAlignment <- VerticalAlignment.Center

        let close = IconSet.control Glyph.Close 12.0

        close.Bind(TemplatedControl.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        b.Content <- close
        b

    let buildIcon (item: TrayItem) : Control =
        if item.Failed then
            let alert = IconSet.control Glyph.Alert 14.0

            alert.Bind(TemplatedControl.ForegroundProperty, Dyn.res "Palette.Danger")
            |> ignore

            alert :> Control
        elif item.Ready then
            let p = IconSet.control (if item.IsImage then Glyph.Image else Glyph.File) 14.0

            p.Bind(TemplatedControl.ForegroundProperty, Dyn.res "Palette.TextMuted")
            |> ignore

            p :> Control
        else
            // 上传中：Lucide 转圈，挂 anim-spin
            let spin = IconSet.control Glyph.Loader 14.0

            spin.Bind(TemplatedControl.ForegroundProperty, Dyn.res "Palette.Accent")
            |> ignore

            spin.Classes.Add "anim-spin"
            spin :> Control

    let build (item: TrayItem) =
        let iconSlot =
            match item.Thumbnail with
            | Some bmp when not item.Failed ->
                let thumb =
                    Border(
                        Width = 28.0,
                        Height = 28.0,
                        CornerRadius = CornerRadius 4.0,
                        ClipToBounds = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child =
                            Image(
                                Source = bmp,
                                Stretch = Stretch.UniformToFill,
                                Opacity = (if item.Ready then 1.0 else 0.6)
                            )
                    )

                AutomationProperties.SetName(thumb, sprintf "图片预览 %s" item.FileName)
                thumb :> Control
            | _ ->
                let slot =
                    Border(
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 16.0,
                        Height = 16.0,
                        Child = buildIcon item
                    )

                slot :> Control

        let name =
            TextBlock(
                Text = item.FileName,
                FontSize = 11.0,
                MaxWidth = 140.0,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            )

        name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

        let size =
            TextBlock(
                Text = item.StateText,
                FontSize = 10.0,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            )

        if item.Failed then
            size.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        else
            size.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore

        ToolTip.SetTip(size, item.Tip)

        let shrink =
            item.ShrinkNote
            |> Option.map (fun n ->
                let badge =
                    TextBlock(
                        Text = "已缩小",
                        FontSize = 10.0,
                        Margin = Thickness(0.0, 0.0, 4.0, 0.0),
                        VerticalAlignment = VerticalAlignment.Center
                    )

                badge.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Accent") |> ignore
                ToolTip.SetTip(badge, n)
                AutomationProperties.SetName(badge, n)
                AutomationProperties.SetAutomationId(badge, "attachment-shrink-note")
                badge)

        let removeLabel = sprintf "移除附件 %s" item.FileName
        let remove = removeButton removeLabel
        removes.[item.Id] <- remove
        let id = item.Id

        remove.Click.Add(fun _ ->
            onRemove id
            removed.Trigger id)

        let row =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 6.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        row.Children.Add iconSlot
        row.Children.Add name
        shrink |> Option.iter (fun b -> row.Children.Add b)
        row.Children.Add size
        row.Children.Add remove

        let chip =
            Border(
                Tag = item.Id,
                CornerRadius = CornerRadius 6.0,
                BorderThickness = Thickness 1.0,
                Padding = Thickness(6.0, 3.0),
                Child = row
            )

        chip.Classes.Add "grouping-card"

        chip.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer")
        |> ignore

        if item.Failed then
            chip.Bind(Border.BorderBrushProperty, Dyn.res "Palette.HairlineStrong")
            |> ignore
        else
            chip.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore

        if not item.Ready && not item.Failed then
            // 呼吸动效（声明式类名）
            chip.Classes.Add "anim-breathe"

        ToolTip.SetTip(chip, item.Name)
        AutomationProperties.SetName(chip, item.Name)
        chip :> Control

    let applyCompact () =
        scroller.HorizontalScrollBarVisibility <-
            if compact then
                ScrollBarVisibility.Auto
            else
                ScrollBarVisibility.Disabled

        scroller.VerticalScrollBarVisibility <-
            if compact then
                ScrollBarVisibility.Disabled
            else
                ScrollBarVisibility.Auto

        strip.Margin <-
            if compact then
                Thickness(0.0, 0.0, 0.0, 4.0)
            else
                Thickness 0.0

    let renderItems () =
        removes.Clear()
        strip.Children.Clear()

        for item in items do
            strip.Children.Add(build item)

        scroller.IsVisible <- not (List.isEmpty items)

    /// 放进布局的根节点
    member _.Host: Control = scroller :> Control
    member _.Strip = strip
    member _.IsShown = scroller.IsVisible
    member _.Count = items.Length
    member _.Removed = removed.Publish

    member _.OnRemove(f: Guid -> unit) = onRemove <- f

    /// 单入口幂等投影：附件事实与窄屏密度一次给全
    member this.Render(next: TrayItem list, isCompact: bool) =
        items <- next
        compact <- isCompact
        applyCompact ()
        renderItems ()
