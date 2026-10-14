namespace Wanxiang.UI

open System
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client

/// 分派给宿主的提示语气（宿主映射到 ToastTone）。
[<RequireQualifiedAccess>]
type DispatchTone =
    | Info
    | Success
    | Warning
    | Failure

/// 本组合者驱动的模型参与者。实例归宿主（别处也用），这里只按事件给它们发消息。
type ProtocolParts = {
    State: ClientState
    Runs: ConversationRuns
    Outbox: MessageOutbox
    Commands: CommandFeedbackTracker
    Configs: ConfigFeedbackTracker
    Downloads: DownloadBuffers
    Search: SearchInFlight
    Probes: PendingProbes
}

/// 尚在宿主的界面面（侧栏、设置页、对话视图、输入区、浮层）。都是事实通知，不是步骤脚本。
type ProtocolSurface = {
    /// 当前打开的对话。
    ActiveConversation: unit -> Guid option
    /// 当前实例 id（发件箱按实例分）。
    InstanceId: unit -> string
    /// 发回主机（字节由 WsClient 编码，与旧代码同一条路径）。
    Send: WireEvent -> unit
    /// 重画当前对话。
    Render: unit -> unit
    Toast: string -> DispatchTone -> unit
    /// 对话列表需要重新观察（旧 observeConversationList）。
    ObserveList: unit -> unit
    /// 对话已就绪，这条发件箱消息可以发出（旧 DispatchPendingMessage）。
    DispatchPending: PendingMessage -> unit
    /// 下载拼好了。
    Downloaded: string -> byte[] -> unit
    /// 附件 blob 缺失。
    MissingAttachment: string -> unit
    /// 模型部分处理完之后，事件交给宿主界面面（认证、配对、目录、健康、设备、审批、
    /// 探测、列表快照、历史页、已接受的搜索结果、导出、能力表、附件上传）。
    Apply: WireEvent -> unit
}

/// 旧 AppShell.HandleEvent：协议事件 → 模型组合者与宿主界面面。
/// 认证成功的步骤序列（切实例、重置、重新请求目录）仍在宿主的 Apply 里。
type ProtocolDispatch(parts: ProtocolParts, surface: ProtocolSurface) =
    let isActive (convId: Guid) = surface.ActiveConversation() = Some convId

    member _.Handle(ev: WireEvent) : unit =
        match ev with
        // ADR-001 方案 B：Hello/UpgradeRequired 无生产路径，不改变任何状态。
        | Hello _ | UpgradeRequired _ -> ()
        | ToolApprovalCapability _ | ToolApprovalDecision _ | ConfigUpdateTools _ | DoctorRequest _ -> ()
        | ConfigChanged _ ->
            surface.Send CatalogRequest
            surface.Apply ev
        | ProviderProbeResult d ->
            parts.Probes.Resolve d.requestId
            surface.Apply ev
        | ConfigApplied d ->
            let tracked = parts.Configs.Resolve d.requestId
            tracked |> Option.iter (fun feedback -> feedback.onCompleted d.ok)
            // 空文案 = 调用方自己会说话，这里不叠 toast。
            let notice, refetch = ConfigApplied.notice (tracked |> Option.map _.successMessage) d.ok (List.ofSeq d.errors)
            match notice with
            | ConfigAppliedNotice.Success text -> surface.Toast text DispatchTone.Success
            | ConfigAppliedNotice.Failure text -> surface.Toast text DispatchTone.Failure
            | ConfigAppliedNotice.Silent -> ()
            if refetch then surface.Send CatalogRequest
        | ConversationListSnapshot _ ->
            parts.State.Handle ev
            parts.State.AdvanceCursor()
            surface.Apply ev
        | ConversationSnapshot d ->
            parts.State.Handle ev
            parts.State.AdvanceCursor()
            parts.Runs.Snapshot(d.conversationId, d.runtimeState, d.generationId)
            surface.Apply ev
            let instanceId = surface.InstanceId()
            parts.Outbox.ConversationReady(instanceId, d.conversationId)
            for item in parts.Outbox.Items(instanceId, Some d.conversationId) do
                if item.state = PreparingConversation then surface.DispatchPending item
            if isActive d.conversationId then surface.Render()
        | MessageCommitted d ->
            parts.State.Handle ev
            parts.State.AdvanceCursor()
            let committed = MessageView.ofJson d.payload
            if not (MessageView.isUser committed) then parts.Runs.ClearMessage d.conversationId
            // 已成为权威消息，流式临时副本立刻作废。
            if isActive d.conversationId then surface.Render()
        | ConversationUpdated d ->
            parts.State.Handle ev
            parts.State.AdvanceCursor()
            surface.ObserveList()
            if isActive d.conversationId then surface.Render()
        | AuthorityCatchUp _ -> parts.State.Handle ev
        | HistoryPage _ ->
            parts.State.Handle ev
            surface.Apply ev
        | SearchResults d ->
            // 只认最近一次发出的查询，迟到的旧结果丢掉。
            if parts.Search.Accept d.queryId then surface.Apply ev
        | SearchFailed d ->
            if parts.Search.Accept d.queryId then surface.Apply ev
        | GenerationStarted d ->
            parts.Runs.Start(d.conversationId, d.generationId)
            if (parts.Runs.Get(Some d.conversationId)).generationId = Some d.generationId then
                parts.State.Handle ev
                if isActive d.conversationId then surface.Render()
        | GenerationDelta d ->
            if parts.Runs.Delta(d.conversationId, d.generationId, MessageView.ofJson d.payload) && isActive d.conversationId then
                surface.Render()
        | GenerationFinished d ->
            // Fail-closed：未知状态与无详情的失败落成可重试的通用失败卡（D2）。
            let error = GenerationOutcome.errorOf d.status d.error
            error |> Option.iter (fun err -> RetryCountdown.attach d.generationId err DateTimeOffset.UtcNow)
            if parts.Runs.Finish(d.conversationId, d.generationId, error, d.usage) then
                parts.State.Handle ev
                // 宿主在这里做 PWA 安装引导等浏览器事务。
                surface.Apply ev
                match GenerationOutcome.notice d.status error (isActive d.conversationId) with
                | FinishNotice.Stopped -> surface.Toast "已停止" DispatchTone.Info
                | FinishNotice.FailureToast text -> surface.Toast text DispatchTone.Failure
                | FinishNotice.Quiet -> ()
                if isActive d.conversationId then surface.Render()
        | GenerationPartial d ->
            if parts.Runs.RestorePartial(d.conversationId, d.generationId, MessageView.ofJson d.message, d.error)
               && isActive d.conversationId then
                surface.Render()
        | ProviderRateLimitNotice d ->
            // 决策 241：限流信号先到，等 generation.finished 再挂到错误卡上。
            RetryCountdown.notice d.generationId d.limit DateTimeOffset.UtcNow
        | AttachmentDownloadBegin d -> parts.Downloads.Begin(d.sha256, d.fileName)
        | AttachmentDownloadChunk d ->
            if parts.Downloads.AppendBase64(d.sha256, d.dataBase64) then
                surface.Toast UploadFacts.downloadCappedText DispatchTone.Warning
        | AttachmentDownloadComplete d ->
            match parts.Downloads.Take d.sha256 with
            | Some(fileName, bytes) -> surface.Downloaded fileName bytes
            | None -> ()
        | CommandAccepted d ->
            parts.Outbox.Accept d.invocationId
            surface.Render()
        | CommandCommitted d ->
            parts.Outbox.Commit d.invocationId
            match parts.Commands.Commit d.invocationId with
            | Some feedback ->
                feedback.onCommitted ()
                feedback.onCompleted true
                feedback.successMessage |> Option.iter (fun message -> surface.Toast message DispatchTone.Success)
            | None -> ()
            surface.Render()
        | CommandRejected d ->
            parts.Commands.Reject d.invocationId |> Option.iter (fun feedback -> feedback.onCompleted false)
            parts.Outbox.SetState(d.invocationId, RejectedMessage d.message)
            parts.Outbox.RejectCreation(d.invocationId, d.message)
            // 拒绝只报一次：未确认行已内联呈现原因与重试，不叠 toast。
            surface.Render()
        | ServerError d ->
            let active = surface.ActiveConversation()
            match ServerErrorRoute.classify active.IsSome d.message with
            | ServerErrorRoute.MissingAttachment sha ->
                surface.MissingAttachment sha
                surface.Render()
            | ServerErrorRoute.ConversationFailure message ->
                // 可行动的主机错误进错误卡（D9）；槽位被占时退回 toast。
                let failure = GenerationError.create GenerationErrorKind.UnknownFailure message
                if parts.Runs.Finish(active.Value, Guid.CreateVersion7(), Some failure, None) then surface.Render()
                else surface.Toast (ServerErrorRoute.toastText message) DispatchTone.Failure
            | ServerErrorRoute.Toast text -> surface.Toast text DispatchTone.Failure
        | _ -> surface.Apply ev
