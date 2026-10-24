namespace Wanxiang2.AttachmentChip

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

    let glyph =
        let icon = IconSet.control (if data.IsImage then Glyph.Image else Glyph.File) 14.0

        icon.Tag <- if data.IsImage then ChipGlyph.Image else ChipGlyph.File
        icon.VerticalAlignment <- VerticalAlignment.Center
        icon

    do
        glyph.Bind(
            TemplatedControl.ForegroundProperty,
            Dyn.res (
                if data.Missing then
                    "Palette.TextFaint"
                else
                    "Palette.TextMuted"
            )
        )
        |> ignore

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
            let download = IconSet.control Glyph.Download 14.0
            download.Tag <- ChipGlyph.Download
            download.VerticalAlignment <- VerticalAlignment.Center

            download.Bind(TemplatedControl.ForegroundProperty, Dyn.res "Palette.TextFaint")
            |> ignore

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
