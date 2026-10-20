namespace Wanxiang2.ModelChip

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 输入区模型芯片（原生 Button + 矢量箭头实现）：
/// 单入口 Render(modelOption: string option, compact: bool)。
/// 废除 Look 记录与 Spot。
type ModelChip() =
    let caption =
        TextBlock(
            Text = "未选择模型",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis)
    do caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore

    let chevron =
        Path(
            Data = Geometry.Parse "M 3 5 L 7 9 L 11 5",
            StrokeThickness = 1.2,
            StrokeLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 14.0,
            Height = 14.0)
    do chevron.Bind(Path.StrokeProperty, Dyn.res "Palette.TextFaint") |> ignore

    let host =
        let b = Button(Focusable = true, Cursor = new Cursor(StandardCursorType.Hand), VerticalAlignment = VerticalAlignment.Center)
        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b

    let mutable compact = false
    let mutable onOpen: Control -> unit = ignore

    let applyWidth () =
        caption.MaxWidth <- if compact then 120.0 else 240.0
        host.MinHeight <- if compact then 32.0 else 26.0

    do
        let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0, VerticalAlignment = VerticalAlignment.Center)
        row.Children.Add caption
        row.Children.Add chevron
        host.Content <- row
        applyWidth ()
        AutomationProperties.SetName(host, "未选择模型")
        host.Click.Add(fun _ -> onOpen host)

    /// 放进布局的原生 Button 宿主
    member _.Host: Button = host
    member _.Text = caption.Text

    /// 点开时的去处（宿主注入模型切换器，锚点即本芯片）
    member _.OnOpen(f: Control -> unit) = onOpen <- f

    /// 空或「未选择模型」用 muted 色
    member _.ApplyLabel(text: string) =
        let empty = String.IsNullOrWhiteSpace text || text = "未选择模型"
        caption.Text <- if String.IsNullOrWhiteSpace text then "未选择模型" else text
        if empty then
            caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        else
            caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        AutomationProperties.SetName(host, caption.Text)

    /// 单入口幂等渲染
    member this.Render(model: string option, isCompact: bool) =
        compact <- isCompact
        applyWidth ()
        match model with
        | Some label when not (String.IsNullOrWhiteSpace label) -> this.ApplyLabel label
        | _ -> this.ApplyLabel "未选择模型"
