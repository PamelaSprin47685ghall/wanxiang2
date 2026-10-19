module DurationTextTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``zero shows nothing`` () = Assert.Equal("", DurationText.format 0L)

[<Fact>]
let ``seconds keep one decimal`` () = Assert.Equal("1.5s", DurationText.format 1500L)
