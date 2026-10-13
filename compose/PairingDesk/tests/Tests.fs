/// 真实组合：真实的 PairingCodes、FailureLimiter、ClientToken、DesktopClient，由 PairingDesk 组合。
module PairingDeskTests

open System
open Xunit
open ClientToken
open PairingDesk

let now = DateTimeOffset.Parse "2026-01-01T12:00:00Z"
let limits = { window = TimeSpan.FromMinutes 1.0; maxFailures = 3; freeze = TimeSpan.FromMinutes 5.0 }
let desk () = PairingDesk(limits, TimeSpan.FromMinutes 5.0)
let codeOf = function Started(_, Some c) -> c | o -> failwithf "%A" o

[<Fact>]
let ``request, attempt, issue a token whose hash verifies; code is single use`` () =
    let d = desk ()
    let code = codeOf (d.Request(now, "10.0.0.2"))
    match d.Attempt(now, "10.0.0.2", code, Some "phone", true) with
    | Accepted(token, h, name) ->
        Assert.True(ClientToken.matches token h)
        Assert.Equal("phone", name)
        d.Paired "10.0.0.2"
    | o -> failwithf "%A" o
    match d.Attempt(now, "10.0.0.2", code, None, true) with
    | Refused("pairing code already used", None) -> ()
    | o -> failwithf "%A" o

[<Fact>]
let ``wrong codes freeze the address; frozen address cannot request or attempt; others unaffected`` () =
    let d = desk ()
    let code = codeOf (d.Request(now, "a"))
    let wrong = if code = "000000" then "000001" else "000000"
    for _ in 1..2 do
        match d.Attempt(now, "a", wrong, None, true) with Refused(_, None) -> () | o -> failwithf "%A" o
    match d.Attempt(now, "a", wrong, None, true) with Refused(_, Some u) -> Assert.Equal(now + limits.freeze, u) | o -> failwithf "%A" o
    match d.Attempt(now, "a", code, None, true) with Refused("frozen", Some _) -> () | o -> failwithf "%A" o
    match d.Request(now, "a") with Frozen _ -> () | o -> failwithf "%A" o
    // 冻结期间码没被消耗；别的地址仍能用
    match d.Attempt(now, "b", code, None, true) with Accepted _ -> () | o -> failwithf "%A" o

[<Fact>]
let ``paired clears failures for the address`` () =
    let d = desk ()
    let code = codeOf (d.Request(now, "a"))
    let wrong = if code = "000000" then "000001" else "000000"
    for _ in 1..2 do d.Attempt(now, "a", wrong, None, true) |> ignore
    match d.Attempt(now, "a", code, None, true) with Accepted _ -> d.Paired "a" | o -> failwithf "%A" o
    let code2 = codeOf (d.Request(now, "a"))
    match d.Attempt(now, "a", wrong, None, true) with Refused(_, None) -> () | o -> failwithf "%A" o
    ignore code2

[<Fact>]
let ``only the local desktop client can invite; a pending invite is reused, never overwritten`` () =
    let d = desk ()
    Assert.True((d.Invite(now, "phone", false, "127.0.0.1", TimeSpan.FromMinutes 10.0)).IsError)
    Assert.True((d.Invite(now, "wanxiang-desktop", false, "10.0.0.9", TimeSpan.FromMinutes 10.0)).IsError)
    let inv = match d.Invite(now, "wanxiang-desktop", false, "127.0.0.1", TimeSpan.FromMinutes 10.0) with Ok s -> s | Error e -> failwith e
    match d.Request(now + TimeSpan.FromMinutes 1.0, "10.0.0.2") with Started(540, None) -> () | o -> failwithf "%A" o
    // 扫码的设备没先请求也能直接提交邀请码；不能取保留名
    match d.Attempt(now, "10.0.0.2", inv.code, Some "wanxiang-desktop", false) with
    | Accepted(_, _, "client-10.0.0.2") -> ()
    | o -> failwithf "%A" o

[<Fact>]
let ``attempt without request or invite is ignored`` () =
    Assert.Equal(Ignored, (desk ()).Attempt(now, "a", "123456", None, false))

[<Fact>]
let ``reconfigure applies new limits`` () =
    let d = desk ()
    d.Reconfigure { limits with maxFailures = 1 }
    let code = codeOf (d.Request(now, "a"))
    let wrong = if code = "000000" then "000001" else "000000"
    match d.Attempt(now, "a", wrong, None, true) with Refused(_, Some _) -> () | o -> failwithf "%A" o

// 旧 WsConnection：未认证的连接要求发邀请时记一次失败，并且与错码共用同一个限额。
[<Fact>]
let ``unauthenticated invites count toward the same limit as wrong codes`` () =
    let d = desk ()
    let code = codeOf (d.Request(now, "10.0.0.9"))
    Assert.Equal(None, d.RecordFailure(now, "10.0.0.9"))
    match d.Attempt(now, "10.0.0.9", "000000", None, true) with
    | Refused(_, None) -> ()
    | o -> failwithf "%A" o
    let until = d.RecordFailure(now, "10.0.0.9")
    Assert.Equal(Some(now + limits.freeze), until)
    match d.Request(now, "10.0.0.9") with
    | Frozen _ -> ()
    | o -> failwithf "%A" o
    match d.Attempt(now, "10.0.0.9", code, None, true) with
    | Refused("frozen", Some _) -> ()
    | o -> failwithf "%A" o
    Assert.Equal(None, d.RecordFailure(now, "10.0.0.10"))
