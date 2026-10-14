module Wanxiang2.ThemeMetrics.Tests.Tests

open System
open System.IO
open Xunit
open Wanxiang2.ThemeMetrics

/// 金样由照抄的旧 Tokens 数值段与 Metrics.fs 生成
[<Fact>]
let ``golden: every metric matches the old value`` () =
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "thememetrics.txt"))
    Assert.Equal(expected, Render.render [ for m in ThemeMetrics.all -> sprintf "%A" m, ThemeMetrics.initial m ])

[<Fact>]
let ``aliases keep the value of their source`` () =
    Assert.Equal(ThemeMetrics.initial Metric.RowMinHeight, ThemeMetrics.initial Metric.SidebarRowMinHeight)
    Assert.Equal(ThemeMetrics.initial Metric.ChipPaddingX, ThemeMetrics.initial Metric.CompactChipPaddingX)
    Assert.Equal(ThemeMetrics.initial Metric.AccentEdgeWidth, ThemeMetrics.initial Metric.ToastAccentWidth)
