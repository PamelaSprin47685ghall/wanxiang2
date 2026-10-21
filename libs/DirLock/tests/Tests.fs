module DirLockTests

open System
open System.Diagnostics
open System.IO
open Xunit
open DirLock

let tempDir () =
    Path.Combine(Path.GetTempPath(), "dirlock-" + Guid.NewGuid().ToString("N"))

// SPEC 1：同进程第二次获取失败，释放后可再获取
[<Fact>]
let ``second acquire fails until first is disposed`` () =
    let d = tempDir ()

    let first =
        match DirLock.TryAcquire d with
        | Ok l -> l
        | Error e -> failwith e

    match DirLock.TryAcquire d with
    | Ok _ -> failwith "expected lock conflict"
    | Error msg -> Assert.Contains(string Environment.ProcessId, msg)

    (first :> IDisposable).Dispose()

    match DirLock.TryAcquire d with
    | Ok l -> (l :> IDisposable).Dispose()
    | Error e -> failwith e

// SPEC 2：另一个进程持锁时获取失败；该进程被杀后锁自动释放
[<Fact>]
let ``lock held by another process is released when it dies`` () =
    let d = tempDir ()
    Directory.CreateDirectory d |> ignore
    let lockPath = Path.Combine(d, ".lock")
    // 用 flock 让子进程持有同一文件的独占打开；.NET 在 Unix 上 FileShare.None 即 flock(LOCK_EX)
    if not (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) then
        ()
    else
        let psi = ProcessStartInfo("flock", [| "-x"; lockPath; "sleep"; "30" |])
        use p = Process.Start psi
        let deadline = DateTime.UtcNow.AddSeconds 5.0
        let mutable blocked = false

        while not blocked && DateTime.UtcNow < deadline do
            match DirLock.TryAcquire d with
            | Error _ -> blocked <- true
            | Ok l ->
                (l :> IDisposable).Dispose()
                Threading.Thread.Sleep 50

        Assert.True(blocked, "child process lock not observed")
        p.Kill true
        p.WaitForExit()
        // 子进程树退出与内核释放 flock 之间有短暂窗口，限时重试
        let deadline2 = DateTime.UtcNow.AddSeconds 5.0
        let mutable got = false

        while not got && DateTime.UtcNow < deadline2 do
            match DirLock.TryAcquire d with
            | Ok l ->
                (l :> IDisposable).Dispose()
                got <- true
            | Error _ -> Threading.Thread.Sleep 50

        Assert.True(got, "lock not released after holder died")

// SPEC 3：写 pid 失败不报「被占用」，且释放锁
[<Fact>]
let ``pid write failure is reported distinctly and releases the lock`` () =
    let d = tempDir ()
    Directory.CreateDirectory(Path.Combine(d, ".lock.pid")) |> ignore // 让写 pid 必然失败

    match DirLock.TryAcquire d with
    | Ok _ -> failwith "expected pid write failure"
    | Error msg ->
        Assert.Contains("writing", msg)
        Assert.DoesNotContain("is locked", msg)

    Directory.Delete(Path.Combine(d, ".lock.pid"))

    match DirLock.TryAcquire d with
    | Ok l -> (l :> IDisposable).Dispose()
    | Error e -> failwith e

// SPEC 4：状态有主人
[<Fact>]
let ``failed acquires leave the holder intact; locks in different directories are independent`` () =
    let d1, d2 = tempDir (), tempDir ()

    let first =
        match DirLock.TryAcquire d1 with
        | Ok l -> l
        | Error e -> failwith e

    let pidFile = Path.Combine(d1, ".lock.pid")
    let holder = File.ReadAllText(pidFile).Trim()
    // 反复尝试同一目录都失败；失败不惊动持有者：诊断里的 pid 不变，锁仍被拒
    for _ in 1..2 do
        match DirLock.TryAcquire d1 with
        | Ok _ -> failwith "expected lock conflict"
        | Error msg -> Assert.Contains(holder, msg)

    Assert.Equal(holder, File.ReadAllText(pidFile).Trim())
    // 另一个目录是独立实例，可以同时持有
    let second =
        match DirLock.TryAcquire d2 with
        | Ok l -> l
        | Error e -> failwith e

    Assert.Equal(Path.Combine(d1, ".lock"), first.Path)
    Assert.NotEqual<string>(first.Path, second.Path)
    // 释放其中一个不影响另一个
    (second :> IDisposable).Dispose()

    match DirLock.TryAcquire d2 with
    | Ok l -> (l :> IDisposable).Dispose()
    | Error e -> failwith e

    match DirLock.TryAcquire d1 with
    | Ok _ -> failwith "expected d1 still held"
    | Error _ -> ()

    (first :> IDisposable).Dispose()

    match DirLock.TryAcquire d1 with
    | Ok l -> (l :> IDisposable).Dispose()
    | Error e -> failwith e
