namespace Wanxiang2.ActionStrip

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

[<RequireQualifiedAccess>]
type StripGauge =
    /// 按钮间距（旧 spaceXs）
    | Gap
    /// 方形命中区边长，也是整行最小高（旧 inlineActionTarget）
    | Target
    /// 淡显档不透明度（旧 opacitySubtle）
    | Subtle
    /// 显隐补间时长，毫秒（旧 controlRowFade）
    | FadeMs

[<RequireQualifiedAccess>]
type StripGlyph =
    | Copy
    | Check
    | Pencil
    | Refresh
    | Trash

type ActionStripLook =
    { Gauge: StripGauge -> IObservable<float>
      Glyph: StripGlyph -> IBrush -> Control
      /// 图标的默认笔刷（旧 textMuted）
      Muted: IBrush
      /// 给按钮挂图标按钮样式类
      Dress: Border -> unit
      Reduced: IObservable<bool> }

/// 闲置时的显示档
[<RequireQualifiedAccess>]
type StripRest =
    /// 完全隐去（仍占位、可命中、可聚焦，只改不透明度）
    | Hidden
    /// 淡显（最新一条助手回复用作可发现锚点）
    | Subtle

/// 消息脚注里的操作条（旧 MessageCard.actionButtons）：按钮随文档流排布，整行恒定占一个命中区高，
/// 流式期间撤掉按钮也不缩；显隐只改不透明度，不动几何。
type ActionStrip(look: ActionStripLook, rest: StripRest) =
    let fade = DoubleTransition(Property = Visual.OpacityProperty)
    let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let mutable shown = false
    let mutable subtle = 0.0
    let apply () =
        row.Opacity <-
            if shown then 1.0
            else
                match rest with
                | StripRest.Hidden -> 0.0
                | StripRest.Subtle -> subtle
    let square (b: Border) (t: float) =
        b.Width <- t
        b.Height <- t
        b.MinWidth <- t
        b.MinHeight <- t
    do
        let transitions = Transitions()
        transitions.Add fade
        row.Transitions <- transitions
        Follow.live2 row (look.Gauge StripGauge.FadeMs) look.Reduced (fun ms reduced ->
            fade.Duration <- (if reduced then TimeSpan.Zero else TimeSpan.FromMilliseconds ms))
        Follow.live2 row (look.Gauge StripGauge.Gap) (look.Gauge StripGauge.Target) (fun g t ->
            row.Spacing <- g
            row.MinHeight <- t)
        Follow.live row (look.Gauge StripGauge.Subtle) (fun s ->
            subtle <- s
            apply ())

    member _.Host: Control = row
    member _.Shown = shown
    /// 悬停、聚焦或触屏选中时显示，否则回到闲置档
    member _.SetShown(value: bool) =
        shown <- value
        apply ()

    /// 换按钮的图标（复制确认时换成对勾）
    member _.SetIcon(button: Border, glyph: StripGlyph, brush: IBrush) =
        let g = look.Glyph glyph brush
        // 字形纯垂直居中，与脚注文本基线对齐
        g.HorizontalAlignment <- HorizontalAlignment.Center
        g.VerticalAlignment <- VerticalAlignment.Center
        g.Margin <- Thickness 0.0
        button.Child <- g

    /// 加一个图标按钮：tip 是悬停提示，name / help 是读屏名与说明
    member this.Add(glyph: StripGlyph, tip: string, name: string, help: string, action: unit -> unit) : Border =
        let button =
            Spot(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = Thickness 0.0,
                Padding = Thickness 0.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center)
        look.Dress button
        Follow.live button (look.Gauge StripGauge.Target) (square button)
        this.SetIcon(button, glyph, look.Muted)
        ToolTip.SetTip(button, tip)
        AutomationProperties.SetName(button, name)
        AutomationProperties.SetHelpText(button, help)
        button.OnInvoke action
        row.Children.Add button
        button :> Border

    /// 已加的按钮
    member _.Buttons = row.Children |> Seq.cast<Border> |> List.ofSeq

module ActionStrip =
    /// 复制按钮的读屏名带角色与序号，同屏多个「复制」不撞名
    let copyName (isUser: bool) (index: int option) =
        let role = if isUser then "用户" else "助手"
        match index with
        | Some i when i >= 0 -> sprintf "复制%s消息 %d" role (i + 1)
        | _ -> sprintf "复制%s消息" role
