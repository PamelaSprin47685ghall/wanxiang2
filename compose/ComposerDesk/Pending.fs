namespace Wanxiang2.ComposerDesk

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang.UI

/// 未确认消息区（旧 Composer.SetPendingMessages）：当前对话的每条未确认消息一张分组卡，
/// 别的对话的按对话给一个「查看」按钮，末尾一句保存提醒。
/// 外观全部走主题动态资源（Palette.* / Metric.* / Space.* / CornerRadius.* / Thickness.* / FontFamily.Body）
/// 与样式类（secondary / button-secondary，尺寸与留白由 ThemeStyles 给），无常驻 IObservable 字段、无手工订阅。
type PendingPanel() =
    let panel = StackPanel()
    do
        panel.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Md") |> ignore
    let scroller =
        ScrollViewer(
            Content = panel,
            MaxHeight = LayoutPolicy.pendingMessagesMaxHeight,
            IsVisible = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = Thickness(0.0, 0.0, LayoutPolicy.nestedScrollGutter, 0.0))
    let host =
        Border(Background = Brushes.Transparent, BorderThickness = Thickness 0.0, Padding = Thickness 0.0, IsVisible = false, Child = scroller)
    do
        host.Bind(Layoutable.MarginProperty, Dyn.res "Thickness.GapBottomMd") |> ignore
    let mutable keys: (Guid * DeliveryState * bool * bool) list = []

    let caption (text: string) =
        let t = TextBlock(Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = ReadingRhythm.captionLineHeight)
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        t.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontCaption") |> ignore
        t.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Body") |> ignore
        t

    let button (label: string) (onClick: unit -> unit) =
        let b = Button(Content = label, Focusable = true, Cursor = new Cursor(StandardCursorType.Hand),
                       HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        AutomationProperties.SetName(b, label)
        b.Click.Add(fun _ -> onClick ())
        b

    member _.Host: Control = host
    member _.IsShown = host.IsVisible

    /// 单入口幂等渲染：内容变了才重排分组卡
    member _.Render(items: PendingMessage list, activeConversationId: Guid option, canRetry: bool, retry: Guid -> unit, openConversation: Guid -> unit) =
        let next = items |> List.map (fun item -> PendingMessage.invocationId item, item.state, canRetry, activeConversationId = Some item.conversationId)
        if next <> keys then
            keys <- next
            panel.Children.Clear()
            let current, elsewhere = items |> List.partition (fun item -> activeConversationId = Some item.conversationId)
            for item in current do
                let body = StackPanel()
                body.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Xs") |> ignore
                if not (String.IsNullOrEmpty item.text) then
                    let text = SelectableTextBlock(Text = item.text, TextWrapping = TextWrapping.Wrap)
                    text.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
                    text.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft") |> ignore
                    text.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontSmall") |> ignore
                    body.Children.Add(
                        ScrollViewer(
                            MaxHeight = LayoutPolicy.pendingMessagePreviewMaxHeight,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Margin = Thickness(0.0, 0.0, LayoutPolicy.nestedScrollGutter, 0.0),
                            Content = text))
                if not (List.isEmpty item.attachments) then
                    body.Children.Add(caption (item.attachments |> List.map _.fileName |> String.concat "、"))
                let status = TextBlock(Text = PendingMessage.status item, TextWrapping = TextWrapping.Wrap)
                status.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                status.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontMicro") |> ignore
                AutomationProperties.SetName(status, PendingMessage.status item)
                let row = DockPanel(LastChildFill = true)
                if PendingMessage.canRetry item then
                    let b = button "重试" (fun () -> retry (PendingMessage.invocationId item))
                    b.IsEnabled <- canRetry
                    DockPanel.SetDock(b, Dock.Right)
                    row.Children.Add b
                row.Children.Add status
                body.Children.Add row
                let card = Border(Child = body)
                card.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer") |> ignore
                card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
                card.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.BorderThin") |> ignore
                card.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusMd") |> ignore
                card.Padding <- Thickness(Spacing.spaceXl, Spacing.spaceMd)
                panel.Children.Add card
            for conversationId, pending in elsewhere |> List.groupBy _.conversationId do
                panel.Children.Add(button (sprintf "查看另一个对话的未确认消息（%d 条）" pending.Length) (fun () -> openConversation conversationId))
            if not (List.isEmpty items) then
                let notice = caption "未确认内容只保留在当前窗口，关闭或刷新前请先确认保存。"
                AutomationProperties.SetName(notice, notice.Text)
                panel.Children.Add notice
            scroller.IsVisible <- not (List.isEmpty items)
            host.IsVisible <- not (List.isEmpty items)
