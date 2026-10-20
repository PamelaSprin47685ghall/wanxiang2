module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


let input atts =
    { host = HostAvailable
      generation = Idle
      text = HasText
      attachments = atts
      vision = VisionAllowed
      sendKey = EnterSends }

[<Fact>]
let ``uploading or failed attachments block sending`` () =
    Assert.Equal(SendGate.Send, (ComposerSendState.decide (input [])).gate)
    let up = ComposerSendState.decide (input [ Samples.up ])
    Assert.Equal(SendGate.Blocked BlockReason.AttachmentUploading, up.gate)
    Assert.False up.SendEnabled
    Assert.Equal("附件上传中，请稍候…", up.SendTip)
    let bad = ComposerSendState.decide (input [ Samples.bad ])
    Assert.Equal(SendGate.Blocked BlockReason.AttachmentFailed, bad.gate)
    Assert.False bad.SendEnabled
    Assert.Equal("有附件上传失败，移除或重新添加后发送", bad.SendTip)

[<Fact>]
let ``an image on a non-vision model blocks send with the given message`` () =
    let v = ComposerSendState.decide { input [ Samples.img ] with vision = VisionForbidden "不能看图" }
    Assert.True v.VisionBlocked
    Assert.Equal(SendGate.Blocked (BlockReason.ModelCannotSeeImage "不能看图"), v.gate)
    Assert.False v.SendEnabled
    Assert.Equal("不能看图", v.SendTip)

[<Fact>]
let ``a ready attachment alone is enough; nothing at all is not`` () =
    Assert.Equal(SendGate.Send, (ComposerSendState.decide { input [ Samples.ok ] with text = EmptyText }).gate)
    let v = ComposerSendState.decide { input [] with text = EmptyText }
    Assert.Equal(SendGate.Blocked BlockReason.EmptyContent, v.gate)
    Assert.False v.SendEnabled
    Assert.Equal("请输入消息或添加附件", v.SendTip)

[<Fact>]
let ``while generating the send key is stop and the queue key appears`` () =
    let v = ComposerSendState.decide { input [] with generation = Generating CanStop }
    Assert.Equal(SendGate.Stop, v.gate)
    Assert.True v.SendEnabled
    Assert.Equal("停止生成", v.SendLabel)
    Assert.True v.QueueVisible
    Assert.Equal(Some "排队发送（Enter）", v.QueueTip)
    Assert.Equal(None, (ComposerSendState.decide (input [])).QueueTip)

[<Fact>]
let ``offline uses the reason or a default`` () =
    Assert.Equal("连接已断开", (ComposerSendState.decide { input [] with host = HostDisabled "" }).SendTip)
    Assert.Equal("主机忙", (ComposerSendState.decide { input [] with host = HostDisabled "主机忙" }).SendTip)

[<Fact>]
let ``submit treats a failed attachment as not ready`` () =
    Assert.True(ComposerSendState.canSubmit HostAvailable HasText [ Samples.ok ])
    Assert.False(ComposerSendState.canSubmit HostAvailable HasText [ Samples.bad ])
    Assert.False(ComposerSendState.canSubmit HostAvailable EmptyText [])
    Assert.False(ComposerSendState.canSubmit (HostDisabled "") HasText [])

[<Fact>]
let ``chip state text`` () =
    Assert.Equal("需重选", ComposerSendState.chipState AttachmentStage.Restored "1 KB")
    Assert.Equal("上传中", ComposerSendState.chipState AttachmentStage.Uploading "1 KB")
    Assert.Equal("上传失败：1 KB，可移除或重新添加", ComposerSendState.chipTip AttachmentStage.Failed "1 KB")
    Assert.Equal("1 KB", ComposerSendState.chipState AttachmentStage.Ready "1 KB")
    Assert.Equal("文件大小：1 KB", ComposerSendState.chipTip AttachmentStage.Ready "1 KB")

[<Fact>]
let ``Enter sending key matrix: not shift && (enterSends <> ctrl) covers all 8 combinations`` () =
    // 8 combinations: (sendKey = EnterSends / CtrlEnterSends) x (shift = true / false) x (ctrlOrMeta = true / false)
    // EnterSends mode: enter sends only when no shift and no ctrl
    Assert.True(ComposerSendState.shouldSend EnterSends false false) // Enter -> sends
    Assert.False(ComposerSendState.shouldSend EnterSends true false) // Shift + Enter -> newline (does not send)
    Assert.False(ComposerSendState.shouldSend EnterSends false true) // Ctrl + Enter -> does not send
    Assert.False(ComposerSendState.shouldSend EnterSends true true) // Shift + Ctrl + Enter -> does not send

    // CtrlEnterSends mode: enter sends only when no shift and ctrl is pressed
    Assert.False(ComposerSendState.shouldSend CtrlEnterSends false false) // Enter -> newline (does not send)
    Assert.False(ComposerSendState.shouldSend CtrlEnterSends true false) // Shift + Enter -> newline (does not send)
    Assert.True(ComposerSendState.shouldSend CtrlEnterSends false true) // Ctrl + Enter -> sends
    Assert.False(ComposerSendState.shouldSend CtrlEnterSends true true) // Shift + Ctrl + Enter -> does not send
