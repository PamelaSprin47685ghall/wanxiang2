namespace ClientToken

open System
open System.Security.Cryptography
open System.Text

/// 每设备永久令牌：客户端保存原文，服务端只保存哈希。
/// 格式是用户手里的凭据，不能变：前缀 + 32 字节随机的 base64url（无填充）；哈希 = SHA-256(UTF-8 原文) 的小写十六进制。
[<RequireQualifiedAccess>]
module ClientToken =
    [<Literal>]
    let Prefix = "wanxiang_client_"

    /// 新令牌（256 bit 密码学随机）。
    let generate () : string =
        let b64 =
            Convert.ToBase64String(RandomNumberGenerator.GetBytes 32).TrimEnd('=').Replace('+', '-').Replace('/', '_')

        Prefix + b64

    /// 存进配置的哈希。
    let hash (token: string) : string =
        SHA256.HashData(Encoding.UTF8.GetBytes token)
        |> Convert.ToHexString
        |> _.ToLowerInvariant()

    /// 常量时间比较两个字符串（例如两个哈希）。
    let fixedTimeEquals (a: string) (b: string) : bool =
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes a, Encoding.UTF8.GetBytes b)

    /// 令牌原文是否对应这个哈希（常量时间）。
    let matches (token: string) (storedHash: string) : bool =
        not (isNull token)
        && not (isNull storedHash)
        && fixedTimeEquals (hash token) storedHash

    /// 设备的稳定标识：哈希的前 16 位十六进制。界面与设备管理用它指代设备，不能用于认证。
    let deviceId (tokenHash: string) : string =
        if isNull tokenHash then ""
        elif tokenHash.Length <= 16 then tokenHash
        else tokenHash.Substring(0, 16)
