module MessageTimestampTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "MessageTimestamp", "messagetimestamp.txt"))

/// 金样由旧代码（从 旧 Views/MessageCard.fs 的 formatTimestamp（Now 改为参数） 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), MessageTimestampRender.render ())

[<Fact>]
let ``same day shows time only and older days show dates`` () =
    let now = DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero)
    Assert.DoesNotContain("月", MessageTimestamp.format now (now.AddHours -1.0))
    Assert.StartsWith("昨天", MessageTimestamp.format now (now.AddDays -1.0))
    Assert.Contains("10月12日", MessageTimestamp.format now (now.AddDays -2.0))
    Assert.StartsWith("2025年", MessageTimestamp.format now (now.AddYears -1))
