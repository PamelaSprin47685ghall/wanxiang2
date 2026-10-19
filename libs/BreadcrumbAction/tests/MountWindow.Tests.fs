module MountWindowTests
open System
open System.IO
open Xunit
open Wanxiang.UI


let msgs = MountWindowSamples.msgs

[<Fact>]
let ``only the tail window is mounted`` () =
    let w = MountWindow(40, 60)
    Assert.Equal(60, w.Plan(msgs 0 100 false, false))
    Assert.Equal(60, w.HiddenCount)
    Assert.Equal(Some 59UL, Array.tryLast w.Hidden)

[<Fact>]
let ``expand steps up until nothing is hidden`` () =
    let w = MountWindow(40, 60)
    w.Plan(msgs 0 100 false, false) |> ignore
    Assert.True(w.Expand())
    Assert.Equal(0, w.Plan(msgs 0 100 false, false))
    Assert.False(w.Expand())

[<Fact>]
let ``ensure mounts a hidden target with a margin`` () =
    let w = MountWindow(40, 60)
    w.Plan(msgs 0 100 false, false) |> ignore
    Assert.True(w.EnsureMounted 10UL)
    Assert.Equal(6, w.Plan(msgs 0 100 false, false))
    Assert.False(w.EnsureMounted 99UL)

[<Fact>]
let ``an older page arriving during a prepend is mounted whole`` () =
    let w = MountWindow(40, 60)
    w.Plan(msgs 50 50 false, false) |> ignore
    // 原先挂着的 40 张加上整页 50 张，只剩原先就隐藏的 10 张
    Assert.Equal(10, w.Plan(msgs 0 100 false, true))
    // 没有前置在途时同样的到达只按窗口挂
    let v = MountWindow(40, 60)
    v.Plan(msgs 50 50 false, false) |> ignore
    Assert.Equal(60, v.Plan(msgs 0 100 false, false))
