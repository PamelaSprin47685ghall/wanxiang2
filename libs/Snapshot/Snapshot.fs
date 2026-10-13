namespace Snapshot

open System
open System.IO
open System.Security.Cryptography
open System.Text

/// 读取结果。
type Loaded =
    /// 文件不存在。
    | Missing
    | Loaded of string
    /// 文件存在但不可用（校验失败、版本不对、非法 UTF-8）。快照是缓存，调用方应忽略它并完整重建。
    | Corrupt of reason: string

/// 原子快照文件对象：私有状态是它管的那个路径；接收 Save(text)，回答 Load()。
/// 文本代表什么由调用方决定。
/// 文件格式：首行 `wanxiang-snapshot 1 <sha256-hex>`，其后是正文（UTF-8）。
type SnapshotFile(path: string) =
    static let version = 1

    static let magic = "wanxiang-snapshot"

    static let hashHex (bytes: byte[]) = SHA256.HashData bytes |> Convert.ToHexString |> _.ToLowerInvariant()

    member _.Path = path

    /// 原子保存：写临时文件、落盘、再改名覆盖。任何时刻读者看到的要么是旧快照，要么是新快照。
    member _.Save(text: string) : unit =
        let full = Path.GetFullPath path
        Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
        let body = Encoding.UTF8.GetBytes text
        let header = Encoding.UTF8.GetBytes(sprintf "%s %d %s\n" magic version (hashHex body))
        let tmp = full + ".tmp"
        do
            use fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
            fs.Write(header, 0, header.Length)
            fs.Write(body, 0, body.Length)
            fs.Flush true
        File.Move(tmp, full, true)

    /// 读取并校验。
    member _.Load() : Loaded =
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
                    elif parts[1] <> string version then Corrupt(sprintf "unsupported version %s" parts[1])
                    elif parts[2] <> hashHex body then Corrupt "checksum mismatch"
                    else
                        try Loaded(UTF8Encoding(false, true).GetString body)
                        with :? DecoderFallbackException -> Corrupt "invalid UTF-8"
            with e -> Corrupt e.Message
