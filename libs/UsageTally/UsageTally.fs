namespace Wanxiang.Server

open System
open Microsoft.Extensions.AI

/// 一次生成里逐轮累加的 token 用量（从旧 ChatOrchestrator.addUsage 拆出）。
/// 工具循环每一轮 Provider 调用都要计入，否则用户只看到最后一轮的开销。
/// 某项只要有一轮报告过就有值；从没报告过保持 None（不当成 0）。
type UsageTally() =
    let mutable prompt: int option = None
    let mutable completion: int option = None
    let mutable cached: int option = None
    let mutable total: int option = None

    static let sum (a: int option) (b: int option) =
        match a, b with
        | Some x, Some y -> Some(x + y)
        | Some x, None -> Some x
        | None, Some y -> Some y
        | None, None -> None

    static let ofNullable (n: Nullable<int64>) = if n.HasValue then Some(int n.Value) else None

    /// 计入一轮的用量。
    member _.Add(u: UsageDetails) =
        prompt <- sum prompt (ofNullable u.InputTokenCount)
        completion <- sum completion (ofNullable u.OutputTokenCount)
        cached <- sum cached (ofNullable u.CachedInputTokenCount)
        total <- sum total (ofNullable u.TotalTokenCount)

    member _.PromptTokens = prompt
    member _.CompletionTokens = completion
    member _.CachedTokens = cached
    member _.TotalTokens = total
