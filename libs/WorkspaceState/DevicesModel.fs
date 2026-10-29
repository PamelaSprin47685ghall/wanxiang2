namespace Wanxiang.UI

open System
open System.Text.Json.Nodes

/// 一台已授权设备（来自 `wanxiang.dev/devices.snapshot`）。令牌哈希不出主机，`id` 是截断的稳定标识。
type DeviceInfo =
    {
        id: string
        name: string
        createdAt: DateTimeOffset option
        lastSeen: DateTimeOffset option
        revoked: bool
        online: bool
        /// 当前这条连接就是它
        current: bool
        /// 本机桌面客户端（保留名；不能在这里重命名或吊销）
        desktop: bool
    }

/// 配对邀请：码 + 到期时间。只在内存，不入日志。
type PairingInviteView =
    {
        code: string
        expiresAtUtc: DateTimeOffset
    }

module DevicesModel =

    /// 邀请有效期（决策 46）：与主机一致，仅用于兜底显示；以主机下发的到期时间为准。
    let inviteLifetime = TimeSpan.FromMinutes 5.0

    let private tryTime (o: JsonObject) (key: string) : DateTimeOffset option =
        match o[key] with
        | :? JsonValue as v ->
            match v.TryGetValue<string>() with
            | true, raw ->
                match
                    DateTimeOffset.TryParse(
                        raw,
                        Globalization.CultureInfo.InvariantCulture,
                        Globalization.DateTimeStyles.RoundtripKind
                    )
                with
                | true, t -> Some t
                | _ -> None
            | _ -> None
        | _ -> None

    let private tryBool (o: JsonObject) (key: string) : bool =
        match o[key] with
        | :? JsonValue as v ->
            match v.TryGetValue<bool>() with
            | true, b -> b
            | _ -> false
        | _ -> false

    let private tryString (o: JsonObject) (key: string) : string =
        match o[key] with
        | :? JsonValue as v ->
            match v.TryGetValue<string>() with
            | true, s -> s
            | _ -> ""
        | _ -> ""

    /// 解析设备快照。未知字段忽略，缺字段取保守默认值（宽容读）。
    let parse (items: JsonArray) : DeviceInfo list =
        [
            for item in items do
                match item with
                | :? JsonObject as o ->
                    yield
                        {
                            id = tryString o "id"
                            name = tryString o "name"
                            createdAt = tryTime o "createdAt"
                            lastSeen = tryTime o "lastSeen"
                            revoked = tryBool o "revoked"
                            online = tryBool o "online"
                            current = tryBool o "current"
                            desktop = tryBool o "desktop"
                        }
                | _ -> ()
        ]

    /// 剩余时间 → `m:ss`；过期（≤ 0）显示 `0:00`。
    let countdown (remaining: TimeSpan) : string =
        let total = max 0 (int (Math.Ceiling remaining.TotalSeconds))
        sprintf "%d:%02d" (total / 60) (total % 60)

    /// 6 位码在界面上分成 3+3，便于口述和核对；复制与校验仍用原值。
    let groupCode (code: string) : string =
        if not (isNull code) && code.Length = 6 then
            code.Substring(0, 3) + " " + code.Substring 3
        else
            code

    /// 设备行的“最后在线”文案。
    let lastSeenText (now: DateTimeOffset) (device: DeviceInfo) : string =
        if device.revoked then
            "已吊销"
        elif device.online then
            "在线"
        else
            match device.lastSeen with
            | None -> "从未连接"
            | Some t ->
                let age = now - t

                if age < TimeSpan.FromMinutes 1.0 then
                    "刚刚在线"
                elif age < TimeSpan.FromHours 1.0 then
                    sprintf "%d 分钟前在线" (int age.TotalMinutes)
                elif age < TimeSpan.FromDays 1.0 then
                    sprintf "%d 小时前在线" (int age.TotalHours)
                else
                    sprintf "%s 在线" (t.ToLocalTime().ToString("yyyy-MM-dd"))

    let createdText (device: DeviceInfo) : string =
        match device.createdAt with
        | Some t -> sprintf "加入于 %s" (t.ToLocalTime().ToString("yyyy-MM-dd"))
        | None -> "加入时间未知"
