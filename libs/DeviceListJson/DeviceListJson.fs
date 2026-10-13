namespace Wanxiang.Server

open System
open System.Globalization
open System.Text.Json.Nodes

/// 设备列表里一台设备的线上 JSON 形状（从旧 WsConnection.SendDevices 拆出）。
module DeviceListJson =

    let private iso (t: DateTimeOffset) = t.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)

    /// online 只对未吊销的设备成立（与旧实现一致）。
    let item (id: string) (name: string) (createdAt: DateTimeOffset) (lastSeen: DateTimeOffset option)
             (revoked: bool) (online: bool) (current: bool) (desktop: bool) : JsonObject =
        let o = JsonObject()
        o["id"] <- id
        o["name"] <- name
        o["createdAt"] <- iso createdAt
        match lastSeen with
        | Some t -> o["lastSeen"] <- iso t
        | None -> ()
        o["revoked"] <- revoked
        o["online"] <- (not revoked && online)
        o["current"] <- current
        o["desktop"] <- desktop
        o
