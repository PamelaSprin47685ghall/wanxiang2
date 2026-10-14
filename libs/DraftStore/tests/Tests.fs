module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧 DraftCodec / DraftStore(MemoryDraftBackend) / DraftPolicy 跑同一份 Render.fs 生成。
[<Fact>]
let ``golden: codec bytes, save decisions and merges match old code`` () =
    let dir = Path.Combine(AppContext.BaseDirectory, "golden")
    let inputs = File.ReadAllLines(Path.Combine(dir, "inputs.jsonl")) |> Array.filter ((<>) "") |> List.ofArray
    let saves (xs: DraftSnapshot list) =
        let store = DraftStore()
        [ for s in xs ->
            match store.Save s with
            | DraftWrite.Unchanged -> "Unchanged"
            | DraftWrite.Delete -> "Delete"
            | DraftWrite.Write t -> "Write " + t ]
    Assert.Equal(File.ReadAllText(Path.Combine(dir, "expected.txt")), Render.all inputs saves)

// 迁自旧 ComposerReliabilityTests：后端换成「宿主保存的文本」，页面回收 = 只剩这段文本。
[<Fact>]
let ``a recycled page restores drafts from the browser backend`` () =
    let before = DraftStore()
    let conv = Guid.NewGuid()
    let snapshot =
        { drafts =
            [ { instanceId = "host-1"; conversationId = Some conv; text = "手机切后台前的草稿"
                attachments = [ { fileName = "照片.jpg"; mediaType = "image/jpeg"; size = 99L; sha256 = String('c', 64) } ] } ]
          unsent = [] }
    let stored = match before.Save snapshot with DraftWrite.Write t -> t | other -> failwithf "应当写入：%A" other
    let after = DraftStore()
    let loaded = after.Loaded(Some stored)
    Assert.Equal("手机切后台前的草稿", (Assert.Single loaded.drafts).text)
    Assert.Equal("照片.jpg", (Assert.Single (Assert.Single loaded.drafts).attachments).fileName)
    // 读到的就是上次落盘内容：原样再存不写
    Assert.Equal(DraftWrite.Unchanged, after.Save loaded)

// 清除后再存同样内容要重新写（旧「关闭后删除」用例里的闸门部分；是否启用由宿主判断）。
[<Fact>]
let ``after the host clears storage the same snapshot is written again`` () =
    let store = DraftStore()
    let snapshot = { drafts = [ { instanceId = "h"; conversationId = None; text = "x"; attachments = [] } ]; unsent = [] }
    Assert.True(match store.Save snapshot with DraftWrite.Write _ -> true | _ -> false)
    store.Cleared()
    Assert.True(match store.Save snapshot with DraftWrite.Write _ -> true | _ -> false)
    Assert.Equal(DraftWrite.Delete, store.Save DraftSnapshot.empty)
    Assert.Equal(DraftWrite.Unchanged, store.Save DraftSnapshot.empty)

// 迁自旧 ComposerReliabilityTests。
[<Fact>]
let ``conversations that vanish from the list count as deleted unless a send is in flight`` () =
    let kept, gone, sending = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let deleted = DraftPolicy.deletedConversations (Set.ofList [ kept ]) (Set.ofList [ kept; gone; sending ]) (Set.ofList [ sending ])
    Assert.Equal<Guid list>([ gone ], Set.toList deleted)


// 金标准：golden/restore.txt 由 tools/OldDraftRestore.fs（旧 AppShell 原样摘录）跑 RestoreRender 生成。
[<Fact>]
let ``golden: restore notice, prune and summary match old AppShell`` () =
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "restore.txt"))
    let actual =
        RestoreRender.render DraftRestore.knownConversations DraftRestore.restoreNotice DraftRestore.fillComposer
            DraftRestore.summaryCounts DraftRestore.pruneStored DraftRestore.knownAfterPrune DraftRestore.clearedText
    Assert.Equal(expected, actual)

[<Fact>]
let ``restore notice: unsent wins, attachments ask for reselect, nothing restored is silent`` () =
    let s = { drafts = [ RestoreRender.dr "h" None "" [ RestoreRender.att ] ]; unsent = [] }
    Assert.Equal(Some "已恢复上次未发送的草稿。附件需要重新选择。", DraftRestore.restoreNotice s "h" 0)
    Assert.Equal(Some "已恢复上次未发送的内容；未确认的消息可重试，不会重复保存。附件需要重新选择。", DraftRestore.restoreNotice s "h" 1)
    Assert.Equal(None, DraftRestore.restoreNotice s "other" 0)
