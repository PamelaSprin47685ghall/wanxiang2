namespace Wanxiang.UI

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open Wanxiang.Core

// 组合者：按对话保存的临时生成状态（MessageView、Wire 的错误与用量、ConversationSummary 的运行态）与错误卡重试倒计时（Wire 的限流信号）。

/// 临时生成状态按对话保存；导航只选择要显示哪一份，不拥有生成本身。
type ConversationRun = {
    generationId: Guid option
    running: bool
    message: MessageView option
    error: GenerationError option
    usage: GenerationUsage option
    /// 决策 230：中途失败时已流出的半截回复（只在有正文时保留），供「继续生成」接着写。
    partial: MessageView option
}

type ConversationRuns() =
    let empty = { generationId = None; running = false; message = None; error = None; usage = None; partial = None }
    let mutable items: Map<Guid, ConversationRun> = Map.empty
    // 退休台账防同代事件的重复记账（重复 Finish/迟到 Start）。generationId 是 GUID
    // 不复用，台账只需覆盖「同一次连接内可能的重复/迟到投递窗口」——游标协议本身
    // 已按 cursor 去重，这里只是纵深防御。没有上限时每次生成都永久增加一个条目，
    // 长对话用户会让集合单调增长（Phase 0 风险 R2），因此给 FIFO 总量上限。
    let retiredCapacity = 1024
    let mutable retired: Set<Guid * Guid> = Set.empty
    let retiredOrder = Queue<Guid * Guid>()
    let retire (id: Guid) (generationId: Guid) =
        if not (retired.Contains(id, generationId)) then
            retired <- retired.Add(id, generationId)
            retiredOrder.Enqueue(id, generationId)
            while retiredOrder.Count > retiredCapacity do
                let oldest = retiredOrder.Dequeue()
                retired <- retired.Remove oldest

    member _.Get(id: Guid option) = id |> Option.bind items.TryFind |> Option.defaultValue empty

    member this.Snapshot(id: Guid, runtimeState: string, generationId: Guid option) =
        let current = this.Get(Some id)
        let running = ConversationSummary.isGeneratingStatus runtimeState
        let generationId = generationId |> Option.filter (fun id -> id <> Guid.Empty)
        if not (generationId |> Option.exists (fun generation -> retired.Contains(id, generation))) then
            items <- items.Add(id,
                { current with
                    running = running
                    generationId = if running then generationId else None
                    message = if running && current.generationId = generationId then current.message else None
                    error = if running then None else current.error
                    partial = if running then None else current.partial })

    member this.Start(id: Guid, generationId: Guid) =
        let current = this.Get(Some id)
        if current.generationId <> Some generationId && not (retired.Contains(id, generationId)) then
            current.generationId |> Option.iter (fun previous -> retire id previous)
            items <- items.Add(id, { current with generationId = Some generationId; running = true; message = None; error = None; partial = None })

    member this.Delta(id: Guid, generationId: Guid, message: MessageView) =
        let current = this.Get(Some id)
        if current.running && current.generationId = Some generationId then
            items <- items.Add(id, { current with message = Some message })
            true
        else false

    member this.Finish(id: Guid, generationId: Guid, error: GenerationError option, usage: GenerationUsage option) =
        let current = this.Get(Some id)
        let applicable = not (retired.Contains(id, generationId)) && (current.generationId.IsNone || current.generationId = Some generationId)
        retire id generationId
        if applicable then
            items <- items.Add(id,
                { current with generationId = None; running = false; message = None; error = error
                               partial =
                                   (match error, current.message with
                                    | Some _, Some m when m.role = "assistant" && not (String.IsNullOrWhiteSpace m.text) -> Some m
                                    | _ -> None)
                               usage = usage |> Option.orElse current.usage })
            true
        else false

    member this.ClearMessage(id: Guid) =
        items <- items.Add(id, { this.Get(Some id) with message = None })

    member this.ClearError(id: Guid) =
        items <- items.Add(id, { this.Get(Some id) with error = None; partial = None })

    member _.Disconnect() =
        items <- items |> Map.map (fun _ item -> { item with generationId = None; running = false; message = None })

    /// 决策 230：把半截回复编成可落盘的助手消息（去掉尾部空白：Anthropic 拒收以空白结尾的预填）。
    /// 决策 250：主机保存的半截回复（刷新 / 换设备后随观察下发）。正在生成时不覆盖；
    /// 本端已有更晚一次生成的结果（另一 generationId 的错误或流）时也不覆盖。
    member this.RestorePartial(id: Guid, generationId: Guid, message: MessageView, error: GenerationError option) =
        let current = this.Get(Some id)
        if current.running || message.role <> "assistant" || String.IsNullOrWhiteSpace message.text then false
        else
            retire id generationId
            items <- items.Add(id, { current with partial = Some message; error = (error |> Option.orElse current.error) })
            true

    static member PartialMessageJson(partial: MessageView) : JsonObject =
        let node = JsonObject()
        node["text"] <- partial.text.TrimEnd()
        let contents = JsonArray()
        contents.Add node
        let message = JsonObject()
        message["role"] <- "assistant"
        message["contents"] <- contents
        message

    /// 退休台账当前条目数（诊断/测试用：证明 FIFO 淘汰在生效）。
    member _.RetiredCount = retired.Count

    member _.Clear() =
        items <- Map.empty
        retired <- Set.empty
        retiredOrder.Clear()

/// 错误卡的重试倒计时（决策 242）。
///
/// 截止时刻在**收到失败的那一刻**定下，挂在那份错误实例上（引用身份）：卡片每次重渲染
/// 都读同一个截止时刻，倒计时不会因为界面刷新从头再数。来源优先级：
/// `wanxiang.dev/provider.rate-limit` 的等待秒数 → 错误自带的 `retryAfterSeconds`。
/// 不给错误记录加字段：记录形状是既有契约（同 RestoredAttachments 的取舍）。
type RetryCountdownInfo = {
    retryAt: DateTimeOffset
    limit: ProviderRateLimit option
}

module RetryCountdown =
    let private table = System.Runtime.CompilerServices.ConditionalWeakTable<GenerationError, RetryCountdownInfo>()
    /// 已收到、尚未配上 finished 的限流信号（按 generationId）。
    let private pending = System.Collections.Concurrent.ConcurrentDictionary<Guid, ProviderRateLimit * DateTimeOffset>()

    /// 记下某次生成的限流信号（先于 generation.finished 到达）。
    let notice (generationId: Guid) (limit: ProviderRateLimit) (receivedAt: DateTimeOffset) =
        // 防止异常对端只发 notice 不发 finished 让表无限增长。
        if pending.Count > 256 then pending.Clear()
        pending[generationId] <- (limit, receivedAt)

    /// generation.finished 到达：给错误实例定下截止时刻。没有任何等待信号时不登记。
    let attach (generationId: Guid) (error: GenerationError) (now: DateTimeOffset) =
        let limit =
            match pending.TryRemove generationId with
            | true, (limit, receivedAt) -> Some(limit, receivedAt)
            | _ -> None
        let fromLimit =
            limit |> Option.bind (fun (l, at) -> ProviderRateLimit.waitSeconds l |> Option.map (fun s -> at.AddSeconds(float s)))
        let fromError = error.retryAfterSeconds |> Option.filter (fun s -> s > 0) |> Option.map (fun s -> now.AddSeconds(float s))
        match fromLimit |> Option.orElse fromError with
        | Some retryAt when error.retryable ->
            table.AddOrUpdate(error, { retryAt = retryAt; limit = limit |> Option.map fst })
        | _ -> ()

    /// 错误卡读取截止时刻。未登记但错误自带秒数时（直接构造的卡片、旧主机），
    /// 以第一次读取的时刻为起点登记，之后同一实例读到的都是同一个截止时刻。
    let tryGet (error: GenerationError) (now: DateTimeOffset) : RetryCountdownInfo option =
        match table.TryGetValue error with
        | true, info -> Some info
        | _ ->
            match error.retryAfterSeconds with
            | Some s when s > 0 && error.retryable ->
                let info = { retryAt = now.AddSeconds(float s); limit = None }
                table.AddOrUpdate(error, info)
                Some info
            | _ -> None

    /// 剩余整秒（向上取整，不小于 0）。
    let remainingSeconds (info: RetryCountdownInfo) (now: DateTimeOffset) =
        let left = (info.retryAt - now).TotalSeconds
        if left <= 0.0 then 0 else int (Math.Ceiling left)

/// §215 / 决策 366：哪类生成错误给哪个恢复动作。动作与文案归 ErrorRecovery，错误分类归 Wire；这里把两边接起来。
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ErrorRecovery =

    let forKind (kind: GenerationErrorKind) : ErrorRecovery option =
        match kind with
        | ProviderAuthFailed | ModelNotFound -> Some OpenProviderSettings
        // 限流 / 额度用尽：除了等，最有用的是换一个模型继续
        | ProviderRateLimited -> Some SwitchModel
        | ConfigInvalid | ProviderBadRequest -> Some OpenConversationSettings
        | ContextTooLong -> Some StartNewConversation
        | ProviderTimeout | ProviderUnavailable | ContentFiltered | ToolFailed | UnknownFailure -> None
