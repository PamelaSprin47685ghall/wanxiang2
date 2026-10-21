module MessageTimestampTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``same day shows time only and older days show dates`` () =
    let now = DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero)
    Assert.DoesNotContain("月", MessageTimestamp.format now (now.AddHours -1.0))
    Assert.StartsWith("昨天", MessageTimestamp.format now (now.AddDays -1.0))
    Assert.Contains("10月12日", MessageTimestamp.format now (now.AddDays -2.0))
    Assert.StartsWith("2025年", MessageTimestamp.format now (now.AddYears -1))
