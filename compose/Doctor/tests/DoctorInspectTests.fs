module Wanxiang.Tests.DoctorInspectTests

// 宿主运行时的命令行 doctor / 支持包（不持锁 Inspect 路径），以及「结果未知」。
// 全部用真实存储持锁、真实文件字节；结果未知用真实写失败（Linux：日志符号链接到 /dev/full；
// 其他 POSIX：文件大小上限收紧到 0，写与截回都按 EFBIG 失败）。

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Store
open Wanxiang.Server
open Wanxiang.Tests.Helpers

// FsizeZero 收紧的是整个进程的文件大小上限；并行的其他用例此时写文件会一起失败。
// 本程序集用例很少，整体串行。
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()

/// 只在测试里用：临时把本进程 RLIMIT_FSIZE 收紧到 0，并忽略 SIGXFSZ。
/// macOS 没有 /dev/full；在已有内容的文件上，超限的写按 EFBIG 失败，
/// 截回到原长度的 ftruncate 在 Darwin 上同样按 EFBIG 失败 —— 写失败且截不回，是真实故障。
/// 用完恢复原上限与原信号处置。
module private FsizeZero =
    open System.Runtime.InteropServices

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RLimit =
        {
            mutable Cur: uint64
            mutable Max: uint64
        }

    [<DllImport("libc", SetLastError = true)>]
    extern int getrlimit(int resource, RLimit& rlim)

    [<DllImport("libc", SetLastError = true)>]
    extern int setrlimit(int resource, RLimit& rlim)

    [<DllImport("libc", SetLastError = true)>]
    extern IntPtr signal(int signum, IntPtr handler)

    let run (act: unit -> 'a) : 'a =
        let mutable old = { Cur = 0UL; Max = 0UL }

        if getrlimit (1, &old) <> 0 then
            failwith "getrlimit failed" // RLIMIT_FSIZE

        let previous = signal (25, IntPtr 1) // SIGXFSZ → SIG_IGN

        if previous = IntPtr(-1) then
            failwith "signal failed"

        try
            try
                let mutable zero = { Cur = 0UL; Max = old.Max }

                if setrlimit (1, &zero) <> 0 then
                    failwith "setrlimit failed"

                act ()
            finally
                setrlimit (1, &old) |> ignore
        finally
            signal (25, previous) |> ignore

let private create (store: EventStore) =
    store.SubmitEvents
        [
            ConversationCreated
                {
                    conversationId = Guid.NewGuid()
                    title = "t"
                    config = SessionConfig.empty
                }
        ]

let private logCheck (d: Doctor.Diagnosis) =
    d.checks |> List.find (fun c -> c.name = "log-replay")

let private noConfig dir = Path.Combine(dir, "missing.toml")

let private bundleDiag (dir: string) : JsonNode =
    let out = Path.Combine(dir, "bundle-out")

    match SupportBundle.create dir (noConfig dir) out with
    | Error e -> failwithf "bundle failed: %s" e
    | Ok b -> JsonNode.Parse(File.ReadAllText(Path.Combine(b, "diagnosis.json")))

let private commit (i: uint64) =
    Events.Commit.create
        i
        (DateTimeOffset(2026, 10, 1, 0, 0, int i, TimeSpan.Zero))
        [
            ConversationCreated
                {
                    conversationId = Guid.NewGuid()
                    title = sprintf "c%d" i
                    config = SessionConfig.empty
                }
        ]

[<Fact>]
let ``host running: CLI doctor checks without the lock and reports healthy`` () =
    let dir = tempDir ()

    try
        use host = new EventStore(dir)

        for _ in 1..3 do
            match create host with
            | Committed _ -> ()
            | r -> failwithf "submit %A" r

        let c = logCheck (Doctor.run (noConfig dir, dir))
        Assert.True(c.ok, c.detail)
        Assert.Contains("healthy through commit 3", c.detail)
        Assert.Contains("host running", c.detail)
        let diag = bundleDiag dir
        Assert.Equal("healthy", diag["logStatus"].GetValue<string>())
        Assert.True(diag["hostRunning"].GetValue<bool>())
        // 宿主仍可写
        match create host with
        | Committed c -> Assert.Equal(4UL, c.id)
        | r -> failwithf "submit after inspect %A" r
    finally
        cleanup dir

[<Fact>]
let ``host running: half-written last record is reported as possibly in flight, not damage`` () =
    let dir = tempDir ()

    try
        use host = new EventStore(dir)
        create host |> ignore
        // 宿主追加到一半的样子：一条记录的前半截
        let frame = Assembly.frame (commit 2UL)

        use fs =
            new FileStream(DataPaths.logFile dir, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)

        fs.Write(frame, 0, frame.Length / 2)
        fs.Flush()
        let c = logCheck (Doctor.run (noConfig dir, dir))
        Assert.True(c.ok, c.detail)
        Assert.Contains("may still be being written", c.detail)
        let diag = bundleDiag dir
        Assert.Equal("healthy", diag["logStatus"].GetValue<string>())
        Assert.True(diag["tailMayBeInFlight"].GetValue<bool>())
    finally
        cleanup dir

[<Fact>]
let ``host running: damage before the tail is real damage`` () =
    let dir = tempDir ()

    try
        DataPaths.ensureDataDirs dir
        let bad = JsonSeq.Frame.encode "{not json"

        File.WriteAllBytes(
            DataPaths.logFile dir,
            Array.concat [ Assembly.frame (commit 1UL); bad; Assembly.frame (commit 2UL) ]
        )

        use host = new EventStore(dir)
        Assert.True(host.IsReadOnly)
        let c = logCheck (Doctor.run (noConfig dir, dir))
        Assert.False(c.ok, c.detail)
        Assert.Contains("Stop the host", c.detail)
        Assert.Contains("through commit 1", c.detail)
        let diag = bundleDiag dir
        Assert.Equal("repair-required", diag["logStatus"].GetValue<string>())
        Assert.Equal(int64 (Assembly.frame (commit 1UL)).Length, diag["damageOffset"].GetValue<int64>())
    finally
        cleanup dir

[<Fact>]
let ``outcome unknown from a real write failure names the commit`` () =
    let dir = tempDir ()

    try
        DataPaths.ensureDataDirs dir

        let host, failing, submitUnknown =
            if OperatingSystem.IsLinux() then
                // 真实故障：日志是指向 /dev/full 的符号链接，写入 ENOSPC 且截不回
                File.CreateSymbolicLink(DataPaths.logFile dir, "/dev/full") |> ignore
                let host = new EventStore(dir)
                host, 1UL, (fun () -> create host)
            else
                // 真实故障（macOS 等）：先落一条提交，再把文件大小上限收紧到 0，第二条写失败且截不回
                let host = new EventStore(dir)

                match create host with
                | Committed _ -> ()
                | r -> failwithf "submit %A" r

                let submit () = FsizeZero.run (fun () -> create host)
                host, 2UL, submit

        use _host = host

        match submitUnknown () with
        | CommitFailed(Poisoned m) -> Assert.Contains(sprintf "outcome of commit %d unknown" failing, m)
        | r -> failwithf "expected unknown outcome, got %A" r

        Assert.True(host.IsReadOnly)

        match Inspect.ofStore host with
        | OutcomeUnknown(_, id, _) -> Assert.Equal(failing, id)
        | r -> failwithf "expected OutcomeUnknown, got %A" r
        // 主机内的 doctor（检查主机）给出带 commit id 的明确提示
        let c =
            logCheck (Doctor.runLive (noConfig dir, dir, fun () -> Inspect.ofStore host))

        Assert.False(c.ok)
        Assert.Contains(sprintf "commit %d is unknown" failing, c.detail)
        Assert.Contains("Restart the host", c.detail)
        // 之后的提交被拒，不重试
        match create host with
        | CommitFailed(Poisoned m) -> Assert.Contains("read-only", m)
        | r -> failwithf "expected read-only, got %A" r
    finally
        cleanup dir
