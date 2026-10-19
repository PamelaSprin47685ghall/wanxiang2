module ScrollFollowTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``at the bottom a render follows; scrolled up it counts unread`` () =
    let f = ScrollFollow()
    Assert.Equal(RenderFollow.ScrollToEnd, f.Rendered(false, 3, false))
    f.Scrolled(true, false) |> ignore
    Assert.Equal(RenderFollow.Keep, f.Rendered(false, 5, false))
    Assert.Equal(2, f.Unread)
    Assert.Equal("2 条新消息", (f.Button(true, false)).text)

[<Fact>]
let ``older pages and streaming refreshes are not unread; a finished stream is`` () =
    let f = ScrollFollow()
    f.Rendered(false, 3, false) |> ignore
    f.Scrolled(true, false) |> ignore
    f.Rendered(false, 30, true) |> ignore
    Assert.Equal(0, f.Unread)
    f.Rendered(true, 30, false) |> ignore
    Assert.Equal(0, f.Unread)
    f.Rendered(false, 30, false) |> ignore
    Assert.Equal(1, f.Unread)

[<Fact>]
let ``returning to latest stays at bottom through mid-animation frames`` () =
    let f = ScrollFollow()
    f.Scrolled(true, false) |> ignore
    f.ReturnToLatest()
    f.Scrolled(false, false) |> ignore
    Assert.True f.AtBottom
    Assert.True(f.Scrolled(true, false))
    Assert.False f.AtBottom

[<Fact>]
let ``pin holds until wheel or reset`` () =
    let f = ScrollFollow()
    f.Pin()
    Assert.True f.RevealPinned
    Assert.False f.AtBottom
    f.Wheel()
    Assert.False f.RevealPinned
