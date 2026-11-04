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

[<Fact>]
let ``退场的阅读字号与输入区冗余死档不再回到刻度上`` () =
    // 阅读列曾有过一个死档（Metric.FontReading，14.5），以及一批在原生化重构后
    // 从未消费、数值与现行控件已偏离或重复的输入区/附件冗余死档。
    // 已按负责人决定清理（2026-10-31 / 2026-11-03）；这条用例防止它们回潮。
    let retired =
        [
            "FontReading"
            "ComposerMaxHeight"
            "ComposerShellPaddingX"
            "ComposerShellPaddingY"
            "ComposerAttachmentNameMaxWidth"
            "ComposerAttachmentStateWidth"
            "ComposerModelMaxWidth"
            "ComposerModelCompactMaxWidth"
            "AttachmentRowPaddingY"
        ]

    let names = ThemeMetrics.all |> List.map string

    for r in retired do
        Assert.DoesNotContain(r, names)
