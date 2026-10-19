namespace Wanxiang2.SkeletonList

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 用到的笔刷
[<RequireQualifiedAccess>]
type SkeletonInk =
    /// 占位条、头像、气泡底（旧 Tokens.borderSoft）
    | Bar
    /// 用户气泡细描边（旧 Tokens.hairlineStrong）
    | Edge

/// 用到的活度量
[<RequireQualifiedAccess>]
type SkeletonGauge =
    /// 骨架之间的竖向间距（旧 ContentMetrics.messageGap）
    | Gap
    /// 列表底部留白（旧 ContentMetrics.messageEndBreathing）
    | EndBreathing
    /// 阅读列宽（旧 Tokens.readingWidth）
    | ReadingWidth
    | AvatarSize
    | RadiusSm
    | RadiusLg
    | RadiusPill
    | SpaceMd
    | SpaceXl
    | BorderWidth
    /// 正文条透明度（旧 skeletonOpacityBase）
    | OpacityBase
    /// 用户骨架透明度（旧 skeletonOpacityBreathMin）
    | OpacityBreathMin
    /// 减弱动效或未挂载时整块的静态透明度（旧 skeletonOpacityReduced）
    | OpacityReduced

/// 外观：笔刷是活实例；度量、帧间隔、减弱动效都是活值
type SkeletonListLook =
    { Ink: SkeletonInk -> IBrush
      Gauge: SkeletonGauge -> IObservable<float>
      /// 呼吸帧间隔（旧 MotionLedger.skeletonBreathFrame）
      Frame: IObservable<TimeSpan>
      Reduced: IObservable<bool> }

/// 呼吸曲线由使用方给（相位记账在别处，如 BreathPhase）：Begin 相位归零，Step 走一拍返回透明度
type SkeletonBreath =
    { Begin: unit -> unit
      Step: unit -> float }

/// 历史加载骨架（旧 ChatView.EnsureSkeletonBuilt 与骨架呼吸）：助手 → 用户 → 助手三块占位。
/// 显示且挂在树上、未减弱动效时按帧呼吸整块透明度；计时器属于控件自己。
type SkeletonList(look: SkeletonListLook, breath: SkeletonBreath) =
    let gauge g = look.Gauge g
    let panel =
        StackPanel(Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false)
    let timer = DispatcherTimer()
    let mutable attached = false
    let mutable reduced = false
    let mutable still = 0.85

    let textBar (height: float) (fraction: float) =
        let bar = Border(Height = height, Background = look.Ink SkeletonInk.Bar, HorizontalAlignment = HorizontalAlignment.Stretch)
        Follow.live bar (gauge SkeletonGauge.RadiusSm) (fun r -> bar.CornerRadius <- CornerRadius r)
        Follow.live bar (gauge SkeletonGauge.OpacityBase) (fun o -> bar.Opacity <- o)
        let grid = Grid(HorizontalAlignment = HorizontalAlignment.Stretch)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(fraction, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(max 0.01 (1.0 - fraction), GridUnitType.Star)))
        Grid.SetColumn(bar, 0)
        grid.Children.Add bar
        grid :> Control

    let avatar (opacity: SkeletonGauge) (gap: bool) =
        let a = Border(Background = look.Ink SkeletonInk.Bar, VerticalAlignment = VerticalAlignment.Top)
        Follow.live a (gauge SkeletonGauge.AvatarSize) (fun s -> a.Width <- s; a.Height <- s)
        Follow.live a (gauge SkeletonGauge.RadiusPill) (fun r -> a.CornerRadius <- CornerRadius r)
        Follow.live a (gauge opacity) (fun o -> a.Opacity <- o)
        if gap then Follow.live a (gauge SkeletonGauge.SpaceXl) (fun s -> a.Margin <- Thickness(0.0, 0.0, s, 0.0))
        else a.Margin <- Thickness 0.0
        a

    let assistant (lines: (float * float) list) =
        let a = avatar SkeletonGauge.OpacityReduced true
        let column = StackPanel(Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch)
        Follow.live column (gauge SkeletonGauge.SpaceMd) (fun s -> column.Spacing <- s)
        for h, w in lines do column.Children.Add(textBar h w)
        let dock = DockPanel(LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Stretch)
        DockPanel.SetDock(a, Dock.Left)
        dock.Children.Add a
        dock.Children.Add column
        AutomationProperties.SetName(dock, "正在加载助手消息")
        dock :> Control

    let user () =
        // 与真实用户气泡同一 chrome：细描边，换成真卡时轮廓不变。气泡尺寸是占位形状，不是度量
        let bubble =
            Border(
                Background = look.Ink SkeletonInk.Bar,
                BorderBrush = look.Ink SkeletonInk.Edge,
                Width = 260.0,
                Height = 40.0,
                HorizontalAlignment = HorizontalAlignment.Right)
        Follow.live bubble (gauge SkeletonGauge.BorderWidth) (fun w -> bubble.BorderThickness <- Thickness w)
        Follow.live2 bubble (gauge SkeletonGauge.RadiusLg) (gauge SkeletonGauge.RadiusSm) (fun lg sm ->
            bubble.CornerRadius <- CornerRadius(lg, lg, sm, lg))
        Follow.live bubble (gauge SkeletonGauge.OpacityBreathMin) (fun o -> bubble.Opacity <- o)
        let row = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right)
        Follow.live row (gauge SkeletonGauge.SpaceXl) (fun s -> row.Spacing <- s)
        row.Children.Add bubble
        row.Children.Add(avatar SkeletonGauge.OpacityBreathMin false)
        AutomationProperties.SetName(row, "正在加载用户消息")
        row :> Control

    let stop () =
        timer.Stop()
        panel.Opacity <- if panel.IsVisible && reduced then still else 1.0

    let start () =
        timer.Stop()
        if reduced || not attached then panel.Opacity <- still
        else
            breath.Begin()
            timer.Start()

    /// 旧 RefreshSkeletonMotion：只在显示时动
    let refresh () =
        if panel.IsVisible then
            if reduced then
                stop ()
                panel.Opacity <- still
            elif attached then start ()

    do
        AutomationProperties.SetName(panel, "正在加载历史消息")
        Follow.live panel (gauge SkeletonGauge.Gap) (fun g -> panel.Spacing <- g)
        Follow.live panel (gauge SkeletonGauge.EndBreathing) (fun b -> panel.Margin <- Thickness(0.0, 0.0, 0.0, b))
        Follow.live panel (gauge SkeletonGauge.ReadingWidth) (fun w -> panel.MaxWidth <- w)
        Follow.live panel (gauge SkeletonGauge.OpacityReduced) (fun o -> still <- o)
        Follow.live panel look.Frame (fun f -> timer.Interval <- f)
        Follow.live panel look.Reduced (fun r ->
            let changed = r <> reduced
            reduced <- r
            if changed && attached then refresh ())
        timer.Tick.Add(fun _ -> panel.Opacity <- breath.Step())
        panel.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            refresh ())
        panel.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            stop ()
            if panel.IsVisible then panel.Opacity <- still)
        panel.Children.Add(assistant [ (14.0, 0.45); (13.0, 0.85); (13.0, 0.70); (13.0, 0.35) ])
        panel.Children.Add(user ())
        panel.Children.Add(assistant [ (14.0, 0.60); (13.0, 0.90); (13.0, 0.75); (13.0, 0.50) ])

    /// 放进布局的根（宿主把它设为滚动区内容）
    member _.Host: Control = panel
    member _.IsShown = panel.IsVisible
    member _.IsBreathing = timer.IsEnabled
    /// 显示并开始呼吸（旧 ShowSkeletonLoading 的骨架部分）
    member _.Show() =
        panel.IsVisible <- true
        start ()
    /// 停止呼吸并隐藏（旧 HideSkeletonLoading 的骨架部分）
    member _.Hide() =
        if panel.IsVisible then
            stop ()
            panel.IsVisible <- false
    /// 走一拍（测试不等真计时器）
    member _.Step() = if timer.IsEnabled then panel.Opacity <- breath.Step()
