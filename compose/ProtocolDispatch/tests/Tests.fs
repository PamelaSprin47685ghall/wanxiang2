module Tests

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

/// 真实参与者 + 记录宿主界面面收到的事实。
type private Rig(active: Guid option) =
    let log = ResizeArray<string>()
    let sent = ResizeArray<WireEvent>()
    let parts =
        { State = ClientState()
          Runs = ConversationRuns()
          Outbox = MessageOutbox()
          Commands = CommandFeedbackTracker()
          Configs = ConfigFeedbackTracker()
          Downloads = DownloadBuffers(16L)
          Search = SearchInFlight()
          Probes = PendingProbes() }
    let surface =
        { ActiveConversation = fun () -> active
          InstanceId = fun () -> "inst"
          Send = fun ev -> sent.Add ev
          Render = fun () -> log.Add "render"
          Toast = fun text tone -> log.Add(sprintf "toast %A %s" tone text)
          ObserveList = fun () -> log.Add "observe"
          DispatchPending = fun item -> log.Add(sprintf "dispatch %s" item.text)
          Downloaded = fun name bytes -> log.Add(sprintf "downloaded %s %d" name bytes.Length)
          MissingAttachment = fun sha -> log.Add("missing " + sha)
          Apply = fun ev -> log.Add("apply " + (Reflection.FSharpValue.GetUnionFields(ev, typeof<WireEvent>) |> fst).Name) }
    member val Dispatch = ProtocolDispatch(parts, surface)
    member _.Parts = parts
    member _.Log = List.ofSeq log
    member _.Sent = List.ofSeq sent

[<Fact>]
let ``Hello 与审批能力回声不改变任何状态，也不交给宿主`` () =
    let rig = Rig(None)
    rig.Dispatch.Handle(Hello {| instanceId = Some "i"; protocol = "p"; version = 1 |})
    Assert.Empty(rig.Log)

[<Fact>]
let ``认证成功整段交给宿主，组合者不碰模型`` () =
    let rig = Rig(None)
    rig.Dispatch.Handle(AuthAccepted {| instanceId = "x" |})
    Assert.Equal<string list>([ "apply AuthAccepted" ], rig.Log)
    Assert.Empty(rig.Sent)

[<Fact>]
let ``HandleEvent CommandCommitted resolves feedback and commits outbox`` () =
    let rig = Rig(None)
    let id = Guid.NewGuid()
    let mutable committed = false
    let mutable completed = None
    rig.Parts.Commands.TrackIdWithCompletion(id, true, Some "已保存", (fun () -> committed <- true), (fun ok -> completed <- Some ok))
    rig.Dispatch.Handle(CommandCommitted {| invocationId = id; commandId = "c"; commitId = 0UL |})
    Assert.True(committed)
    Assert.Equal(Some true, completed)
    Assert.Equal<string list>([ "toast Success 已保存"; "render" ], rig.Log)

[<Fact>]
let ``配置回执：登记过的空文案不叠提示但仍重新拉目录，失败只报错误`` () =
    let rig = Rig(None)
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    rig.Parts.Configs.Track(a, "", ignore)
    rig.Dispatch.Handle(ConfigApplied {| requestId = a; ok = true; errors = [] |})
    rig.Dispatch.Handle(ConfigApplied {| requestId = b; ok = false; errors = [ "x"; "y" ] |})
    Assert.Equal<string list>([ "toast Failure x；y" ], rig.Log)
    Assert.Equal<WireEvent list>([ CatalogRequest ], rig.Sent)

[<Fact>]
let ``HandleEvent GenerationFinished with error preserves error in runs`` () =
    let conv, other = Guid.NewGuid(), Guid.NewGuid()
    let rig = Rig(Some conv)
    let g1, g2 = Guid.NewGuid(), Guid.NewGuid()
    rig.Dispatch.Handle(GenerationStarted {| conversationId = other; generationId = g1; providerId = "p"; model = "m" |})
    rig.Dispatch.Handle(GenerationFinished {| conversationId = other; generationId = g1; status = "failed"; error = None; usage = None |})
    rig.Dispatch.Handle(GenerationStarted {| conversationId = conv; generationId = g2; providerId = "p"; model = "m" |})
    rig.Dispatch.Handle(GenerationFinished {| conversationId = conv; generationId = g2; status = "cancelled"; error = None; usage = None |})
    let log = rig.Log
    Assert.Equal(6, log.Length)
    Assert.StartsWith("toast Failure 生成失败，主机没有说明原因。", log[1])
    Assert.Equal<string list>(
        [ "apply GenerationFinished"; "render"; "apply GenerationFinished"; "toast Info 已停止"; "render" ],
        [ log[0]; log[2]; log[3]; log[4]; log[5] ])

[<Fact>]
let ``迟到的旧搜索结果丢掉`` () =
    let rig = Rig(None)
    let old, latest = Guid.NewGuid(), Guid.NewGuid()
    rig.Parts.Search.Sent(old, DateTimeOffset.UtcNow)
    rig.Parts.Search.Sent(latest, DateTimeOffset.UtcNow)
    rig.Dispatch.Handle(SearchFailed {| queryId = old; message = "x" |})
    rig.Dispatch.Handle(SearchFailed {| queryId = latest; message = "x" |})
    Assert.Equal<string list>([ "apply SearchFailed" ], rig.Log)

[<Fact>]
let ``主机错误：附件缺失标记，记账失败进当前对话错误卡，其余提示`` () =
    let conv = Guid.NewGuid()
    let rig = Rig(Some conv)
    rig.Dispatch.Handle(ServerError {| message = "attachment ABC not found" |})
    rig.Dispatch.Handle(ServerError {| message = "消息记账失败：磁盘满" |})
    rig.Dispatch.Handle(ServerError {| message = "boom" |})
    Assert.Equal<string list>([ "missing abc"; "render"; "render"; "toast Failure 主机报告了错误：boom" ], rig.Log)
    Assert.True((rig.Parts.Runs.Get(Some conv)).error.IsSome)

[<Fact>]
let ``下载超限提示一次，拼好后交给宿主保存`` () =
    let rig = Rig(None)
    rig.Dispatch.Handle(AttachmentDownloadBegin {| sha256 = "s"; size = 3L; mediaType = "x"; fileName = "a.txt" |})
    rig.Dispatch.Handle(AttachmentDownloadChunk {| sha256 = "s"; index = 0; dataBase64 = Convert.ToBase64String [| 1uy; 2uy; 3uy |] |})
    rig.Dispatch.Handle(AttachmentDownloadComplete {| sha256 = "s" |})
    rig.Dispatch.Handle(AttachmentDownloadBegin {| sha256 = "t"; size = 32L; mediaType = "x"; fileName = "b" |})
    rig.Dispatch.Handle(AttachmentDownloadChunk {| sha256 = "t"; index = 0; dataBase64 = Convert.ToBase64String(Array.zeroCreate<byte> 32) |})
    Assert.Equal<string list>([ "downloaded a.txt 3"; "toast Warning 附件过大，下载已中止。" ], rig.Log)

[<Fact>]
let ``HandleEvent CommandRejected marks outbox as rejected and informs feedback`` () =
    let rig = Rig(None)
    let conv = Guid.NewGuid()
    let id = PendingMessage.invocationId (rig.Parts.Outbox.Stage("inst", conv, "hi", [], None))
    let mutable completed = None
    rig.Parts.Commands.TrackIdWithCompletion(id, true, None, ignore, (fun ok -> completed <- Some ok))
    rig.Dispatch.Handle(CommandRejected {| invocationId = id; code = "x"; message = "不行"; requiredCommitId = None |})
    Assert.Equal(Some false, completed)
    Assert.Equal(Some(RejectedMessage "不行"), rig.Parts.Outbox.TryFind id |> Option.map _.state)
    Assert.Equal<string list>([ "render" ], rig.Log)

[<Fact>]
let ``HandleEvent GenerationStarted, GenerationDelta, and GenerationFinished flow`` () =
    let conv = Guid.NewGuid()
    let rig = Rig(Some conv)
    let g = Guid.NewGuid()
    rig.Dispatch.Handle(GenerationStarted {| conversationId = conv; generationId = g; providerId = "p"; model = "m" |})
    Assert.True((rig.Parts.Runs.Get(Some conv)).running)
    rig.Dispatch.Handle(GenerationFinished {| conversationId = conv; generationId = g; status = "completed"; error = None; usage = None |})
    Assert.False((rig.Parts.Runs.Get(Some conv)).running)
    Assert.Equal<string list>([ "render"; "apply GenerationFinished"; "render" ], rig.Log)

[<Fact>]
let ``对话快照到达后发出等着它的发件箱消息`` () =
    let conv = Guid.NewGuid()
    let rig = Rig(None)
    let create = Wanxiang.Core.CreateConversation {| invocationId = Guid.NewGuid(); conversationId = conv; title = ""; config = SessionConfig.empty |}
    rig.Parts.Outbox.Stage("inst", conv, "等快照", [], Some create) |> ignore
    rig.Dispatch.Handle(
        ConversationSnapshot
            {| conversationId = conv; title = ""; lastCommitId = 0UL; runtimeState = "idle"; generationId = None
               messages = Text.Json.Nodes.JsonArray(); snapshotEarliestCommitId = 0UL; snapshotHasMore = false; config = SessionConfig.empty |})
    Assert.Equal<string list>([ "apply ConversationSnapshot"; "dispatch 等快照" ], rig.Log)
