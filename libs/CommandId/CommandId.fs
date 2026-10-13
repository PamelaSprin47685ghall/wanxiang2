namespace Wanxiang.Core

open System
open System.Security.Cryptography
open System.Text

/// 幂等命令标识。调用方交来已经规范化的业务载荷；规范化方式由调用方负责并保持稳定。
module CommandId =

    /// 命令标识的版本前缀。改它等于让所有已记录的命令标识失效。
    [<Literal>]
    let Prefix = "wanxiang.command.v1"

    /// commandId = base64url( SHA-256( Prefix \0 invocationId \0 commandType \0 canonicalPayload ) )，无填充。
    /// 网络重试复用 invocationId，得到同一个 commandId；内容被改，commandId 跟着变。
    let compute (invocationId: Guid) (commandType: string) (canonicalPayload: string) : string =
        let material = String.concat "\u0000" [ Prefix; invocationId.ToString("D"); commandType; canonicalPayload ]
        let bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material))
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    /// 载荷的 SHA-256 小写十六进制，用于判断同一 commandId 的载荷是否一致。
    let sha256Hex (value: string) : string =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()
