namespace Wanxiang2.AttachmentChip

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

[<RequireQualifiedAccess>]
type ChipGlyph =
    | Image
    | File
    | Download

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 一个附件的显示数据
type ChipData = Wanxiang2.WorkspaceState.AttachmentChipVS

/// 消息里的附件条（旧 MessageCard.attachmentRow）：图标、文件名、大小与下载图标；
/// 内容丢失时变灰、不可点，但仍有提示与读屏名。
type AttachmentChip(data: ChipData) =
    let invoked = Event<unit>()

    let glyphPath =
        if data.IsImage then
            "M 2 2 L 12 2 L 12 12 L 2 12 Z M 3 10 L 6 6 L 9 9 L 11 7 L 12 8"
        else
            "M 3 2 L 9 2 L 12 5 L 12 13 L 3 13 Z M 9 2 L 9 5 L 12 5"

    let glyph =
        Path(
            Data = Geometry.Parse glyphPath,
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent,
            Tag = (if data.IsImage then ChipGlyph.Image else ChipGlyph.File),
            Width = 14.0,
            Height = 14.0,
            VerticalAlignment = VerticalAlignment.Center
        )

    do
        if data.Missing then
            glyph.Bind(Path.StrokeProperty, Dyn.res "Palette.TextFaint") |> ignore
        else
            glyph.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore

    let name =
        TextBlock(
            Text = data.FileName,
            FontSize = 13.0,
            MaxWidth = 260.0,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        )

    do
        if data.Missing then
            name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
        else
            name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

    let meta =
        TextBlock(
            Text = (if data.Missing then "内容已丢失" else data.SizeText),
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center
        )

    do meta.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore

    let row =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, VerticalAlignment = VerticalAlignment.Center)

    let host =
        if not data.Missing then
            let b =
                Button(
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = Thickness(0.0, 4.0, 0.0, 0.0),
                    Padding = Thickness(12.0, 6.0),
                    BorderThickness = Thickness 1.0,
                    CornerRadius = CornerRadius 6.0
                )

            b :> Control
        else
            Border(
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = Thickness(0.0, 4.0, 0.0, 0.0),
                Padding = Thickness(12.0, 6.0),
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius 6.0
            )
            :> Control

    do
        host.Bind(Border.BackgroundProperty, Dyn.res "Palette.Surface") |> ignore
        host.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Border") |> ignore
        row.Children.Add glyph
        row.Children.Add name
        row.Children.Add meta

        if not data.Missing then
            let download =
                Path(
                    Data = Geometry.Parse "M 7 2 L 7 10 M 4 7 L 7 10 L 10 7 M 2 12 L 12 12",
                    StrokeThickness = 1.0,
                    Fill = Brushes.Transparent,
                    Tag = ChipGlyph.Download,
                    Width = 14.0,
                    Height = 14.0,
                    VerticalAlignment = VerticalAlignment.Center
                )

            download.Bind(Path.StrokeProperty, Dyn.res "Palette.TextFaint") |> ignore
            row.Children.Add download

        match host with
        | :? Button as b -> b.Content <- row
        | :? Border as b -> b.Child <- row
        | _ -> ()

        let tip =
            if data.Missing then
                sprintf "附件「%s」内容已丢失" data.FileName
            else
                (if data.IsImage then "查看或下载图片" else "下载附件")

        ToolTip.SetTip(host, tip)

        AutomationProperties.SetName(
            host,
            (if data.Missing then
                 tip
             else
                 sprintf "%s %s" tip data.FileName)
        )

        if not data.Missing then
            host.Classes.Add "hover-raised"

            match host with
            | :? Button as b -> b.Click.Add(fun _ -> invoked.Trigger())
            | _ -> ()

    member _.Host: Control = host
    /// 点下（未丢失时）：宿主据此下载或预览
    member _.Invoked = invoked.Publish

    /// 按一下（测试与键盘）
    member _.Invoke() =
        if not data.Missing then
            invoked.Trigger()

    /// 单入口：从 AttachmentChipVS 渲染
    static member Render(vs: AttachmentChipVS, ?onInvoke: unit -> unit) : Control =
        let c = AttachmentChip(vs)
        onInvoke |> Option.iter (fun f -> c.Invoked.Add(fun () -> f ()))
        c.Host
