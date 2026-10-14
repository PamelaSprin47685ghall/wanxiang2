namespace Wanxiang2.ExchangeNav

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 用到的笔刷
[<RequireQualifiedAccess>]
type NavInk =
    | Text
    | SurfaceRaised
    | Warning

/// 用到的活度量
[<RequireQualifiedAccess>]
type NavGauge =
    | SpaceMd
    | SpaceXl
    | Space2xl
    | RadiusLg
    | BorderWidth
    | FontSmall
    | ReadingWidth
    | TextButtonPadY
    | TextButtonMinHeight

/// 外观
type ExchangeNavLook =
    { Ink: NavInk -> IBrush
      Gauge: NavGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      /// 次要按钮样式类（旧 Ui.Secondary）
      DressRetry: Border -> unit
      /// 浮层阴影（旧 Tokens.shadowPopup）
      Shadow: unit -> BoxShadows }

type private TextSpot(label: string) as this =
    inherit Spot()
    let text = TextBlock(Text = label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
    do this.Child <- text
    member _.Label = text

/// 阅读导航端点态与「更早一页取失败」行内提示（旧 ChatView 的 canJump*、olderFailureBanner）。
/// 跳转状态机仍归 ExchangeJump；平滑滚动与挂载窗口扩容由宿主做。重试按钮是本库 Spot。
type ExchangeNav(look: ExchangeNavLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let mutable canPrev = false
    let mutable canNext = false
    let mutable retry: unit -> unit = ignore
    let message =
        TextBlock(Foreground = ink NavInk.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center)
    let retryButton =
        let b = TextSpot "重试"
        b.Focusable <- true
        b.Cursor <- new Cursor(StandardCursorType.Hand)
        b.VerticalAlignment <- VerticalAlignment.Center
        look.DressRetry b
        Follow.live2 b (gauge NavGauge.Space2xl) (gauge NavGauge.TextButtonPadY) (fun x y -> b.Padding <- Thickness(x, y))
        Follow.live b (gauge NavGauge.TextButtonMinHeight) (fun h -> b.MinHeight <- h)
        AutomationProperties.SetName(b, "重试加载更早消息")
        b.OnInvoke(fun () -> retry ())
        b
    let banner =
        let row = Grid()
        Follow.live row (gauge NavGauge.SpaceXl) (fun s -> row.ColumnSpacing <- s)
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(message, 0)
        Grid.SetColumn(retryButton, 1)
        row.Children.Add message
        row.Children.Add retryButton
        let border =
            Border(
                Name = "older-page-failure",
                Background = ink NavInk.SurfaceRaised,
                BorderBrush = ink NavInk.Warning,
                Child = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                IsVisible = false)
        border.BoxShadow <- look.Shadow ()
        Follow.live border (gauge NavGauge.BorderWidth) (fun w -> border.BorderThickness <- Thickness w)
        Follow.live border (gauge NavGauge.RadiusLg) (fun r -> border.CornerRadius <- CornerRadius r)
        Follow.live2 border (gauge NavGauge.Space2xl) (gauge NavGauge.SpaceMd) (fun x y -> border.Padding <- Thickness(x, y))
        Follow.live2 border (gauge NavGauge.Space2xl) (gauge NavGauge.SpaceMd) (fun x y -> border.Margin <- Thickness(x, y, x, 0.0))
        Follow.live border (gauge NavGauge.ReadingWidth) (fun w -> border.MaxWidth <- w)
        Follow.live message (gauge NavGauge.FontSmall) (fun s -> message.FontSize <- s)
        Follow.live message look.BodyFont (fun f -> message.FontFamily <- f)
        AutomationProperties.SetLiveSetting(message, AutomationLiveSetting.Polite)
        border

    /// 「更早一页失败」横幅（宿主叠在消息区顶部）
    member _.FailureBanner: Control = banner
    /// 「重试」按钮（测试与读屏）
    member _.RetryButton: Border = retryButton
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
