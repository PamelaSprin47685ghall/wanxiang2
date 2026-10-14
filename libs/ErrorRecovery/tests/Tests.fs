module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

// 迁自旧测试。
// 旧 LibraryUndoTests `§215 每类不可自愈的错误都有恢复动作`、R19SimplifyTests `366 限流只给「换个模型」并直达切换器` 的 label 一行；forKind 各行迁往 ConversationRuns。
[<Fact>]
let ``366 限流的恢复动作文案是「换个模型」`` () =
    Assert.Equal("换个模型", ErrorRecovery.label SwitchModel)
