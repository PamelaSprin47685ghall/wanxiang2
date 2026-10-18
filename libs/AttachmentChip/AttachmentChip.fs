namespace Wanxiang2.AttachmentChip

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

[<RequireQualifiedAccess>]
type ChipInk =
    | Surface
    | Border
    | Text
    | TextMuted
    | TextFaint

[<RequireQualifiedAccess>]
type ChipGauge =
    | BorderWidth
    /// 旧 Radius.md
    | Radius
    /// 旧 spaceXl
    | PadX
    /// 旧 attachmentRowPaddingY
    | PadY
    /// 旧 spaceMd：行内间距
    | Gap
    /// 旧 spaceXs：上外边距
    | Top
    /// 文件名字号（旧 fontSmall）
    | NameSize
    /// 大小 / 丢失说明字号（旧 fontMicro）
    | MetaSize
    /// 旧 attachmentNameMaxWidth
    | NameMaxWidth

[<RequireQualifiedAccess>]
type ChipGlyph =
    | Image
    | File
    | Download

type AttachmentChipLook =
    { Ink: ChipInk -> IBrush
      Gauge: ChipGauge -> IObservable<float>
      Glyph: ChipGlyph -> IBrush -> Control
      /// 可点时挂表面状态反馈（旧 Interaction.surface raised）
      Dress: Border -> unit }

/// 一个附件的显示数据
type ChipData =
    { FileName: string
      /// 已格式化的大小（旧 AttachmentRef.formatSize）
      SizeText: string
      IsImage: bool
      /// 内容已丢失：退化为静态条
      Missing: bool }

/// 消息里的附件条（旧 MessageCard.attachmentRow）：图标、文件名、大小与下载图标；
/// 内容丢失时变灰、不可点，但仍有提示与读屏名。
type AttachmentChip(look: AttachmentChipLook, data: ChipData) =
    let invoked = Event<unit>()
    let faint = look.Ink ChipInk.TextFaint
    let glyph = look.Glyph (if data.IsImage then ChipGlyph.Image else ChipGlyph.File) (if data.Missing then faint else look.Ink ChipInk.TextMuted)
    let name =
        TextBlock(
            Text = data.FileName,
            Foreground = (if data.Missing then faint else look.Ink ChipInk.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center)
    let meta =
        TextBlock(
            Text = (if data.Missing then "内容已丢失" else data.SizeText),
            Foreground = faint,
            VerticalAlignment = VerticalAlignment.Center)
    let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let host: Border =
        if data.Missing then Border() else Spot(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true) :> Border
    do
        glyph.VerticalAlignment <- VerticalAlignment.Center
        row.Children.Add glyph
        row.Children.Add name
        row.Children.Add meta
        if not data.Missing then
            let download = look.Glyph ChipGlyph.Download faint
            download.VerticalAlignment <- VerticalAlignment.Center
            row.Children.Add download
        Follow.live row (look.Gauge ChipGauge.Gap) (fun g -> row.Spacing <- g)
        Follow.live2 name (look.Gauge ChipGauge.NameSize) (look.Gauge ChipGauge.NameMaxWidth) (fun s w ->
            name.FontSize <- s
            name.MaxWidth <- w)
        Follow.live meta (look.Gauge ChipGauge.MetaSize) (fun s -> meta.FontSize <- s)
        host.Background <- look.Ink ChipInk.Surface
        host.BorderBrush <- look.Ink ChipInk.Border
        host.HorizontalAlignment <- HorizontalAlignment.Left
        host.Child <- row
        Follow.live3 host (look.Gauge ChipGauge.PadX) (look.Gauge ChipGauge.PadY) (look.Gauge ChipGauge.Top) (fun x y t ->
            host.Padding <- Thickness(x, y)
            host.Margin <- Thickness(0.0, t, 0.0, 0.0))
        Follow.live2 host (look.Gauge ChipGauge.BorderWidth) (look.Gauge ChipGauge.Radius) (fun w r ->
            host.BorderThickness <- Thickness w
            host.CornerRadius <- CornerRadius r)
        let tip =
            if data.Missing then sprintf "附件「%s」内容已丢失" data.FileName
            // 图片的意图是「查看或下载」，与普通文件的「下载」分开说
            else (if data.IsImage then "查看或下载图片" else "下载附件")
        ToolTip.SetTip(host, tip)
        AutomationProperties.SetName(host, (if data.Missing then tip else sprintf "%s %s" tip data.FileName))
        match host with
        | :? Spot as spot ->
            look.Dress host
            spot.OnInvoke invoked.Trigger
        | _ -> ()

    member _.Host: Control = host
    /// 点下（未丢失时）：宿主据此下载或预览
    member _.Invoked = invoked.Publish
    /// 按一下（测试与键盘）
    member _.Invoke() =
        match host with
        | :? Spot as spot -> spot.Invoke()
        | _ -> ()
