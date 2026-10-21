namespace Wanxiang.UI

open System
open System.Net.NetworkInformation
open System.Net.Sockets
open System.Text.Json.Nodes
open QRCoder

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

/// 手机应该打开哪个地址。
[<RequireQualifiedAccess>]
type InviteTarget =
    /// 主机地址对外可达：直接用它。
    | Reachable of url: string
    /// 主机只监听本机回环地址：手机无法直接访问；候选是这台机器的局域网地址（可能为空）。
    | LoopbackOnly of candidates: string list
    /// 还没有连接地址。
    | NoAddress

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

    /// ws(s)://host:port/ws → http(s)://host:port/（同源 PWA 的入口）。
    let httpOrigin (wsUrl: string) : string option =
        match Uri.TryCreate((if isNull wsUrl then "" else wsUrl.Trim()), UriKind.Absolute) with
        | true, uri when uri.Scheme = "ws" || uri.Scheme = "wss" ->
            let scheme = if uri.Scheme = "wss" then "https" else "http"
            Some(UriBuilder(scheme, uri.Host, uri.Port, "/").Uri.ToString())
        | _ -> None

    let isLoopbackHost (wsUrl: string) : bool =
        match Uri.TryCreate((if isNull wsUrl then "" else wsUrl.Trim()), UriKind.Absolute) with
        | true, uri -> uri.IsLoopback || uri.Host = "localhost"
        | _ -> false

    /// 本机的局域网 IPv4 地址（桌面端；浏览器或受限环境返回空）。
    let lanAddresses () : string list =
        try
            [
                for nic in NetworkInterface.GetAllNetworkInterfaces() do
                    if
                        nic.OperationalStatus = OperationalStatus.Up
                        && nic.NetworkInterfaceType <> NetworkInterfaceType.Loopback
                    then
                        for ua in nic.GetIPProperties().UnicastAddresses do
                            if ua.Address.AddressFamily = AddressFamily.InterNetwork then
                                yield ua.Address.ToString()
            ]
            |> List.distinct
        with _ ->
            []

    /// 手机该打开的地址。`lan` 注入便于测试。
    let inviteTarget (wsUrl: string) (lan: string list) : InviteTarget =
        match httpOrigin wsUrl with
        | None -> InviteTarget.NoAddress
        | Some origin ->
            if isLoopbackHost wsUrl then
                let port = (Uri origin).Port
                let scheme = (Uri origin).Scheme
                InviteTarget.LoopbackOnly [ for ip in lan -> UriBuilder(scheme, ip, port, "/").Uri.ToString() ]
            else
                InviteTarget.Reachable origin

    /// 二维码模块矩阵（true = 深色模块）。使用成熟库 QRCoder 编码，不自行实现（规范 58）。
    let qrMatrix (text: string) : bool[][] =
        use generator = new QRCodeGenerator()
        use data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M)
        // QRCoder 的 ModuleMatrix 自带 4 模块静区；这里去掉，由绘制层统一补静区（QrCodeView）。
        let quiet = 4
        let size = data.ModuleMatrix.Count - quiet * 2

        [|
            for r in 0 .. size - 1 -> [| for c in 0 .. size - 1 -> data.ModuleMatrix[r + quiet][c + quiet] |]
        |]
