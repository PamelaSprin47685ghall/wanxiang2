namespace Wanxiang.App

/// 收到一次关闭信号后该做什么。
[<RequireQualifiedAccess>]
type ShutdownStep =
    /// 第一次：优雅排空（关闭连接、单写者 flush）后退出。
    | Graceful
    /// 第二次及以后：立即退出（退出码 130）。
    | Force

/// 关闭信号计数（决策 101/102，Q101/102；从旧 Program.fs 拆出）。
/// SIGINT 与 SIGTERM 可能并发到达，计数在锁内，保证恰好一次 Graceful。
type ShutdownSignals() =
    let gate = obj ()
    let mutable count = 0

    /// 记一次信号，返回这一次该做的事。
    member _.Signal() : ShutdownStep =
        lock gate (fun () ->
            count <- count + 1

            if count >= 2 then
                ShutdownStep.Force
            else
                ShutdownStep.Graceful)

    /// 立即退出时的进程退出码（128 + SIGINT）。
    static member ForceExitCode = 130
