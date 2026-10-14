module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "durationtext.txt"))

/// 金样由旧代码（从 旧 Views/MessageCard.fs 的 formatDuration 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``zero shows nothing`` () = Assert.Equal("", DurationText.format 0L)

[<Fact>]
let ``seconds keep one decimal`` () = Assert.Equal("1.5s", DurationText.format 1500L)
