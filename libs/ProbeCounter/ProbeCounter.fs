namespace Wanxiang.UI

open System.Collections.Generic

/// 决策 450：生命周期一致性探针的命中计数，迁自旧 `Model/ParityProbe.fs`。
/// 只记「这条生命周期路径真的跑到了」，不带任何用户内容。默认关闭，关闭时零开销（一次布尔判断）。
/// 打开条件（环境变量 / 页面参数）与输出（stderr / `globalThis.__wxProbe`）碰平台，归宿主：宿主在 Hit 返回 true（首次命中）时写一行。
type ProbeCounter() =
    let counts = Dictionary<string, int>()

    /// 是否计数。宿主按平台开关设置；测试可直接打开。
    member val Enabled = false with get, set

    /// 记录一次命中；返回是否为该探点的首次命中。关闭时什么也不记，返回 false。
    member this.Hit(name: string) : bool =
        this.Enabled
        && lock counts (fun () ->
            match counts.TryGetValue name with
            | true, n ->
                counts[name] <- n + 1
                false
            | _ ->
                counts[name] <- 1
                true)

    member _.Count(name: string) =
        lock counts (fun () ->
            match counts.TryGetValue name with
            | true, n -> n
            | _ -> 0)

    member _.Reset() = lock counts (fun () -> counts.Clear())
