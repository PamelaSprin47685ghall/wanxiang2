module SingleInstanceTests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``socketPath 拼接配置目录与固定文件名`` () =
    Assert.Equal(Path.Combine("/tmp", "ui.sock"), SingleInstance.socketPath "/tmp")

[<Fact>]
let ``socketPath 空目录得到相对文件名`` () =
    Assert.Equal("ui.sock", SingleInstance.socketPath "")

[<Fact>]
let ``socketPath 带尾分隔符的目录不重复`` () =
    Assert.Equal(Path.Combine("/tmp", "ui.sock"), SingleInstance.socketPath "/tmp/")

let private tempDir () =
    let d =
        Path.Combine(Path.GetTempPath(), "wanxiang-si-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory d |> ignore
    d

/// 回调在监听循环里跑；ack 先于回调写出，所以这里轮询等它落地。
let private waitFor (timeoutMs: int) (pred: unit -> bool) =
    let sw = Diagnostics.Stopwatch.StartNew()
    let mutable ok = false

    while not ok && sw.ElapsedMilliseconds < int64 timeoutMs do
        ok <- pred ()

        if not ok then
            Threading.Thread.Sleep 20

    ok

// SPEC 3：真实的监听进程 + 真实的 Unix 套接字
[<Fact>]
let ``listen 收到转发的链接并回 ok；空行只当唤起；Dispose 后不再有人接`` () =
    let dir = tempDir ()
    let path = SingleInstance.socketPath dir
    let links = ResizeArray<string>()
    let activates = ref 0
    use l = SingleInstance.listen path (fun u -> links.Add u) (fun () -> incr activates)
    // 非空行：链接交给 onLink，tryForward 拿到 "ok" 才返回 true
    Assert.True(SingleInstance.tryForward path (Some "wanxiang://open?id=abc"))
    Assert.True(waitFor 2000 (fun () -> links.Count = 1))
    Assert.Equal("wanxiang://open?id=abc", links[0])
    // 空行：唤起窗口，不产生链接
    Assert.True(SingleInstance.tryForward path None)
    Assert.True(waitFor 2000 (fun () -> !activates = 1))
    Assert.Equal(1, links.Count)
    // 换行会被剥掉（转发方清洗，监听方再 Trim）
    Assert.True(SingleInstance.tryForward path (Some "wanxiang://x\n"))
    Assert.True(waitFor 2000 (fun () -> links.Count = 2))
    Assert.Equal("wanxiang://x", links[1])
    // Dispose：套接字文件清掉，再也没人接
    l.Dispose()
    Assert.False(File.Exists path)
    Assert.False(SingleInstance.tryForward path (Some "wanxiang://y"))

[<Fact>]
let ``没有监听者时 tryForward 一律 false`` () =
    let dir = tempDir ()
    let path = SingleInstance.socketPath dir
    // 文件不存在
    Assert.False(SingleInstance.tryForward path (Some "wanxiang://open"))
    // 有同名文件但不是套接字（陈旧残留）：连不上，仍 false
    File.WriteAllText(path, "stale")
    Assert.False(SingleInstance.tryForward path (Some "wanxiang://open"))
