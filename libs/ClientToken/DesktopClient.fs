namespace DesktopClient

open System
open System.Net

/// 本机桌面客户端这一身份：一个保留的设备名，加上「来自回环地址」。
/// 只有它能发配对邀请；经配对码加入的设备不能取这个名字。
[<RequireQualifiedAccess>]
module DesktopClient =
    /// 保留名。已写进用户的配置文件，不能改。
    [<Literal>]
    let Name = "wanxiang-desktop"

    /// 是否保留名（忽略大小写与首尾空白）。
    let isReservedName (name: string) : bool =
        not (isNull name)
        && String.Equals(name.Trim(), Name, StringComparison.OrdinalIgnoreCase)

    /// 配对设备自报的名字：保留名或空白一律换成 fallback，其余去掉首尾空白。
    let sanitizeName (requested: string option) (fallback: string) : string =
        match requested with
        | Some n when not (String.IsNullOrWhiteSpace n) && not (isReservedName n) -> n.Trim()
        | _ -> fallback

    let isLoopback (address: string) : bool =
        if String.IsNullOrWhiteSpace address then
            false
        else
            match IPAddress.TryParse address with
            | true, ip ->
                IPAddress.IsLoopback ip
                || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))
            | _ -> false

    /// 这条连接是不是本机桌面客户端：设备名是保留名、未吊销、来自回环地址。
    let isDesktop (deviceName: string) (revoked: bool) (remoteAddress: string) : bool =
        not revoked && isReservedName deviceName && isLoopback remoteAddress
