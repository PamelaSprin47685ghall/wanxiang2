namespace FailureLimiter

open System
open System.Collections.Generic

/// 按键（例如远端地址）计失败：窗口内失败达到 maxFailures 次就冻结 freeze 时长。
/// 私有状态：每键的失败时间与冻结期限。条目数超过阈值时清理过期条目，内存有界。线程安全。
type FailureLimiter(window: TimeSpan, maxFailures: int, freeze: TimeSpan, ?pruneThreshold: int) =
    do if maxFailures < 1 then invalidArg (nameof maxFailures) "must be >= 1"
    let pruneThreshold = defaultArg pruneThreshold 1024
    let gate = obj ()
    let failures = Dictionary<string, Queue<DateTimeOffset>>()
    let frozenUntil = Dictionary<string, DateTimeOffset>()

    let trim (now: DateTimeOffset) (q: Queue<DateTimeOffset>) =
        while q.Count > 0 && q.Peek() <= now - window do q.Dequeue() |> ignore

    let prune (now: DateTimeOffset) =
        for kv in Seq.toArray failures do
            trim now kv.Value
            if kv.Value.Count = 0 then failures.Remove kv.Key |> ignore
        for kv in Seq.toArray frozenUntil do
            if now >= kv.Value then frozenUntil.Remove kv.Key |> ignore

    member _.Window = window
    member _.MaxFailures = maxFailures
    member _.Freeze = freeze

    /// 冻结到何时；未冻结为 None。
    member _.FrozenUntil(now: DateTimeOffset, key: string) : DateTimeOffset option =
        lock gate (fun () ->
            match frozenUntil.TryGetValue key with
            | true, u when now < u -> Some u
            | _ -> None)

    member this.IsFrozen(now: DateTimeOffset, key: string) : bool = (this.FrozenUntil(now, key)).IsSome

    /// 记一次失败；返回 true 表示这次触发了冻结。
    member _.RecordFailure(now: DateTimeOffset, key: string) : bool =
        lock gate (fun () ->
            if failures.Count + frozenUntil.Count >= pruneThreshold then prune now
            let q =
                match failures.TryGetValue key with
                | true, q -> q
                | _ ->
                    let q = Queue()
                    failures[key] <- q
                    q
            trim now q
            q.Enqueue now
            if q.Count >= maxFailures then
                frozenUntil[key] <- now + freeze
                failures.Remove key |> ignore
                true
            else false)

    /// 清除这个键的失败与冻结（例如它成功了）。
    member _.Clear(key: string) : unit =
        lock gate (fun () ->
            failures.Remove key |> ignore
            frozenUntil.Remove key |> ignore)

    /// 正在跟踪的键数（诊断与有界性证据）。
    member _.TrackedKeys : int = lock gate (fun () -> failures.Count + frozenUntil.Count)
