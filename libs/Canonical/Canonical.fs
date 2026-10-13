namespace Canonical

open System
open System.Security.Cryptography
open Jcs.Net

/// RFC 8785 JSON 规范化（JCS）与内容哈希。
/// 同一语义的 JSON（键序、空白、数字写法不同）得到同一字节串，于是得到同一哈希。
[<RequireQualifiedAccess>]
module Jcs =
    /// 规范化为 UTF-8 字节。
    let utf8 (json: string) : byte[] = JsonCanonicalizer.CanonicalizeToUtf8 json

    /// 规范化为字符串。
    let text (json: string) : string = JsonCanonicalizer.Canonicalize json

    /// SHA-256(JCS(json))，十六进制小写。
    let sha256Hex (json: string) : string =
        utf8 json |> SHA256.HashData |> Convert.ToHexString |> _.ToLowerInvariant()
