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
let ``退场的阅读字号档不再回到刻度上`` () =
    // 阅读列曾有过一个死档（Metric.FontReading，14.5）：clean-break 重构把输入区
    // 字号从它改成 FontBody 后消费点删了、登记档没删，于是「改它没反应」。
    // 已按负责人决定删除（2026-10-31）；这条用例只防它回潮。
    //
    // 注意本用例**不**证明「刻度上每一项都有消费者」：那要扫本库目录以外的
    // 全部 `.fs` 与资源键，而本库的测试按职责边界只拿到自己（根 AGENTS.md SPEC 2）。
    // 需要那种口径时，做成 `./x` 里的仓库级门禁，别再让一条本库用例冒充它。
    let retired = "FontReading"
    let names = ThemeMetrics.all |> List.map string
    Assert.DoesNotContain(retired, names)
