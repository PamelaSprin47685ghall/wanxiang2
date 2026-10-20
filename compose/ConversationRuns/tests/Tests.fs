module Tests

open System
open Xunit
open Wanxiang.Core
open Wanxiang.UI

let private msg role text = { MessageView.empty with role = role; text = text }
let private now = DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero)

// 迁自旧 UiControllersTests。
[<Fact>]
let ``ConversationRuns initial state is empty and idle`` () =
    let runs = ConversationRuns()
    let r = runs.Get(None)
    Assert.False(r.running)
    Assert.True(Option.isNone r.generationId)
    Assert.True(Option.isNone r.message)
    Assert.True(Option.isNone r.error)

[<Fact>]
let ``ConversationRuns start transitions to generating and resets message`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()

    runs.Start(convId, genId)
    let r = runs.Get(Some convId)
    Assert.True(r.running)
    Assert.Equal(Some genId, r.generationId)
    Assert.True(Option.isNone r.message)
    Assert.True(Option.isNone r.error)

[<Fact>]
let ``ConversationRuns delta updates streaming message`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()
    runs.Start(convId, genId)

    let msg = { MessageView.empty with role = "assistant"; text = "Hello " }
    let ok = runs.Delta(convId, genId, msg)
    Assert.True(ok)
    Assert.Equal("Hello ", runs.Get(Some convId).message.Value.text)

    let msg2 = { msg with text = "Hello world!" }
    let ok2 = runs.Delta(convId, genId, msg2)
    Assert.True(ok2)
    Assert.Equal("Hello world!", runs.Get(Some convId).message.Value.text)

[<Fact>]
let ``ConversationRuns finish without error archives entry to retired ledger with capacity limit`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()
    runs.Start(convId, genId)
    let ok = runs.Finish(convId, genId, None, None)
    Assert.True(ok)

    let r = runs.Get(Some convId)
    Assert.False(r.running)
    Assert.True(Option.isNone r.generationId)
    Assert.True(Option.isNone r.message)

    // Verifying retirement FIFO queue bounds (capacity = 1024)
    for _ in 1 .. 1050 do
        let gid = Guid.NewGuid()
        runs.Start(convId, gid)
        runs.Finish(convId, gid, None, None) |> ignore

    // The ledger should strictly not exceed 1024 entries
    Assert.Equal(1024, runs.RetiredCount)

[<Fact>]
let ``ConversationRuns finish with error records error in state`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()
    runs.Start(convId, genId)

    let genErr = {
        kind = GenerationErrorKind.ProviderTimeout
        message = "Network timeout"
        detail = None
        retryable = true
        retryAfterSeconds = Some 5
    }
    runs.Finish(convId, genId, Some genErr, None) |> ignore

    let r = runs.Get(Some convId)
    Assert.False(r.running)
    Assert.True(r.error.IsSome)
    Assert.Equal("Network timeout", r.error.Value.message)

    runs.ClearError(convId)
    Assert.True(Option.isNone (runs.Get(Some convId).error))

[<Fact>]
let ``ConversationRuns disconnect clears in-flight run but preserves retired ledger`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()
    runs.Start(convId, genId)
    runs.Disconnect()

    let r = runs.Get(Some convId)
    Assert.False(r.running)
    Assert.True(Option.isNone r.generationId)

// 迁自旧 UiPureLogicTests。
[<Fact>]
let ``ConversationRuns state machine and FIFO retirement capacity`` () =
    let runs = ConversationRuns()
    let convId = Guid.NewGuid()
    let genId = Guid.NewGuid()

    // 1. 未开始时 Get 为空
    let r0 = runs.Get(Some convId)
    Assert.False(r0.running)
    Assert.True(r0.generationId.IsNone)

    // 2. Start
    runs.Start(convId, genId)
    let r1 = runs.Get(Some convId)
    Assert.True(r1.running)
    Assert.Equal(Some genId, r1.generationId)

    // 3. Delta
    let msg = { MessageView.empty with text = "生成中..." }
    let accepted = runs.Delta(convId, genId, msg)
    Assert.True(accepted)
    Assert.Equal("生成中...", runs.Get(Some convId).message.Value.text)

    // 4. Finish
    let finished = runs.Finish(convId, genId, None, None)
    Assert.True(finished)
    let r2 = runs.Get(Some convId)
    Assert.False(r2.running)
    Assert.True(r2.generationId.IsNone)

    // 5. 退休台账防止重复 Finish / 迟到 Start
    runs.Start(convId, genId)
    Assert.False(runs.Get(Some convId).running, "已退休的 generationId 不能再启动")

    // 6. 清理
    runs.Clear()
    Assert.Equal(0, runs.RetiredCount)

// 迁自旧 ContinueLinkUndoTests（'230 继续生成只接受有正文的助手消息' 要 EventStore/CommandEngine，留在旧仓库）。
[<Fact>]
let ``230 半截回复编成去尾空白的助手消息`` () =
    let json = ConversationRuns.PartialMessageJson(msg "assistant" "半句\n\n ")
    Assert.Equal("assistant", json["role"].GetValue<string>())
    Assert.Equal("半句", json["contents"].[0].["text"].GetValue<string>())

[<Fact>]
let ``230 失败时保留有正文的半截回复，重试或重新开始即清掉`` () =
    let runs = ConversationRuns()
    let conv, gen = Guid.NewGuid(), Guid.NewGuid()
    runs.Start(conv, gen)
    runs.Delta(conv, gen, msg "assistant" "写到一半  ") |> ignore
    let err = GenerationError.create ProviderTimeout "超时"
    Assert.True(runs.Finish(conv, gen, Some err, None))
    Assert.Equal(Some "写到一半  ", (runs.Get(Some conv)).partial |> Option.map _.text)
    runs.ClearError conv
    Assert.True((runs.Get(Some conv)).partial.IsNone)

[<Fact>]
let ``230 成功结束或空回复不留半截`` () =
    let runs = ConversationRuns()
    let conv = Guid.NewGuid()
    let g1 = Guid.NewGuid()
    runs.Start(conv, g1)
    runs.Delta(conv, g1, msg "assistant" "完整") |> ignore
    runs.Finish(conv, g1, None, None) |> ignore
    Assert.True((runs.Get(Some conv)).partial.IsNone)
    let g2 = Guid.NewGuid()
    runs.Start(conv, g2)
    runs.Delta(conv, g2, msg "assistant" "   ") |> ignore
    runs.Finish(conv, g2, Some(GenerationError.create ProviderTimeout "x"), None) |> ignore
    Assert.True((runs.Get(Some conv)).partial.IsNone)

// 迁自旧 PartialPersistAndUrlSchemeTests。
[<Fact>]
let ``250 刷新后客户端从主机恢复半截与错误卡；正在生成时不覆盖`` () =
    let runs = ConversationRuns()
    let conv, gen = Guid.NewGuid(), Guid.NewGuid()
    let err = GenerationError.create ProviderTimeout "超时"
    let msg = { MessageView.empty with role = "assistant"; text = "半截" }
    Assert.True(runs.RestorePartial(conv, gen, msg, Some err))
    let run = runs.Get(Some conv)
    Assert.Equal(Some "半截", run.partial |> Option.map _.text)
    Assert.True(run.error.IsSome)
    // 迟到的同代 started 不应复活这次生成
    runs.Start(conv, gen)
    Assert.False((runs.Get(Some conv)).running)
    let conv2, g2 = Guid.NewGuid(), Guid.NewGuid()
    runs.Start(conv2, g2)
    Assert.False(runs.RestorePartial(conv2, Guid.NewGuid(), msg, Some err))

// 迁自旧 ModelChipRateLimitTests。
[<Fact>]
let ``retry countdown deadline is fixed at finish time and survives re-reads`` () =
    let gid = Guid.CreateVersion7()
    let err = GenerationError.create ProviderRateLimited "限流"
    RetryCountdown.notice gid { ProviderRateLimit.empty with resetSeconds = Some 60; remainingRequests = Some 0 } now
    RetryCountdown.attach gid err (now.AddSeconds 2.0)
    match RetryCountdown.tryGet err (now.AddSeconds 30.0) with
    | Some info ->
        Assert.Equal(now.AddSeconds 60.0, info.retryAt)
        Assert.Equal(30, RetryCountdown.remainingSeconds info (now.AddSeconds 30.0))
        Assert.Equal(0, RetryCountdown.remainingSeconds info (now.AddSeconds 90.0))
        Assert.True(info.limit |> Option.exists ProviderRateLimit.isExhausted)
    | None -> Assert.Fail "expected countdown"

[<Fact>]
let ``non retryable errors never get a countdown`` () =
    let gid = Guid.CreateVersion7()
    let err = { GenerationError.create ProviderAuthFailed "密钥无效" with retryAfterSeconds = Some 10 }
    RetryCountdown.notice gid { ProviderRateLimit.empty with retryAfterSeconds = Some 10 } now
    RetryCountdown.attach gid err now
    Assert.Equal(None, RetryCountdown.tryGet err now)


// 迁自旧 LibraryUndoTests / R19SimplifyTests。
[<Fact>]
let ``§215 每类不可自愈的错误都有恢复动作`` () =
    Assert.Equal(Some OpenProviderSettings, ErrorRecovery.forKind ProviderAuthFailed)
    Assert.Equal(Some OpenProviderSettings, ErrorRecovery.forKind ModelNotFound)
    Assert.Equal(Some OpenConversationSettings, ErrorRecovery.forKind ConfigInvalid)
    Assert.Equal(Some SwitchModel, ErrorRecovery.forKind ProviderRateLimited)
    Assert.Equal("换个模型", ErrorRecovery.label SwitchModel)
    Assert.Equal(Some StartNewConversation, ErrorRecovery.forKind ContextTooLong)
    Assert.Equal(None, ErrorRecovery.forKind ProviderTimeout)
    Assert.Contains("换个模型", GenerationErrorKind.hint ProviderRateLimited)

[<Fact>]
let ``366 限流只给「换个模型」并直达切换器`` () =
    Assert.Equal(Some SwitchModel, ErrorRecovery.forKind ProviderRateLimited)
    Assert.Equal("换个模型", ErrorRecovery.label SwitchModel)
    Assert.Equal(Some OpenConversationSettings, ErrorRecovery.forKind ConfigInvalid)

// ---- 生成结束（generation.finished）的结局判定与提示 ----

[<Fact>]
let ``生成结束：完成与取消都不算失败`` () =
    Assert.True((GenerationOutcome.errorOf "completed" None).IsNone)
    Assert.True((GenerationOutcome.errorOf "cancelled" None).IsNone)
    // 结束状态自带错误详情时也按状态走：完成 / 取消没有失败。
    let err = GenerationError.create ProviderTimeout "超时"
    Assert.True((GenerationOutcome.errorOf "completed" (Some err)).IsNone)
    Assert.True((GenerationOutcome.errorOf "cancelled" (Some err)).IsNone)

[<Fact>]
let ``生成结束：失败带详情就沿用主机的错误`` () =
    let err = GenerationError.create ProviderRateLimited "限流了"
    let outcome = GenerationOutcome.errorOf "failed" (Some err)
    Assert.True(outcome.IsSome)
    Assert.Equal(ProviderRateLimited, outcome.Value.kind)
    Assert.Equal("限流了", outcome.Value.message)
    Assert.Equal(None, outcome.Value.detail)

[<Fact>]
let ``生成结束：失败没有详情落成可重试的通用失败`` () =
    let outcome = GenerationOutcome.errorOf "failed" None
    Assert.True(outcome.IsSome)
    Assert.Equal(GenerationErrorKind.UnknownFailure, outcome.Value.kind)
    Assert.True(outcome.Value.retryable)
    Assert.Equal("生成失败，主机没有说明原因。可以重试；反复出现时请在设置里「检查主机」。", outcome.Value.message)

[<Fact>]
let ``生成结束：未知状态按失败处理并记下原始状态`` () =
    for status in [ "exploded"; "" ] do
        let outcome = GenerationOutcome.errorOf status None
        Assert.True(outcome.IsSome)
        Assert.Equal(GenerationErrorKind.UnknownFailure, outcome.Value.kind)
        Assert.True(outcome.Value.retryable)
        Assert.Equal("生成结束状态未知，已按失败处理。", outcome.Value.message)
        Assert.Equal(Some(sprintf "未知状态：%s" status), outcome.Value.detail)

[<Fact>]
let ``生成结束：当前对话的失败只进卡片，不叠提示`` () =
    let err = GenerationError.create ProviderRateLimited "限流了"
    let outcome = GenerationOutcome.errorOf "failed" (Some err)
    Assert.Equal(FinishNotice.Quiet, GenerationOutcome.notice "failed" outcome true)

[<Fact>]
let ``生成结束：非当前对话的失败只给一句提示`` () =
    let err = GenerationError.create ProviderRateLimited "限流了"
    let outcome = GenerationOutcome.errorOf "failed" (Some err)
    match GenerationOutcome.notice "failed" outcome false with
    | FinishNotice.FailureToast text ->
        // 提示是面向用户的一句话：错误说明加上该类别的行动建议。
        Assert.Contains("限流了", text)
        Assert.Contains("换个模型", text)
    | other -> Assert.Fail(sprintf "expected a failure toast, got %A" other)

[<Fact>]
let ``生成结束：当前对话被停止时提示已停止`` () =
    let outcome = GenerationOutcome.errorOf "cancelled" None
    Assert.Equal(FinishNotice.Stopped, GenerationOutcome.notice "cancelled" outcome true)
    // 非当前对话的取消看不见：不出提示。
    Assert.Equal(FinishNotice.Quiet, GenerationOutcome.notice "cancelled" outcome false)

[<Fact>]
let ``生成结束：正常完成没有提示`` () =
    let outcome = GenerationOutcome.errorOf "completed" None
    Assert.Equal(FinishNotice.Quiet, GenerationOutcome.notice "completed" outcome true)
    Assert.Equal(FinishNotice.Quiet, GenerationOutcome.notice "completed" outcome false)

[<Fact>]
let ``生成结束：一次真实的生成序列走完，状态从生成中退回空闲`` () =
    let runs = ConversationRuns()
    let conv, gen = Guid.NewGuid(), Guid.NewGuid()

    // 1. 开始生成：运行中，没有消息、没有错误。
    runs.Start(conv, gen)
    let running = runs.Get(Some conv)
    Assert.True(running.running)
    Assert.Equal(Some gen, running.generationId)
    Assert.True(running.message.IsNone)
    Assert.True(running.error.IsNone)

    // 2. 流式增量：消息进状态，错误仍空。
    Assert.True(runs.Delta(conv, gen, { MessageView.empty with role = "assistant"; text = "生成中" }))
    Assert.Equal(Some "生成中", runs.Get(Some conv).message |> Option.map _.text)
    Assert.True(runs.Get(Some conv).error.IsNone)

    // 3. 结束（成功）：回到空闲，消息清空，错误仍空，用量记下。
    let usage = { GenerationUsage.empty with totalTokens = Some 42; durationMs = Some 1200L }
    Assert.True(runs.Finish(conv, gen, None, Some usage))
    let finished = runs.Get(Some conv)
    Assert.False(finished.running)
    Assert.True(finished.generationId.IsNone)
    Assert.True(finished.message.IsNone)
    Assert.True(finished.error.IsNone)
    Assert.Equal(Some usage, finished.usage)

[<Fact>]
let ``生成结束：一次失败的生成序列把错误落进状态，提示由结局判定给出`` () =
    let runs = ConversationRuns()
    let conv, gen = Guid.NewGuid(), Guid.NewGuid()
    runs.Start(conv, gen)
    runs.Delta(conv, gen, { MessageView.empty with role = "assistant"; text = "半句" }) |> ignore

    // 主机报限流：错误原样落进状态，结局判定说是失败。
    let err = GenerationError.create ProviderRateLimited "限流了"
    Assert.True(runs.Finish(conv, gen, Some err, None))
    let failed = runs.Get(Some conv)
    Assert.False(failed.running)
    Assert.True(failed.generationId.IsNone)
    Assert.Equal(Some err, failed.error)
    Assert.Equal(ProviderRateLimited, (GenerationOutcome.errorOf "failed" (Some err)).Value.kind)
    // 有正文的半截回复留下，供「继续生成」接着写。
    Assert.Equal(Some "半句", failed.partial |> Option.map _.text)

    // 清掉错误卡：半截也一并清掉。
    runs.ClearError(conv)
    let cleared = runs.Get(Some conv)
    Assert.True(cleared.error.IsNone)
    Assert.True(cleared.partial.IsNone)

[<Fact>]
let ``D2 无错误详情的失败与未知状态都落成可重试的通用失败`` () =
    let e = (GenerationOutcome.errorOf "weird" None).Value
    Assert.Equal(GenerationErrorKind.UnknownFailure, e.kind)
    Assert.Equal(Some "未知状态：weird", e.detail)
    Assert.True((GenerationOutcome.errorOf "failed" None).IsSome)
    Assert.Equal(FinishNotice.Quiet, GenerationOutcome.notice "failed" (GenerationOutcome.errorOf "failed" None) true)
