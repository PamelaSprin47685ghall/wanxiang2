namespace Wanxiang.UI

open System

/// 草稿落盘的**节流闸**：打字停下来 800 ms 后才落一次盘。
///
/// 闸的所有权在**宿主侧**（`compose/AppShell`），不在状态树、也不在控件库：
/// - 状态树只管「文本是什么」——它不按键就发一次 `DraftWrite`（那会每个键击写一次 IndexedDB）；
/// - `libs/DraftStore` 的 `DraftStore.Save` 记着上次落盘内容，返回 `Unchanged` 就不写——
///   那是**去重**，不是**节流**，两者都要。
///
/// 这个类型是**纯函数 + 注入时钟**的决策器：它只回答「现在该落盘吗」，
/// 真正的计时器由宿主排（`ShellPorts.After`）。这样它可被确定性地测——不依赖真实时间。
///
/// 放在组合者里而不是独立成库：它只有一个消费者，就是宿主自己的草稿策略，
/// 独立成库只会在库里多出一层引用与一份要单独维护的清单。
/// （决策 391/2026-10-21：原来拆成了 `libs/DraftSaveGate`，按「不做碎片」收回。）
type DraftSaveGate(windowMs: float) =
    let mutable lastSaveAt: DateTimeOffset option = None
    let mutable pending = false
    let mutable pendingText = ""

    /// 节流窗口（毫秒）。
    member _.WindowMs = windowMs

    /// 有新文本要落盘。返回 `true` 表示**现在就该落**，
    /// `false` 表示「已排上尾巴，等窗口到期再落」。
    member this.Request(now: DateTimeOffset, text: string) : bool =
        pendingText <- text
        match lastSaveAt with
        | Some t when (now - t).TotalMilliseconds < windowMs ->
            // 窗口内：这次搭车，不立即落。
            pending <- true
            false
        | _ ->
            // 窗口外：立刻落一次。
            lastSaveAt <- Some now
            pending <- false
            true

    /// 尾巴到期（宿主排的计时器回来了）：落**最后一次**的文本。
    member this.Flush(now: DateTimeOffset) : string option =
        if pending then
            pending <- false
            lastSaveAt <- Some now
            Some pendingText
        else
            None

    /// 宿主刚落过盘（立即落那条）：记下时刻，尾巴作废。
    member this.Saved(now: DateTimeOffset) =
        pending <- false
        lastSaveAt <- Some now
