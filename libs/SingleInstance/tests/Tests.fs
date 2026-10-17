module Tests

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

// TODO: tryForward / listen 需要真实 Unix 套接字与运行中的监听进程，
// 属 IO 部分，留待 Long Stroke 或宿主集成测试覆盖。
