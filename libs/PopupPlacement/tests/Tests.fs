module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "popupplacement.txt"))

/// 金样由旧代码（从 旧 Controls/OverlayHost.fs 的 positionPopup，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``popup flips above when there is no room below`` () =
    let _, y = PopupPlacement.place 10.0 500.0 30.0 20.0 100.0 200.0 400.0 600.0 16.0 8.0 false
    Assert.Equal(500.0 - 8.0 - 200.0, y)

[<Fact>]
let ``right aligned popup shares the anchor right edge`` () =
    let x, _ = PopupPlacement.place 200.0 10.0 100.0 20.0 150.0 50.0 400.0 600.0 16.0 8.0 true
    Assert.Equal(150.0, x)
