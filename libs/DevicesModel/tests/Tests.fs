module Tests

open Xunit
open Wanxiang.UI

[<Fact>]
let ``loopback host offers lan candidates on the same port`` () =
    match DevicesModel.inviteTarget "ws://127.0.0.1:8787/ws" [ "192.168.0.7" ] with
    | InviteTarget.LoopbackOnly [ c ] -> Assert.Equal("http://192.168.0.7:8787/", c)
    | other -> failwithf "%A" other

[<Fact>]
let ``non websocket urls have no invite address`` () =
    Assert.Equal(InviteTarget.NoAddress, DevicesModel.inviteTarget "https://example.com/" [ "10.0.0.1" ])
    Assert.Equal(None, DevicesModel.httpOrigin null)
    Assert.False(DevicesModel.isLoopbackHost null)

[<Fact>]
let ``qr matrix is square and non-empty`` () =
    let m = DevicesModel.qrMatrix "https://example.com/"
    Assert.True(m.Length > 0)
    Assert.All(m, fun row -> Assert.Equal(m.Length, row.Length))
