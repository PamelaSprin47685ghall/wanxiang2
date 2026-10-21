namespace Wanxiang.Store

open System
open System.IO
open System.Security.Cryptography

/// 复制文件并核对 SHA-256：副本与原件哈希不同就失败。修复证据包用它保全现场（从旧 RecoveryBundle 拆出）。
module VerifiedCopy =

    /// 流式计算文件 SHA-256（小写十六进制）；共享读，写入者仍可追加。
    let sha256OfFile (path: string) : string =
        use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        use sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
        let buf = Array.zeroCreate<byte>(64 * 1024)

        let rec loop () =
            let n = fs.Read(buf, 0, buf.Length)

            if n > 0 then
                sha.AppendData(buf, 0, n)
                loop ()

        loop ()
        Convert.ToHexStringLower(sha.GetHashAndReset())

    /// 把 `source` 复制进 `targetDir`（同名，不覆盖），核对哈希，返回 (原件 sha256, 原件大小)。
    /// 哈希不同则抛异常（调用方整体失败，不部分成功）。
    let copyInto (targetDir: string) (source: string) : string * int64 =
        let copyPath = Path.Combine(targetDir, Path.GetFileName source)
        File.Copy(source, copyPath, false)
        let origSha = sha256OfFile source
        let copySha = sha256OfFile copyPath

        if origSha <> copySha then
            failwithf "recovery bundle hash mismatch for %s (%s != %s)" source origSha copySha

        origSha, FileInfo(source).Length
