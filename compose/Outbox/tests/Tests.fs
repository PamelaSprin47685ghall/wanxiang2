module Tests

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI

// 迁自旧 ComposerReliabilityTests。
[<Fact>]
let ``a send whose conversation was never confirmed falls back into the start draft`` () =
    let conv = Guid.NewGuid()
    let drafts, outbox = ComposerDrafts(), MessageOutbox()
    let creation =
        CreateConversation {| invocationId = Guid.NewGuid(); conversationId = conv; title = "新对话"; config = SessionConfig.empty |}
    let item = outbox.Stage("host-1", conv, "对话还没建成", [], Some creation)
    outbox.ApplyState(PendingMessage.invocationId item, UnconfirmedMessage "断线")
    let snapshot = DraftPolicy.snapshotOf drafts outbox (fun _ -> DateTimeOffset.UtcNow)
    Assert.Empty snapshot.unsent
    let row = Assert.Single snapshot.drafts
    Assert.Equal(None, row.conversationId)
    Assert.Equal("对话还没建成", row.text)

[<Fact>]
let ``an unconfirmed send comes back as retryable with the original invocation id`` () =
    let conv = Guid.NewGuid()
    let drafts, outbox = ComposerDrafts(), MessageOutbox()
    let attachment =
        { attachmentId = Guid.NewGuid(); sha256 = String('b', 64); size = 10L; mediaType = "text/plain"
          fileName = "a.txt"; ready = true; failed = false }
    let item = outbox.Stage("host-1", conv, "还没确认的消息", [ attachment ], None)
    outbox.ApplyState(PendingMessage.invocationId item, UnconfirmedMessage "连接已断开")
    let snapshot = DraftPolicy.snapshotOf drafts outbox (fun _ -> DateTimeOffset.UtcNow)
    let text = DraftCodec.serialize snapshot
    // 重新启动：新的内存，只从落盘文本恢复。
    let parsed = DraftCodec.tryParse text
    let drafts2, outbox2 = ComposerDrafts(), MessageOutbox()
    let restored = DraftPolicy.restoreInto parsed "host-1" drafts2 outbox2
    Assert.Equal(1, restored)
    let back = Assert.Single(outbox2.Items("host-1", Some conv))
    Assert.Equal(PendingMessage.invocationId item, PendingMessage.invocationId back)
    Assert.True(PendingMessage.canRetry back, "必须是可重试的未确认状态，而不是已排队/发送中")
    Assert.Equal("还没确认的消息", back.text)
    Assert.Equal(1, back.attachments.Length)
    // 重试发出的命令载荷与原命令逐字相同（主机据此幂等核对，不会重复保存）。
    Assert.Equal<string>(
        WireCodec.encodeCommand item.command,
        WireCodec.encodeCommand back.command)

[<Fact>]
let ``drafts are isolated per conversation and per host`` () =
    let convA, convB = Guid.NewGuid(), Guid.NewGuid()
    let snapshot =
        { drafts =
            [ { instanceId = "host-1"; conversationId = Some convA; text = "一号机甲"; attachments = [] }
              { instanceId = "host-1"; conversationId = Some convB; text = "一号机乙"; attachments = [] }
              { instanceId = "host-2"; conversationId = Some convA; text = "二号机甲"; attachments = [] } ]
          unsent = [] }
    let drafts, outbox = ComposerDrafts(), MessageOutbox()
    DraftPolicy.restoreInto snapshot "host-1" drafts outbox |> ignore
    Assert.Equal("一号机甲", (drafts.Get("host-1", Some convA)).text)
    Assert.Equal("一号机乙", (drafts.Get("host-1", Some convB)).text)
    Assert.Equal("", (drafts.Get("host-2", Some convA)).text)
    // 反方向：只恢复 host-2 时，host-1 的内容不会出现。
    let drafts2 = ComposerDrafts()
    DraftPolicy.restoreInto snapshot "host-2" drafts2 (MessageOutbox()) |> ignore
    Assert.Equal("二号机甲", (drafts2.Get("host-2", Some convA)).text)
    Assert.Equal("", (drafts2.Get("host-1", Some convA)).text)

[<Fact>]
let ``saving while connected to one host keeps another host's stored drafts`` () =
    let stored =
        { drafts = [ { instanceId = "host-2"; conversationId = None; text = "另一台机器上的草稿"; attachments = [] } ]
          unsent = [] }
    let drafts = ComposerDrafts()
    (drafts.Get("host-1", None)).text <- "这台机器"
    let current = DraftPolicy.snapshotOf drafts (MessageOutbox()) (fun _ -> DateTimeOffset.UtcNow)
    let merged = DraftPolicy.mergeForSave stored (Set.ofList [ "host-1" ]) current
    Assert.Equal<string list>([ "另一台机器上的草稿"; "这台机器" ], merged.drafts |> List.map _.text)

// 迁自旧 R27HostCapabilitiesTests：用真实命令经扩展成员登记。
[<Fact>]
let ``command tracker expires newer commands but leaves message delivery to outbox`` () =
    let tracker = CommandFeedbackTracker()
    let cont = ContinueResponse {| invocationId = Guid.NewGuid(); conversationId = Guid.NewGuid(); messageJson = System.Text.Json.Nodes.JsonObject() |}
    let send = SendUserMessage {| invocationId = Guid.NewGuid(); conversationId = Guid.NewGuid(); messageJson = System.Text.Json.Nodes.JsonObject() |}
    tracker.Track(cont, None, ignore)
    tracker.Track(send, None, ignore)
    let expired = tracker.Expire(DateTimeOffset.UtcNow.AddMinutes 1.0, TimeSpan.FromSeconds 12.0)
    Assert.Equal(1, expired.Length)
    Assert.Equal(1, tracker.Count)
