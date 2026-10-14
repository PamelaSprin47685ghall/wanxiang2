module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 UiControllersTests 与 UiPureLogicTests。
[<Fact>]
let ``AttachmentDraftController begin draft, complete and remove lifecycle`` () =
    let controller = AttachmentDraftController()
    let attId = Guid.NewGuid()
    let upload: AttachmentUpload = {
        attachmentId = attId
        fileName = "file1.txt"
        mediaType = "text/plain"
        size = 100L
        sha256 = "dummy-sha256"
    }

    controller.Begin upload |> ignore

    Assert.Single(controller.Items) |> ignore
    Assert.False(controller.Items.[0].ready)
    Assert.Equal("file1.txt", controller.Items.[0].fileName)
    Assert.True(controller.HasUploading)

    let completed = controller.Complete(attId, 100L)
    Assert.True(completed.IsSome)
    Assert.True(controller.Items.[0].ready)
    Assert.True(controller.HasReady)

    controller.Remove(attId) |> ignore
    Assert.Empty(controller.Items)

[<Fact>]
let ``AttachmentDraftController upload and draft state transitions`` () =
    let controller = AttachmentDraftController()
    let attId = Guid.NewGuid()
    let upload: AttachmentUpload =
        { attachmentId = attId
          fileName = "test.png"
          mediaType = "image/png"
          size = 1024L
          sha256 = "dummy-sha256" }

    // 1. Begin
    controller.Begin upload |> ignore
    Assert.True(controller.HasUploading)
    Assert.False(controller.HasReady)
    Assert.False(controller.HasFailed)
    Assert.Single(controller.Items) |> ignore

    // 2. Complete
    let completed = controller.Complete(attId, 1024L)
    Assert.True(completed.IsSome)
    let (_, stillDrafted, _) = completed.Value
    Assert.True(stillDrafted)
    Assert.True(controller.HasReady)
    Assert.False(controller.HasUploading)

    // 3. TryConsumeReady
    let consumed = controller.TryConsumeReady()
    Assert.True(consumed.IsSome)
    Assert.Single(consumed.Value) |> ignore

    // 4. 再次消费为空
    Assert.True(controller.TryConsumeReady().Value |> List.isEmpty)

[<Fact>]
let ``drafts are keyed per host and conversation and moving keeps the owner key in step`` () =
    let drafts = ComposerDrafts()
    let a = drafts.Get("h1", None)
    a.text <- "起步"
    Assert.Same(a, drafts.Get("h1", None))
    Assert.NotSame(a, drafts.Get("h2", None))
    let conv = Guid.NewGuid()
    drafts.Move("h1", None, Some conv)
    Assert.Equal("起步", (drafts.Get("h1", Some conv)).text)
    Assert.Equal(("h1", Some conv), (drafts.Get("h1", Some conv)).attachments.OwnerKey)
    Assert.Equal("", (drafts.Get("h1", None)).text)
    drafts.Remove("h1", Some conv)
    Assert.Equal("", (drafts.Get("h1", Some conv)).text)
    // 断连时上传中的翻成失败残留，发送不被永远楔住
    let c = (drafts.Get("h1", None)).attachments
    c.Begin { attachmentId = Guid.NewGuid(); fileName = "a"; mediaType = "text/plain"; size = 1L; sha256 = "s" } |> ignore
    Assert.Equal(1, c.MarkAllUploadingAsFailed().Length)
    Assert.True c.HasFailed
    Assert.True(c.TryConsumeReady().IsNone)
