module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


let input atts =
    { enabled = true; generating = false; canStop = false; disabledReason = ""; hasText = true
      attachments = atts; visionNo = false; visionBlockedMessage = "不能看图"; enterSends = true }

[<Fact>]
let ``uploading or failed attachments block sending`` () =
    Assert.True((ComposerSendState.decide (input [])).canSend)
    let up = ComposerSendState.decide (input [ Samples.up ])
    Assert.False up.canSend
    Assert.Equal("附件上传中，请稍候…", up.sendTip)
    let bad = ComposerSendState.decide (input [ Samples.bad ])
    Assert.False bad.canSend
    Assert.Equal("有附件上传失败，移除或重新添加后发送", bad.sendTip)

[<Fact>]
let ``an image on a non-vision model blocks send with the given message`` () =
    let v = ComposerSendState.decide { input [ Samples.img ] with visionNo = true }
    Assert.True v.visionBlocked
    Assert.False v.canSend
    Assert.Equal("不能看图", v.sendTip)

[<Fact>]
let ``a ready attachment alone is enough; nothing at all is not`` () =
    Assert.True((ComposerSendState.decide { input [ Samples.ok ] with hasText = false }).canSend)
    let v = ComposerSendState.decide { input [] with hasText = false }
    Assert.False v.canSend
    Assert.Equal("请输入消息或添加附件", v.sendTip)

[<Fact>]
let ``while generating the send key is stop and the queue key appears`` () =
    let v = ComposerSendState.decide { input [] with generating = true; canStop = true }
    Assert.True v.sendEnabled
    Assert.Equal("停止生成", v.sendLabel)
    Assert.True v.queueVisible
    Assert.Equal(Some "排队发送（Enter）", v.queueTip)
    Assert.Equal(None, (ComposerSendState.decide (input [])).queueTip)

[<Fact>]
let ``offline uses the reason or a default`` () =
    Assert.Equal("连接已断开", (ComposerSendState.decide { input [] with enabled = false }).sendTip)
    Assert.Equal("主机忙", (ComposerSendState.decide { input [] with enabled = false; disabledReason = "主机忙" }).sendTip)

[<Fact>]
let ``submit treats a failed attachment as not ready`` () =
    Assert.True(ComposerSendState.canSubmit true "hi" [ Samples.ok ])
    Assert.False(ComposerSendState.canSubmit true "hi" [ Samples.bad ])
    Assert.False(ComposerSendState.canSubmit true " " [])
    Assert.False(ComposerSendState.canSubmit false "hi" [])

[<Fact>]
let ``chip state text`` () =
    Assert.Equal("需重选", ComposerSendState.chipState true Samples.ok "1 KB")
    Assert.Equal("上传中", ComposerSendState.chipState false Samples.up "1 KB")
    Assert.Equal("上传失败：1 KB，可移除或重新添加", ComposerSendState.chipTip false Samples.bad "1 KB")
