module PopupPlacementTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "PopupPlacement", "popupplacement.txt"))

/// 金样由旧代码（从 旧 Controls/OverlayHost.fs 的 positionPopup，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), PopupPlacementRender.render ())

[<Fact>]
let ``popup flips above when there is no room below`` () =
    let _, y = PopupPlacement.place 10.0 500.0 30.0 20.0 100.0 200.0 400.0 600.0 16.0 8.0 false
    Assert.Equal(500.0 - 8.0 - 200.0, y)

[<Fact>]
let ``right aligned popup shares the anchor right edge`` () =
    let x, _ = PopupPlacement.place 200.0 10.0 100.0 20.0 150.0 50.0 400.0 600.0 16.0 8.0 true
    Assert.Equal(150.0, x)

// ---- 文档规则的真实行为（随机尺寸）

let private rng = Random 20261014
let private cases =
    [ for _ in 1 .. 500 ->
        let vw, vh = 200.0 + rng.NextDouble() * 1200.0, 200.0 + rng.NextDouble() * 900.0
        let aw, ah = 10.0 + rng.NextDouble() * 80.0, 10.0 + rng.NextDouble() * 40.0
        let ax, ay = rng.NextDouble() * (vw - aw), rng.NextDouble() * (vh - ah)
        let w, h = 40.0 + rng.NextDouble() * 400.0, 20.0 + rng.NextDouble() * 400.0
        ax, ay, aw, ah, w, h, vw, vh, rng.Next 2 = 0 ]

let private inset, gap = 16.0, 8.0

[<Fact>]
let ``a popup that fits stays fully inside the viewport with the inset`` () =
    for ax, ay, aw, ah, w, h, vw, vh, right in cases do
        let x, y = PopupPlacement.place ax ay aw ah w h vw vh inset gap right
        Assert.True(x >= inset && y >= inset)
        if w + 2.0 * inset <= vw then Assert.True(x + w <= vw - inset + 1e-9)
        if h + 2.0 * inset <= vh then Assert.True(y + h <= vh - inset + 1e-9)

[<Fact>]
let ``room below means the popup opens below the anchor`` () =
    for ax, ay, aw, ah, w, h, vw, vh, right in cases do
        let below = ay + ah + gap
        if below + h <= vh - inset then
            let _, y = PopupPlacement.place ax ay aw ah w h vw vh inset gap right
            Assert.Equal(below, y)

[<Fact>]
let ``no room below flips above the anchor when that fits`` () =
    for ax, ay, aw, ah, w, h, vw, vh, right in cases do
        let above = ay - gap - h
        if ay + ah + gap + h > vh - inset && above >= inset then
            let _, y = PopupPlacement.place ax ay aw ah w h vw vh inset gap right
            Assert.Equal(above, y, 9)

[<Fact>]
let ``alignment edge is kept unless the viewport clamps it`` () =
    for ax, ay, aw, ah, w, h, vw, vh, right in cases do
        let x, _ = PopupPlacement.place ax ay aw ah w h vw vh inset gap right
        let wanted = if right then ax + aw - w else ax
        if wanted >= inset && wanted + w <= vw - inset then Assert.Equal(wanted, x, 9)
