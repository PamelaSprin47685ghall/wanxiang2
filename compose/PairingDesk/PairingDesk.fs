namespace PairingDesk

open System
open PairingCode
open FailureLimiter
open ClientToken
open DesktopClient

type Limits = { window: TimeSpan; maxFailures: int; freeze: TimeSpan }

type RequestOutcome =
    /// 有一个有效的码。newCode = Some 表示刚生成，宿主应把它告诉用户（例如打在终端上）；
    /// None 表示沿用桌面客户端仍有效的邀请码。
    | Started of expiresInSeconds: int * newCode: string option
    | Frozen of until: DateTimeOffset

type AttemptOutcome =
    /// 码正确，已消耗。宿主先把 tokenHash 与 deviceName 持久化，成功后调用 Paired，再把 token 原文交给设备。
    | Accepted of token: string * tokenHash: string * deviceName: string
    /// 码不对或已失效；frozenUntil = Some 表示这个地址现在被冻结（可能正是这次触发的）。
    | Refused of reason: string * frozenUntil: DateTimeOffset option
    /// 这条连接没请求过配对，也没有邀请：什么也没发生。
    | Ignored

/// 组合者 C：配对柜台。拥有配对这段合作的顺序：查冻结 → 用码 → 失败计数或签发令牌。
/// 码归 PairingCodes，计数归 FailureLimiter，令牌格式归 ClientToken，桌面身份归 DesktopClient。
/// 持久化令牌哈希与给设备发消息归宿主（它拥有配置和连接）。线程安全。
type PairingDesk(limits: Limits, codeLifetime: TimeSpan) =
    let codes = PairingCodes()
    let gate = obj ()
    let mutable limiter = FailureLimiter(limits.window, limits.maxFailures, limits.freeze)

    /// 按新配置重建计数器（旧的失败记录随之清空）。
    member _.Reconfigure(l: Limits) = lock gate (fun () -> limiter <- FailureLimiter(l.window, l.maxFailures, l.freeze))

    /// 设备请求配对。
    member _.Request(now: DateTimeOffset, address: string) : RequestOutcome =
        lock gate (fun () ->
            match limiter.FrozenUntil(now, address) with
            | Some u -> Frozen u
            | None ->
                match codes.ActiveInvite now with
                | Some s -> Started(max 1 (int (Math.Ceiling (s.expiresAtUtc - now).TotalSeconds)), None)
                | None ->
                    let s = codes.Start(now, codeLifetime)
                    Started(int codeLifetime.TotalSeconds, Some s.code))

    /// 本机桌面客户端发邀请；别的连接拿不到。
    member _.Invite(now: DateTimeOffset, deviceName: string, revoked: bool, address: string, lifetime: TimeSpan) : Result<PairingSession, string> =
        if DesktopClient.isDesktop deviceName revoked address then Ok(codes.StartInvite(now, lifetime))
        else Error "only the desktop client on this machine can invite devices"

    member _.ActiveInvite(now: DateTimeOffset) = codes.ActiveInvite now

    /// 设备提交配对码。requested：这条连接此前是否请求过配对（连接自己的状态）。
    member _.Attempt(now: DateTimeOffset, address: string, code: string, requestedName: string option, requested: bool) : AttemptOutcome =
        lock gate (fun () ->
            if not (requested || codes.HasInviteSession) then Ignored
            else
                match limiter.FrozenUntil(now, address) with
                | Some u -> Refused("frozen", Some u)
                | None ->
                    match codes.TryConsume(now, code) with
                    | Ok() ->
                        let token = ClientToken.generate ()
                        Accepted(token, ClientToken.hash token, DesktopClient.sanitizeName requestedName (sprintf "client-%s" address))
                    | Error e ->
                        let froze = limiter.RecordFailure(now, address)
                        Refused(e.Describe(), (if froze then limiter.FrozenUntil(now, address) else None)))

    /// 记一次与配对码无关的失败（例如未认证的连接要求发邀请），与错码共用同一个限额。
    /// 返回这个地址此刻的冻结截止时间；没被冻结时为 None。
    member _.RecordFailure(now: DateTimeOffset, address: string) : DateTimeOffset option =
        lock gate (fun () ->
            limiter.RecordFailure(now, address) |> ignore
            limiter.FrozenUntil(now, address))

    /// 宿主已持久化 Accepted 的令牌哈希：清掉这个地址的失败记录。
    member _.Paired(address: string) = lock gate (fun () -> limiter.Clear address)
