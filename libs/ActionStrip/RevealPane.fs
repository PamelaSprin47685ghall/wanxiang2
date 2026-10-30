namespace Wanxiang2.RevealPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 限高视口（旧 MessageCard.detailViewport）：内容超出 maxHeight 时露出「展开全部 / 收起」幽灵按钮。
type RevealPane(content: Control, maxHeight: float, initiallyVisible: bool) =
    let scroller =
        ScrollViewer(
            Content = content,
            ClipToBounds = true,
            MaxHeight = maxHeight,
            Margin = Thickness(0.0, 0.0, 8.0, 0.0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            IsVisible = initiallyVisible
        )

    let label =
        TextBlock(
            Text = "展开全部",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        )

    let button =
        Button(
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            IsVisible = false,
            Content = label
        )

    let host =
        StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0, IsVisible = initiallyVisible)

    let mutable visible = initiallyVisible
    let mutable full = false

    let setText (text: string) =
        label.Text <- text
        AutomationProperties.SetName(button, text)

    let refresh () =
        let clipped =
            scroller.Viewport.Height > 0.0
            && scroller.Extent.Height > scroller.Viewport.Height + 1.0

        button.IsVisible <- visible && (full || clipped)

    do
        button.Classes.Add "ghost"
        button.Classes.Add "button-ghost"
        AutomationProperties.SetName(button, "展开全部")
        host.Children.Add scroller
        host.Children.Add button

        scroller.LayoutUpdated.Add(fun _ ->
            if visible then
                refresh ())

        scroller.PropertyChanged.Add(fun e ->
            if
                e.Property = ScrollViewer.ExtentProperty
                || e.Property = ScrollViewer.ViewportProperty
            then
                refresh ())

        button.Click.Add(fun _ ->
            full <- not full
            scroller.MaxHeight <- (if full then Double.PositiveInfinity else maxHeight)
            setText (if full then "收起" else "展开全部")
            refresh ())

    /// 放进布局的根
    member _.Host: Control = host
    /// 内部滚动视口（测试与宿主读裁切状态用）
    member _.Scroller = scroller
    /// 「展开全部 / 收起」按钮
    member _.Toggle: Button = button
    member _.IsFull = full
    member _.IsShown = visible

    /// 展开 / 收起整块；收起时顺带退出全文态
    member _.ApplyVisible(value: bool) =
        visible <- value
        scroller.IsVisible <- value
        host.IsVisible <- value

        if not value && full then
            full <- false
            scroller.MaxHeight <- maxHeight
            setText "展开全部"

        refresh ()

    /// 切换全文态（同按下按钮）
    member _.ToggleFull() =
        button.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, button))

    /// 单入口：Render(RevealPaneVS)
    member this.Render(vs: RevealPaneVS) =
        this.ApplyVisible vs.Visible

        if vs.Expanded <> full then
            this.ToggleFull()
