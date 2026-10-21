module Wanxiang.Tests.AppShellTests.DraftSaveGateTests

open System
open Xunit
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.AppShell.Tests

let private t0 = DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero)
let private ms (n: float) = t0.AddMilliseconds n

/// 窗口外的第一次：立刻落。
[<Fact>]
let ``窗口外的第一次请求立刻落盘`` () =
    let gate = DraftSaveGate(800.0)
    Assert.True(gate.Request(t0, "a"))

/// 窗口内的后续：搭车，不立刻落。
[<Fact>]
let ``窗口内的请求搭车不立刻落`` () =
    let gate = DraftSaveGate(800.0)
    gate.Request(t0, "a") |> ignore
    Assert.False(gate.Request(ms 100.0, "ab"))
    Assert.False(gate.Request(ms 300.0, "abc"))

/// 尾巴到期：落**最后一次**的文本。
[<Fact>]
let ``尾巴到期落最后一次的文本`` () =
    let gate = DraftSaveGate(800.0)
    gate.Request(t0, "a") |> ignore
    gate.Request(ms 100.0, "ab") |> ignore
    gate.Request(ms 300.0, "abc") |> ignore
    Assert.Equal(Some "abc", gate.Flush(ms 800.0))

/// 尾巴到期但期间没新文本：不重复落。
[<Fact>]
let ``没有待落文本时尾巴不产出内容`` () =
    let gate = DraftSaveGate(800.0)
    gate.Request(t0, "a") |> ignore
    Assert.Equal(None, gate.Flush(ms 800.0))

/// 窗口过后又来新的：立刻落。
[<Fact>]
let ``窗口过后再来请求立刻落`` () =
    let gate = DraftSaveGate(800.0)
    gate.Request(t0, "a") |> ignore
    Assert.True(gate.Request(ms 900.0, "b"))

/// 连续打字只落一次（窗口内搭车，尾巴落最终文本）。
[<Fact>]
let ``连续打字只落一次且内容是最终文本`` () =
    let gate = DraftSaveGate(800.0)
    gate.Request(t0, "a") |> ignore

    for i in 1..9 do
        gate.Request(ms (float i * 50.0), "a" + string i) |> ignore

    Assert.Equal(Some "a9", gate.Flush(ms 800.0))

/// 本机草稿的**恢复**（2026-10-21 PWA 冒烟暴露）。
///
/// 之前的 PWA 冒烟断言是「重载后草稿快照里还有那段文本」——那只证明了**持久化**，
/// 没证明它被灌回了输入框：快照本来就没被清掉，重载后当然还在。
/// 于是「重开页面草稿回到输入框」这条一直是假通过。
///
/// 真判据：恢复之后往输入区**追加**一段后缀，落盘的草稿必须是「原文 + 后缀」；
/// 若没恢复，后缀会替换原文，快照就只剩后缀。
[<Fact>]
let ``本机草稿在启动后被恢复到输入区`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Fake.SetLocalDraft(Some "上次的草稿")
        rig.Shell.Build() |> ignore
        // 恢复是**异步**的（读的是宿主的落点），等它落地。
        // 恢复是异步的：读盘 → 回到 UI 线程派发。测试要**泵**调度器，不能靠睡。
        let mutable vs = Workspace.project rig.Shell.Model

        for _ in 1..50 do
            if String.IsNullOrEmpty vs.Composer.Text then
                Threading.Thread.Sleep 10
                Avalonia.Threading.Dispatcher.UIThread.RunJobs()
                vs <- Workspace.project rig.Shell.Model

        Assert.Equal("上次的草稿", vs.Composer.Text))

/// 输入区**已经有字**时，旧草稿不该盖掉用户这次打的字。
[<Fact>]
let ``输入区已有文本时旧草稿不覆盖`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.TextChanged "这次打的"))) |> ignore
        rig.Fake.SetLocalDraft(Some "上次的草稿")
        rig.Shell.Build() |> ignore
        let mutable vs = Workspace.project rig.Shell.Model

        for _ in 1..50 do
            Threading.Thread.Sleep 10
            Avalonia.Threading.Dispatcher.UIThread.RunJobs()
            vs <- Workspace.project rig.Shell.Model

        Assert.Equal("这次打的", vs.Composer.Text))
