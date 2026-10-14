namespace Wanxiang2.ModelChip

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 用到的笔刷
[<RequireQualifiedAccess>]
type ChipInk =
    | Text
    | TextMuted
    | Hairline
    /// 下拉箭头（旧 Tokens.textFaint）
    | TextFaint

/// 用到的活度量
[<RequireQualifiedAccess>]
type ChipGauge =
    | FontSmall
    | MaxWidth
    | CompactMaxWidth
    | PadX
    | PadY
    | RadiusSm
    | BorderWidth
    | CompactMinHeight
    | SpaceXs

/// 外观
type ModelChipLook =
    { Ink: ChipInk -> IBrush
      Gauge: ChipGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      /// 下拉箭头图标（旧 Icons.chevronDown）
      Chevron: IBrush -> Control
      /// 描边表面反馈（旧 Ui.surfaceBorderedTransitions / Interaction.HoverVariant.bordered）
      Dress: Border -> unit }

/// 输入区模型芯片（旧 Composer.modelChip）：显示当前模型名，点开切换；未选时用 muted 色。
type ModelChip(look: ModelChipLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let caption =
        TextBlock(
            Text = "未选择模型",
            Foreground = ink ChipInk.TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis)
    let host =
        Spot(
            Background = Brushes.Transparent,
            BorderBrush = ink ChipInk.Hairline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            VerticalAlignment = VerticalAlignment.Center)
    let mutable compact = false
    let mutable onOpen: Control -> unit = ignore
    let applyWidth () =
        caption.MaxWidth <- Follow.now (gauge (if compact then ChipGauge.CompactMaxWidth else ChipGauge.MaxWidth))
        host.MinHeight <- if compact then Follow.now (gauge ChipGauge.CompactMinHeight) else 0.0

    do
        let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live row (gauge ChipGauge.SpaceXs) (fun s -> row.Spacing <- s)
        row.Children.Add caption
        let chevron = look.Chevron(ink ChipInk.TextFaint)
        chevron.VerticalAlignment <- VerticalAlignment.Center
        row.Children.Add chevron
        host.Child <- row
        look.Dress host
        Follow.live caption (gauge ChipGauge.FontSmall) (fun s -> caption.FontSize <- s)
        Follow.live caption look.BodyFont (fun f -> caption.FontFamily <- f)
        Follow.live2 host (gauge ChipGauge.PadX) (gauge ChipGauge.PadY) (fun x y -> host.Padding <- Thickness(x, y))
        Follow.live host (gauge ChipGauge.RadiusSm) (fun r -> host.CornerRadius <- CornerRadius r)
        Follow.live host (gauge ChipGauge.BorderWidth) (fun w -> host.BorderThickness <- Thickness w)
        Follow.live host (gauge ChipGauge.MaxWidth) (fun _ -> applyWidth ())
        Follow.live host (gauge ChipGauge.CompactMaxWidth) (fun _ -> applyWidth ())
        Follow.live host (gauge ChipGauge.CompactMinHeight) (fun _ -> applyWidth ())
        AutomationProperties.SetName(host, "未选择模型")
        host.OnInvoke(fun () -> onOpen host)

    /// 放进布局的根
    member _.Host: Border = host
    member _.Text = caption.Text

    /// 点开时的去处（宿主注入模型切换器，锚点即本芯片）
    member _.OnOpen(f: Control -> unit) = onOpen <- f

    member _.SetCompact(value: bool) =
        compact <- value
        applyWidth ()

    /// 空或「未选择模型」用 muted 色
    member _.SetLabel(text: string) =
        let empty = String.IsNullOrWhiteSpace text || text = "未选择模型"
        caption.Text <- if String.IsNullOrWhiteSpace text then "未选择模型" else text
        caption.Foreground <- ink (if empty then ChipInk.TextMuted else ChipInk.Text)
        AutomationProperties.SetName(host, caption.Text)
