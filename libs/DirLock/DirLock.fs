namespace DirLock

open System
open System.IO
open System.Text

/// 目录独占锁：在目录下打开 `.lock` 并独占（FileShare.None），句柄存活即持锁。
/// 进程崩溃时操作系统关闭句柄，锁自动释放，所以不存在需要人工清理的陈旧锁。
/// 持有者 pid 写在旁边的 `.lock.pid`，仅供诊断，不参与判定
/// （独占打开的 `.lock` 本身别人读不了）。
type DirLock private (stream: FileStream, path: string) =

    /// 锁文件路径。
    member _.Path = path

    /// 尝试获取锁。目录不存在时创建。被占用时返回 Error，消息中带上占用者 pid（若可读）。
    static member TryAcquire(dir: string) : Result<DirLock, string> =
        Directory.CreateDirectory dir |> ignore
        let path = Path.Combine(dir, ".lock")
        try
            let s = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
            File.WriteAllText(path + ".pid", string Environment.ProcessId, Encoding.UTF8)
            Ok(new DirLock(s, path))
        with :? IOException as e ->
            let holder =
                try
                    " (held by pid " + File.ReadAllText(path + ".pid").Trim() + ")"
                with _ -> ""
            Error(sprintf "directory %s is locked%s: %s" dir holder e.Message)

    interface IDisposable with
        member _.Dispose() = stream.Dispose()
