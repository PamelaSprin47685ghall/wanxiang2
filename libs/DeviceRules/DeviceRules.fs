namespace Wanxiang.Server

open System

/// 设备管理动作（重命名 / 吊销）。
[<RequireQualifiedAccess>]
type DeviceChange =
    | Rename of string
    | Revoke

/// 设备改名与吊销的规则：名称非空、不超过 64 字符、不占用桌面客户端保留名；
/// 桌面客户端不能改名或在这里吊销；不能吊销当前连接自己；不能重复吊销。
/// 从旧 WsConnection.ChangeDevice 拆出，只依赖 BCL；「是否保留名」由调用方给出判定函数。
module DeviceRules =

    /// 检查一次改动是否允许；Error 带给用户看的原文（与旧实现一致）。
    let check (isReservedName: string -> bool) (targetName: string) (targetIsCurrent: bool) (targetRevoked: bool) (change: DeviceChange) : Result<unit, string> =
        let isDesktop = isReservedName targetName
        match change with
        | DeviceChange.Rename name when String.IsNullOrWhiteSpace name -> Error "设备名称不能为空"
        | DeviceChange.Rename name when name.Trim().Length > 64 -> Error "设备名称最多 64 个字符"
        | DeviceChange.Rename name when isReservedName name -> Error "该名称为本机桌面客户端保留"
        | DeviceChange.Rename _ when isDesktop -> Error "本机桌面客户端不能重命名"
        | DeviceChange.Revoke when isDesktop -> Error "本机桌面客户端不能在这里吊销"
        | DeviceChange.Revoke when targetIsCurrent -> Error "不能吊销当前正在使用的设备"
        | DeviceChange.Revoke when targetRevoked -> Error "该设备已被吊销"
        | _ -> Ok()
