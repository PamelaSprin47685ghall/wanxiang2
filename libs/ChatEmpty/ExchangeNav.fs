namespace Wanxiang2.ExchangeNav

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 阅读导航端点态与「更早一页取失败」行内提示（旧 ChatView 的 canJump*、olderFailureBanner）。
/// 废除 Look 记录，重试按钮改原生 Button 样式类 secondary。
type ExchangeNav() =
    let mutable canPrev = false
    let mutable canNext = false
    let mutable retry: unit -> unit = ignore
    let message =
        TextBlock(TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5)
    do message.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
    let retryButton =
        let b = Button(Content = "重试")
        b.Focusable <- true
        b.Cursor <- new Cursor(StandardCursorType.Hand)
        b.VerticalAlignment <- VerticalAlignment.Center
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        AutomationProperties.SetName(b, "重试加载更早消息")
        b.Click.Add(fun _ -> retry ())
        b
    let banner =
        let row = Grid(ColumnSpacing = 12.0)
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(message, 0)
        Grid.SetColumn(retryButton, 1)
        row.Children.Add message
        row.Children.Add retryButton
        let border =
            Border(
                Name = "older-page-failure",
                Child = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius 12.0,
                Padding = Thickness(16.0, 8.0),
                Margin = Thickness(16.0, 8.0, 16.0, 0.0),
                MaxWidth = 748.0,
                IsVisible = false)
        border.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceRaised") |> ignore
        border.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Warning") |> ignore
        AutomationProperties.SetLiveSetting(message, AutomationLiveSetting.Polite)
        border

    /// 「更早一页失败」横幅（宿主叠在消息区顶部）
    member _.FailureBanner: Control = banner
    /// 「重试」按钮
    member _.RetryButton: Button = retryButton
    member _.CanJumpPrevious = canPrev
    member _.CanJumpNext = canNext
    member _.OlderFailureVisible = banner.IsVisible
    member _.OlderFailureText = message.Text

    /// 菜单与快捷键共用的端点可用态（旧 syncExchangeNavState 的结果）
    member _.SetJumpEnds(previous: bool, next: bool) =
        canPrev <- previous
        canNext <- next

    /// 决策 310：更早一页取失败。挂起的「上一轮」由宿主撤掉；这里只挂提示与重试
    member _.ShowOlderFailure(text: string, onRetry: unit -> unit) =
        retry <- (fun () ->
            banner.IsVisible <- false
            onRetry ())
        message.Text <- text
        AutomationProperties.SetName(message, text)
        banner.IsVisible <- true

    member _.HideOlderFailure() = banner.IsVisible <- false

    /// 换对话时清端点与失败条
    member _.Reset() =
        canPrev <- false
        canNext <- false
        banner.IsVisible <- false
        retry <- ignore
