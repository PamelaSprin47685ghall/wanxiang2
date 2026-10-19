module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


let m = [| Some 1; Some 2; Some 3; Some 4 |]
let cards = [ 0; 2 ]

[<Fact>]
let ``the anchor follows jumps, not the viewport`` () =
    let j = ExchangeJump<int>()
    Assert.Equal(JumpStep.ScrollTo(0, false), j.Jump(-1, m, cards, 2, false, Some 1UL))
    // 视口还停在第 2 张，但锚点已在第 0 张
    Assert.Equal(JumpStep.ScrollTo(2, true), j.Jump(1, m, cards, 2, false, Some 1UL))

[<Fact>]
let ``older history: request, then jump again once the first commit changes`` () =
    let j = ExchangeJump<int>()
    j.SetOlderAvailable true
    Assert.Equal(JumpStep.RequestOlder, j.Jump(-1, m, cards, 0, false, Some 1UL))
    Assert.False(j.Mounted(m, Some 1UL))
    Assert.True(j.Mounted(m, Some 0UL))
    Assert.False j.PendingPrevious

[<Fact>]
let ``losing older history or a failure drops the pending jump`` () =
    let j = ExchangeJump<int>()
    j.SetOlderAvailable true
    j.Jump(-1, m, cards, 0, false, Some 1UL) |> ignore
    j.OlderFailed()
    Assert.False j.PendingPrevious
    j.Jump(-1, m, cards, 0, false, Some 1UL) |> ignore
    j.SetOlderAvailable false
    Assert.False j.PendingPrevious

[<Fact>]
let ``nav state: no next at the bottom, previous while more exists`` () =
    let j = ExchangeJump<int>()
    Assert.Equal((false, true), j.NavState(m, cards, 0, false, false))
    Assert.Equal((false, false), j.NavState(m, cards, 0, true, false))
    Assert.Equal((true, false), j.NavState(m, cards, 0, true, true))
    Assert.Equal((false, false), j.NavState([||], [], 0, false, true))

[<Fact>]
let ``a vanished anchor card is dropped`` () =
    let j = ExchangeJump<int>()
    j.Jump(1, m, cards, 0, false, None) |> ignore
    j.Mounted([| Some 1 |], None) |> ignore
    Assert.Equal(None, j.Anchor)

[<Fact>]
let ``scrolling to the beginning drops the anchor but keeps the pending previous`` () =
    let j = ExchangeJump<int>()
    j.SetOlderAvailable true
    Assert.Equal(JumpStep.RequestOlder, j.Jump(-1, m, cards, 0, false, Some 1UL))
    Assert.Equal(Some 1, j.Anchor)
    j.ClearAnchor()
    Assert.Equal(None, j.Anchor)
    Assert.True j.PendingPrevious
    Assert.True(j.Mounted(m, Some 0UL))
