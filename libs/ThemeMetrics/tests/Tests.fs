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

[<Fact>]
let ``settings surface width is registered on the scale`` () =
    // 第三批：设置页蒙层 surface 宽进刻度（旧硬编码 720）
    Assert.Equal(720.0, ThemeMetrics.initial Metric.SettingsSurfaceWidth)

[<Fact>]
let ``palette and switcher widths stay on their registered values`` () =
    // 刻度已登记的浮层宽：命令面板 560、快速切换模型 300
    Assert.Equal(560.0, ThemeMetrics.initial Metric.PaletteDialogWidth)
    Assert.Equal(300.0, ThemeMetrics.initial Metric.ModelSwitcherWidth)
