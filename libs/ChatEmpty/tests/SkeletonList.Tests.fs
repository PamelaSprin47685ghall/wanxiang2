module Wanxiang2.SkeletonList.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.SkeletonList
open Wanxiang2.SkeletonList.Tests

[<Fact>]
let ``骨架屏显隐与声明式 anim-breathe 动效挂载`` () =
    Headless.run (fun () ->
        let s = SkeletonList()
        Assert.False s.IsShown
        Assert.False s.IsBreathing
        s.Show()
        Assert.True s.IsShown
        Assert.True s.IsBreathing
        Assert.Contains("anim-breathe", (s.Host :?> StackPanel).Classes)
        let w = Window(Width = 600.0, Height = 400.0, Content = s.Host)
        w.Show()
        Assert.True s.IsBreathing
        s.Hide()
        Assert.False s.IsShown
        Assert.False s.IsBreathing
        Assert.DoesNotContain("anim-breathe", (s.Host :?> StackPanel).Classes)
        w.Close())

// 阅读列宽 / 消息间距 / 尾部留白是主题活键（Metric.ReadingWidth / MessageGap / Margin.MessageEnd）：
// 改键值，已经挂好的骨架跟随（与 ChatPane 的度量回归同一断言面）。
// 骨架阅读列三键（ReadingWidth / MessageGap / Margin.MessageEnd）是主题活键。
// 动态资源绑定在控件挂上可视树后激活，故断言前先挂无头窗口。
[<Fact>]
let ``骨架阅读列跟随主题度量`` () =
    Headless.run (fun () ->
        let s = SkeletonList()
        let panel = s.Host :?> StackPanel

        let w = Window(Content = panel)
        w.Show()
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(748.0, panel.MaxWidth)
        Assert.Equal(24.0, panel.Spacing)
        Assert.Equal(48.0, panel.Margin.Bottom)

        Application.Current.Resources["Metric.ReadingWidth"] <- box 600.0
        Application.Current.Resources["Metric.MessageGap"] <- box 32.0
        Application.Current.Resources["Margin.MessageEnd"] <- box (Thickness(0.0, 0.0, 0.0, 64.0))
        Dispatcher.UIThread.RunJobs()

        Assert.Equal(600.0, panel.MaxWidth)
        Assert.Equal(32.0, panel.Spacing)
        Assert.Equal(64.0, panel.Margin.Bottom)
        w.Close())
