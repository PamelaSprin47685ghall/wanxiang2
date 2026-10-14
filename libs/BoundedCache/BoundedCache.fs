namespace Wanxiang.UI

open System.Collections.Generic

/// 带上限的记忆化（从旧 RichBackend.Cache 拆出）。按插入先后淘汰最早的键。
/// 长对话里代码块会被反复重渲染（流式追加、主题切换、滚动复用），不缓存就会把 JS 引擎当成每帧都要付费的热路径。
/// 线程安全：读发生在 UI 线程，写可能来自后台着色线程。
type BoundedCache<'K, 'V when 'K: equality>(limit: int) =
    let map = Dictionary<'K, 'V>()
    let order = Queue<'K>()
    let gate = obj ()

    let insert key value =
        map[key] <- value
        order.Enqueue key
        while order.Count > limit do
            map.Remove(order.Dequeue()) |> ignore

    member _.TryGet(key: 'K) : 'V option =
        lock gate (fun () ->
            match map.TryGetValue key with
            | true, hit -> Some hit
            | _ -> None)

    member _.Set(key: 'K, value: 'V) = lock gate (fun () -> insert key value)

    member _.GetOrAdd(key: 'K, compute: 'K -> 'V) : 'V =
        match lock gate (fun () ->
                  match map.TryGetValue key with
                  | true, hit -> Some hit
                  | _ -> None)
            with
        | Some hit -> hit
        | None ->
            // compute 在锁外执行：着色可能要几百毫秒，占着锁会挡住 UI 线程的读
            let value = compute key
            lock gate (fun () -> insert key value)
            value

    member _.Clear() =
        lock gate (fun () ->
            map.Clear()
            order.Clear())
