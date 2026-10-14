namespace PairingCode

open System
open System.Security.Cryptography
open System.Text

/// 一次配对码会话的公开快照。
type PairingSession = { code: string; expiresAtUtc: DateTimeOffset; used: bool; invited: bool }

type ConsumeError =
    | NoSession
    | AlreadyUsed
    | Expired
    | WrongCode

    member e.Describe() =
        match e with
        | NoSession -> "no pairing session active"
        | AlreadyUsed -> "pairing code already used"
        | Expired -> "pairing code expired"
        | WrongCode -> "invalid pairing code"

/// 配对码：6 位十进制、密码学随机、单次使用、到期失效、同时只有一个有效（新码顶替旧码）。
/// 私有状态：当前会话。码只在内存里。线程安全。
type PairingCodes() =
    let gate = obj ()
    let mutable session : PairingSession option = None

    let fresh (now: DateTimeOffset) (lifetime: TimeSpan) invited =
        let code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")
        let s = { code = code; expiresAtUtc = now + lifetime; used = false; invited = invited }
        session <- Some s
        s

    /// 新码（顶替旧码）。
    member _.Start(now: DateTimeOffset, lifetime: TimeSpan) : PairingSession = lock gate (fun () -> fresh now lifetime false)

    /// 桌面客户端发出的邀请码（同样顶替旧码）。
    member _.StartInvite(now: DateTimeOffset, lifetime: TimeSpan) : PairingSession = lock gate (fun () -> fresh now lifetime true)

    /// 仍有效（未用、未过期）的邀请。
    member _.ActiveInvite(now: DateTimeOffset) : PairingSession option =
        lock gate (fun () ->
            match session with
            | Some s when s.invited && not s.used && now <= s.expiresAtUtc -> Some s
            | _ -> None)

    /// 是否有邀请产生的会话（含已用或已过期的），让提交邀请码的设备能得到明确回执。
    member _.HasInviteSession : bool = lock gate (fun () -> session |> Option.exists _.invited)

    /// 校验并消耗。错误的码不消耗会话（限流是别的对象的事）。
    member _.TryConsume(now: DateTimeOffset, code: string) : Result<unit, ConsumeError> =
        lock gate (fun () ->
            match session with
            | None -> Error NoSession
            | Some s when s.used -> Error AlreadyUsed
            | Some s when now > s.expiresAtUtc ->
                session <- None
                Error Expired
            | Some s ->
                let given = if isNull code then "" else code
                if CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes s.code, Encoding.UTF8.GetBytes given) then
                    session <- Some { s with used = true }
                    Ok()
                else Error WrongCode)

    /// 当前会话（不可变快照）。
    member _.Session : PairingSession option = lock gate (fun () -> session)
