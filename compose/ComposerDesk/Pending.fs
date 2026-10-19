namespace Wanxiang2.ComposerDesk

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics

/// 未确认消息区的外观：主题管理者的活实例
type PendingAppearance =
    { Brush: PaletteKey -> IBrush
      Metric: Metric -> IObservable<float>
      Space: Space -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      /// 次要按钮样式类（旧 Ui.Secondary）
      DressSecondary: Border -> unit }

/// 可点的文字按钮（旧 Ui.button）：自动化层报按钮并给 Invoke
type private PendingButton(label: string) as this =
    inherit Border()
    let mutable action: unit -> unit = ignore
    do
        this.Child <- TextBlock(Text = label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        this.Focusable <- true
        this.Cursor <- new Cursor(StandardCursorType.Hand)
        AutomationProperties.SetName(this, label)
        this.PointerReleased.Add(fun e ->
            if not e.Handled && this.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if not e.Handled && this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())
    override _.StyleKeyOverride = typeof<Border>
    member _.OnInvoke(f: unit -> unit) = action <- f
    member _.Invoke() = if this.IsEnabled then action ()
    override _.OnCreateAutomationPeer() = PendingButtonPeer(this) :> AutomationPeer

and private PendingButtonPeer(owner: PendingButton) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        // 跨线程 marshal，非焦点竞态（UI Automation 调用方可能来自辅助功能后台线程）
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())

/// 未确认消息区（旧 Composer.SetPendingMessages）：当前对话的每条未确认消息一张分组卡，
/// 别的对话的按对话给一个「查看」按钮，末尾一句保存提醒。
type PendingPanel(look: PendingAppearance) =
    let live (o: IObservable<'T>) (f: 'T -> unit) = o |> Observable.add f
    let panel = StackPanel()
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
    let mutable keys: (Guid * DeliveryState * bool * bool) list = []

    let caption (text: string) =
        let t = TextBlock(Text = text, Foreground = look.Brush PaletteKey.TextMuted, TextWrapping = TextWrapping.Wrap, LineHeight = ReadingRhythm.captionLineHeight)
        live (look.Metric Metric.FontCaption) (fun s -> t.FontSize <- s)
        live look.BodyFont (fun f -> t.FontFamily <- f)
        t

    let button (label: string) (onClick: unit -> unit) =
        let b = PendingButton label
        look.DressSecondary b
        live (look.Metric Metric.TextButtonMinHeight) (fun h -> b.MinHeight <- h)
        b.Padding <- Thickness(Spacing.space2xl, Spacing.spaceSm)
        b.OnInvoke onClick
        b

    do
        live (look.Space Space.Md) (fun s -> panel.Spacing <- s; host.Margin <- Thickness(0.0, 0.0, 0.0, s))

    member _.Host: Control = host
    member _.IsShown = host.IsVisible

    member _.Set(items: PendingMessage list, activeConversationId: Guid option, canRetry: bool, retry: Guid -> unit, openConversation: Guid -> unit) =
        let next = items |> List.map (fun item -> PendingMessage.invocationId item, item.state, canRetry, activeConversationId = Some item.conversationId)
        if next <> keys then
            keys <- next
            panel.Children.Clear()
            let current, elsewhere = items |> List.partition (fun item -> activeConversationId = Some item.conversationId)
            for item in current do
                let body = StackPanel()
                live (look.Space Space.Xs) (fun s -> body.Spacing <- s)
                if not (String.IsNullOrEmpty item.text) then
                    let text = SelectableTextBlock(Text = item.text, TextWrapping = TextWrapping.Wrap, Foreground = look.Brush PaletteKey.Text)
                    live (look.Metric Metric.FontSmall) (fun s -> text.FontSize <- s)
                    body.Children.Add(
                        ScrollViewer(
                            MaxHeight = LayoutPolicy.pendingMessagePreviewMaxHeight,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Margin = Thickness(0.0, 0.0, LayoutPolicy.nestedScrollGutter, 0.0),
                            Content = text))
                if not (List.isEmpty item.attachments) then
                    body.Children.Add(caption (item.attachments |> List.map _.fileName |> String.concat "、"))
                let status = TextBlock(Text = PendingMessage.status item, TextWrapping = TextWrapping.Wrap, Foreground = look.Brush PaletteKey.TextMuted)
                live (look.Metric Metric.FontMicro) (fun s -> status.FontSize <- s)
                AutomationProperties.SetName(status, PendingMessage.status item)
                let row = DockPanel(LastChildFill = true)
                if PendingMessage.canRetry item then
                    let b = button "重试" (fun () -> retry (PendingMessage.invocationId item))
                    b.IsEnabled <- canRetry
                    DockPanel.SetDock(b, Dock.Right)
                    row.Children.Add b
                row.Children.Add status
                body.Children.Add row
                let card = Border(Background = look.Brush PaletteKey.SurfaceContainer, BorderBrush = look.Brush PaletteKey.Hairline, Child = body)
                live (look.Metric Metric.BorderWidth) (fun w -> card.BorderThickness <- Thickness w)
                live (look.Space Space.RadiusMd) (fun r -> card.CornerRadius <- CornerRadius r)
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
