namespace Wanxiang2.ThinkingBlock

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

/// 用到的笔刷
[<RequireQualifiedAccess>]
type ThinkingInk =
    /// 分组卡底色（旧 surfaceContainer）
    | Surface
    /// 分组卡最轻描边（旧 hairline）
    | Hairline
    /// 左缘竖线（旧 hairlineStrong）
    | Edge
    /// 正文颜色（旧 textMuted）
    | Body
    /// 选区底色（旧 accentSoft）
    | Selection

/// 用到的活度量
[<RequireQualifiedAccess>]
type ThinkingGauge =
    | BorderWidth
    /// 左缘竖线宽（旧 accentEdgeWidth）
    | EdgeWidth
    /// 旧 spaceXs：卡片上、左、下内边距
    | PadTight
    /// 旧 spaceMd：卡片右内边距、竖线右侧留白、正文与头部间距
    | PadLoose
    /// 旧 spaceXl：卡片下外边距
    | After
    /// 旧 Radius.md
    | Radius

type ThinkingBlockLook =
    { Ink: ThinkingInk -> IBrush
      Gauge: ThinkingGauge -> IObservable<float>
      /// 正文行高随字号（旧 ReadingRhythm.secondaryLineHeight）
      LineHeight: float -> float }

/// 思考过程卡（旧 MessageCard.reasoningBlock 的外框与正文）：分组卡 + 左缘竖线，
/// 头部与限高视口由组装者放进来；正文字号比阅读字号小 1.5。
type ThinkingBlock(look: ThinkingBlockLook, header: Control, body: Control) =
    let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let edge = Border(BorderBrush = look.Ink ThinkingInk.Edge, Child = stack)
    let card =
        Border(
            Background = look.Ink ThinkingInk.Surface,
            BorderBrush = look.Ink ThinkingInk.Hairline,
            ClipToBounds = true,
            Child = edge)
    do
        stack.Children.Add header
        stack.Children.Add body
        Follow.live2 edge (look.Gauge ThinkingGauge.EdgeWidth) (look.Gauge ThinkingGauge.PadLoose) (fun w p ->
            edge.BorderThickness <- Thickness(w, 0.0, 0.0, 0.0)
            edge.Padding <- Thickness(p, 0.0, 0.0, 0.0))
        Follow.live body (look.Gauge ThinkingGauge.PadLoose) (fun p ->
            body.Margin <- Thickness(0.0, p, 0.0, 0.0))
        Follow.live3 card (look.Gauge ThinkingGauge.PadTight) (look.Gauge ThinkingGauge.PadLoose) (look.Gauge ThinkingGauge.After) (fun t l a ->
            card.Padding <- Thickness(t, t, l, t)
            card.Margin <- Thickness(0.0, 0.0, 0.0, a))
        Follow.live2 card (look.Gauge ThinkingGauge.BorderWidth) (look.Gauge ThinkingGauge.Radius) (fun w r ->
            card.BorderThickness <- Thickness w
            card.CornerRadius <- CornerRadius r)

    member _.Host: Control = card

/// 思考正文
module ThinkingBlock =
    /// 可选中的思考正文（右键不出主题自带的英文 Copy 浮层，让消息菜单出得来）
    let text (look: ThinkingBlockLook) (reasoning: string) (fontSize: float) : Control =
        let size = fontSize - 1.5
        SelectableTextBlock(
            ContextFlyout = null,
            Text = reasoning,
            TextWrapping = TextWrapping.Wrap,
            FontSize = size,
            Foreground = look.Ink ThinkingInk.Body,
            LineHeight = look.LineHeight size,
            SelectionBrush = look.Ink ThinkingInk.Selection)

    /// 旧标题：流式时「正在思考」否则「已深度思考」，后接开合提示
    let title (streaming: bool) (expanded: bool) =
        (if streaming then "正在思考" else "已深度思考") + (if expanded then "（收起）" else "（展开）")
