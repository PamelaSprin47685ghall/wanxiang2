namespace Wanxiang.Server

open System
open System.Globalization
open System.Text.Json.Nodes

/// 设备条目里彼此正交的运行时事实（用集合表达，消灭四个裸布尔；
/// 成员可任意组合，线上 JSON 的 `online` 仍只对未吊销设备成立）。
[<RequireQualifiedAccess>]
type DeviceFact =
    | Revoked
    | Online
    | Current
    | Desktop

/// 设备列表里一台设备的线上 JSON 形状（从旧 WsConnection.SendDevices 拆出）。
module DeviceListJson =

    let private iso (t: DateTimeOffset) =
        t.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)

    /// online 只对未吊销的设备成立（与旧实现一致）。
    let item
        (id: string)
        (name: string)
        (createdAt: DateTimeOffset)
        (lastSeen: DateTimeOffset option)
        (facts: Set<DeviceFact>)
        : JsonObject =
        let has f = Set.contains f facts
        let o = JsonObject()
        o["id"] <- id
        o["name"] <- name
        o["createdAt"] <- iso createdAt

        match lastSeen with
        | Some t -> o["lastSeen"] <- iso t
        | None -> ()

        o["revoked"] <- has DeviceFact.Revoked
        o["online"] <- (not (has DeviceFact.Revoked) && has DeviceFact.Online)
        o["current"] <- has DeviceFact.Current
        o["desktop"] <- has DeviceFact.Desktop
        o
