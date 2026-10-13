module PairingCodeTests

open System
open Xunit
open PairingCode

let now = DateTimeOffset.Parse "2026-01-01T12:00:00Z"
let life = TimeSpan.FromMinutes 5.0

// SPEC 1：生命周期：无会话、错码不消耗、新码顶替旧码、单次使用、过期清除
[<Fact>]
let ``full lifecycle`` () =
    let p = PairingCodes()
    Assert.Equal(Error NoSession, p.TryConsume(now, "123456"))
    let s1 = p.Start(now, life)
    Assert.Equal(6, s1.code.Length)
    Assert.True(s1.code |> Seq.forall Char.IsDigit)
    Assert.Equal(now + life, s1.expiresAtUtc)
    let wrong = if s1.code = "000000" then "000001" else "000000"
    Assert.Equal(Error WrongCode, p.TryConsume(now, wrong))
    Assert.False p.Session.Value.used
    let s2 = p.Start(now, life)
    if s2.code <> s1.code then Assert.Equal(Error WrongCode, p.TryConsume(now, s1.code))
    Assert.Equal(Ok(), p.TryConsume(now, s2.code))
    Assert.Equal(Error AlreadyUsed, p.TryConsume(now, s2.code))
    let s3 = p.Start(now, life)
    Assert.Equal(Error Expired, p.TryConsume(now + life + TimeSpan.FromSeconds 1.0, s3.code))
    Assert.Equal(None, p.Session)
    Assert.Equal(Error WrongCode, (p.Start(now, life) |> ignore; p.TryConsume(now, null)))

// SPEC 2：邀请：仍有效时可查，用后或过期后不再是 ActiveInvite，但 HasInviteSession 仍为真以便回执；普通 Start 清掉邀请标记
[<Fact>]
let ``invite is visible while valid and reported after use`` () =
    let p = PairingCodes()
    let s = p.StartInvite(now, life)
    Assert.True s.invited
    Assert.Equal(Some s, p.ActiveInvite now)
    Assert.Equal(None, p.ActiveInvite(now + life + TimeSpan.FromSeconds 1.0))
    Assert.Equal(Ok(), p.TryConsume(now, s.code))
    Assert.Equal(None, p.ActiveInvite now)
    Assert.True p.HasInviteSession
    p.Start(now, life) |> ignore
    Assert.False p.HasInviteSession

// SPEC 3：错误有可读描述（与旧文案一致，客户端界面显示它）
[<Fact>]
let ``errors describe themselves with the existing wording`` () =
    Assert.Equal<string list>(
        [ "no pairing session active"; "pairing code already used"; "pairing code expired"; "invalid pairing code" ],
        [ NoSession; AlreadyUsed; Expired; WrongCode ] |> List.map _.Describe())
