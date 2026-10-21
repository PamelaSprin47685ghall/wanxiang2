namespace Wanxiang2.UserBubble

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

module UserBubble =
    /// 气泡里的正文：可选中，右键不出主题自带的 Copy 浮层
    let text (content: string) (fontSize: float) : Control =
        let tb =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                FontSize = fontSize,
                LineHeight = fontSize * 1.62
            )

        tb.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.UserBubbleText")
        |> ignore

        tb.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.UserBubbleSelection")
        |> ignore

        tb :> Control

/// 用户消息（旧 MessageCard.renderWithStream 的用户分支）：右对齐的深色气泡（右下角收小）+「你」头像。
type UserBubble(body: Control) =
    let bubble =
        Border(
            HorizontalAlignment = HorizontalAlignment.Right,
            Focusable = true,
            BorderThickness = Thickness 1.0,
            CornerRadius = CornerRadius(12.0, 12.0, 6.0, 12.0),
            Padding = Thickness(16.0, 12.0),
            MaxWidth = 560.0,
            Child = body
        )

    let label =
        TextBlock(
            Text = "你",
            FontWeight = FontWeight.Medium,
            FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        )

    let avatar =
        Border(
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Thickness 0.0,
            Width = 26.0,
            Height = 26.0,
            CornerRadius = CornerRadius 999.0,
            Child = label
        )

    let row =
        StackPanel(
            Orientation = Orientation.Horizontal,
            Spacing = 12.0,
            HorizontalAlignment = HorizontalAlignment.Right
        )

    do
        bubble.Bind(Border.BackgroundProperty, Dyn.res "Palette.UserBubble") |> ignore

        bubble.Bind(Border.BorderBrushProperty, Dyn.res "Palette.HairlineStrong")
        |> ignore

        bubble.Classes.Add "surface"
        bubble.Classes.Add "hover-plain"
        AutomationProperties.SetName(bubble, "用户消息")
        avatar.Bind(Border.BackgroundProperty, Dyn.res "Palette.AccentSoft") |> ignore
        label.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Accent") |> ignore
        row.Children.Add bubble
        row.Children.Add avatar

    /// 一行：气泡 + 头像
    member _.Host: Control = row
    member _.Bubble: Border = bubble
    member _.Avatar: Border = avatar

    /// 单入口：从 UserBubbleVS 渲染
    static member Render(vs: UserBubbleVS) : Control =
        let content = UserBubble.text vs.Text vs.FontSize
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        let pane = Wanxiang2.RevealPane.RevealPane(content, 200.0, true)
        stack.Children.Add pane.Host

        for a in vs.Attachments do
            stack.Children.Add(Wanxiang2.AttachmentChip.AttachmentChip.Render a)

        UserBubble(stack).Host
