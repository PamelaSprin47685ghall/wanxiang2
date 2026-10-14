namespace Wanxiang2.UserBubble

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

[<RequireQualifiedAccess>]
type BubbleInk =
    /// 气泡底（旧 userBubble）
    | Fill
    /// 气泡细描边（旧 hairlineStrong）
    | Edge
    /// 气泡文字（旧 userBubbleText）
    | Text
    /// 气泡里的选区（旧 userBubbleSelection）
    | Selection
    /// 头像底（旧 accentSoft）
    | AvatarFill
    /// 头像字（旧 accent）
    | AvatarText

[<RequireQualifiedAccess>]
type BubbleGauge =
    /// 旧 Stroke.thin
    | Stroke
    | RadiusLg
    | RadiusSm
    | RadiusPill
    /// 旧 space2xl / spaceXl：气泡内边距
    | PadX
    | PadY
    /// 旧 userMessageMaxWidth
    | MaxWidth
    /// 旧 logoAvatar
    | Avatar
    /// 头像字号（旧 fontMicro）
    | AvatarFont
    /// 气泡与头像间距（旧 spaceXl）
    | Gap

type UserBubbleLook =
    { Ink: BubbleInk -> IBrush
      Gauge: BubbleGauge -> IObservable<float>
      /// 挂可聚焦表面的状态反馈（焦点环只走外阴影，不改几何；旧 Interaction.surface plain）
      Dress: Border -> unit
      /// 正文行高随字号（旧 ReadingRhythm.proseLineHeight）
      LineHeight: float -> float }

/// 用户消息（旧 MessageCard.renderWithStream 的用户分支）：右对齐的深色气泡（右下角收小）+「你」头像。
/// 气泡内容由组装者放进来（通常是限高视口里的正文、附件）。
type UserBubble(look: UserBubbleLook, body: Control) =
    let bubble =
        Border(
            Background = look.Ink BubbleInk.Fill,
            BorderBrush = look.Ink BubbleInk.Edge,
            HorizontalAlignment = HorizontalAlignment.Right,
            Focusable = true,
            Child = body)
    let label =
        TextBlock(
            Text = "你",
            FontWeight = FontWeight.Medium,
            Foreground = look.Ink BubbleInk.AvatarText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center)
    let avatar =
        Border(Background = look.Ink BubbleInk.AvatarFill, VerticalAlignment = VerticalAlignment.Top, Margin = Thickness 0.0, Child = label)
    let row = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right)
    do
        Follow.live bubble (look.Gauge BubbleGauge.Stroke) (fun s -> bubble.BorderThickness <- Thickness s)
        Follow.live2 bubble (look.Gauge BubbleGauge.RadiusLg) (look.Gauge BubbleGauge.RadiusSm) (fun lg sm ->
            bubble.CornerRadius <- CornerRadius(lg, lg, sm, lg))
        Follow.live3 bubble (look.Gauge BubbleGauge.PadX) (look.Gauge BubbleGauge.PadY) (look.Gauge BubbleGauge.MaxWidth) (fun x y w ->
            bubble.Padding <- Thickness(x, y)
            bubble.MaxWidth <- w)
        AutomationProperties.SetName(bubble, "用户消息")
        look.Dress bubble
        Follow.live2 avatar (look.Gauge BubbleGauge.Avatar) (look.Gauge BubbleGauge.RadiusPill) (fun a r ->
            avatar.Width <- a
            avatar.Height <- a
            avatar.CornerRadius <- CornerRadius r)
        Follow.live label (look.Gauge BubbleGauge.AvatarFont) (fun s -> label.FontSize <- s)
        Follow.live row (look.Gauge BubbleGauge.Gap) (fun g -> row.Spacing <- g)
        row.Children.Add bubble
        row.Children.Add avatar

    /// 一行：气泡 + 头像
    member _.Host: Control = row
    member _.Bubble: Border = bubble
    member _.Avatar: Border = avatar

module UserBubble =
    /// 气泡里的正文：可选中，右键不出主题自带的 Copy 浮层
    let text (look: UserBubbleLook) (content: string) (fontSize: float) : Control =
        SelectableTextBlock(
            ContextFlyout = null,
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            Foreground = look.Ink BubbleInk.Text,
            LineHeight = look.LineHeight fontSize,
            SelectionBrush = look.Ink BubbleInk.Selection)
