module Wanxiang2.ThemeMetrics.Tests.Tests

open System
open System.IO
open Xunit
open Wanxiang2.ThemeMetrics

[<Fact>]
let ``aliases keep the value of their source`` () =
    Assert.Equal(ThemeMetrics.initial Metric.RowMinHeight, ThemeMetrics.initial Metric.SidebarRowMinHeight)
    Assert.Equal(ThemeMetrics.initial Metric.ChipPaddingX, ThemeMetrics.initial Metric.CompactChipPaddingX)
    Assert.Equal(ThemeMetrics.initial Metric.AccentEdgeWidth, ThemeMetrics.initial Metric.ToastAccentWidth)
