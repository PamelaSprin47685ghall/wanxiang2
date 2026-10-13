namespace Snapshot

open System
open System.IO
open System.Security.Cryptography
open System.Text

/// 读取结果。
type Loaded<'T> =
    /// 文件不存在。
    | Missing
    | Loaded of 'T
    /// 文件存在但不可用（校验失败、版本不对、解码失败）。快照是缓存，调用方应忽略它并完整重建。
    | Corrupt of reason: string

/// 原子快照文件：任意值 'T 经调用方编码为文本后保存。
/// 文件格式：首行 `wanxiang-snapshot 1 <sha256-hex>`，其后是正文（UTF-8）。
[<RequireQualifiedAccess>]
module Snapshot =
    [<Literal>]
    let Version = 1

    let private magic = "wanxiang-snapshot"

    let private hashHex (bytes: byte[]) = SHA256.HashData bytes |> Convert.ToHexString |> _.ToLowerInvariant()

    /// 原子保存：写临时文件、落盘、再改名覆盖。任何时刻读者看到的要么是旧快照，要么是新快照。
    let save (path: string) (encode: 'T -> string) (value: 'T) : unit =
        let full = Path.GetFullPath path
        Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
        let body = Encoding.UTF8.GetBytes(encode value)
        let header = Encoding.UTF8.GetBytes(sprintf "%s %d %s\n" magic Version (hashHex body))
        let tmp = full + ".tmp"
        do
            use fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
            fs.Write(header, 0, header.Length)
            fs.Write(body, 0, body.Length)
            fs.Flush true
        File.Move(tmp, full, true)

    /// 读取并校验。
    let load (path: string) (decode: string -> Result<'T, string>) : Loaded<'T> =
        if not (File.Exists path) then Missing
        else
            try
                let bytes = File.ReadAllBytes path
                let nl = Array.IndexOf(bytes, 0x0Auy)
                if nl < 0 then Corrupt "missing header"
                else
                    let parts = Encoding.UTF8.GetString(bytes, 0, nl).Split ' '
                    let body = bytes[nl + 1 ..]
                    if parts.Length <> 3 || parts[0] <> magic then Corrupt "bad header"
                    elif parts[1] <> string Version then Corrupt(sprintf "unsupported version %s" parts[1])
                    elif parts[2] <> hashHex body then Corrupt "checksum mismatch"
                    else
                        match decode (Encoding.UTF8.GetString body) with
                        | Ok v -> Loaded v
                        | Error e -> Corrupt("decode: " + e)
            with e -> Corrupt e.Message
