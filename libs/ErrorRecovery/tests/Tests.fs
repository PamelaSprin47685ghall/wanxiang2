module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``366 限流的恢复动作文案是「换个模型」`` () =
    Assert.Equal("换个模型", ErrorRecovery.label SwitchModel)
