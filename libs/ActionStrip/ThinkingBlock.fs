namespace Wanxiang2.ThinkingBlock

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 思考正文
module ThinkingBlock =
    /// 可选中的思考正文（右键不出主题自带的英文 Copy 浮层，让消息菜单出得来）
    let text (reasoning: string) (fontSize: float) : Control =
        let size = fontSize - 1.5
        let tb =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = reasoning,
                TextWrapping = TextWrapping.Wrap,
                FontSize = size,
                LineHeight = size * 1.56)
        tb.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        tb.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft") |> ignore
        tb :> Control

    /// 旧标题：流式时「正在思考」否则「已深度思考」，后接开合提示
    let title (streaming: bool) (expanded: bool) =
        (if streaming then "正在思考" else "已深度思考") + (if expanded then "（收起）" else "（展开）")

/// 思考过程卡（旧 MessageCard.reasoningBlock 的外框与正文）：分组卡 + 左缘竖线，
/// 头部与限高视口由组装者放进来；正文字号比阅读字号小 1.5。
type ThinkingBlock(header: Control, body: Control) =
    let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let edge =
        Border(
            BorderThickness = Thickness(2.0, 0.0, 0.0, 0.0),
            Padding = Thickness(8.0, 0.0, 0.0, 0.0),
            Child = stack)
    let card =
        Border(
            ClipToBounds = true,
            Padding = Thickness(4.0, 4.0, 8.0, 4.0),
            Margin = Thickness(0.0, 0.0, 0.0, 16.0),
            BorderThickness = Thickness 1.0,
            CornerRadius = CornerRadius 8.0,
            Child = edge)
    do
        card.Classes.Add "grouping-card"
        card.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer") |> ignore
        card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
        edge.Bind(Border.BorderBrushProperty, Dyn.res "Palette.HairlineStrong") |> ignore
        body.Margin <- Thickness(0.0, 4.0, 0.0, 0.0)
        stack.Children.Add header
        stack.Children.Add body

    member _.Host: Control = card

    /// 单入口：从 ThinkingBlockVS 渲染
    static member Render(vs: ThinkingBlockVS, ?_onCopy: unit -> unit) : Control =
        let fold = Wanxiang2.FoldHeader.FoldHeader(vs.Header.Expanded)
        fold.Render vs.Header
        let textControl = ThinkingBlock.text vs.Body vs.FontSize
        let pane = Wanxiang2.RevealPane.RevealPane(textControl, 200.0, vs.Header.Expanded)
        fold.Toggled.Add(fun open' -> pane.ApplyVisible open')
        ThinkingBlock(fold.Header, pane.Host).Host

