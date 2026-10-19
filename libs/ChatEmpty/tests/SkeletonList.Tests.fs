module Wanxiang2.SkeletonList.Tests.Tests

open System
open Avalonia.Controls
open Avalonia.Media
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
