module Tests

open System
open Xunit
open Wanxiang.UI

let t0 = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)

let draft inst conv text : StoredDraft = { instanceId = inst; conversationId = conv; text = text; attachments = [] }

let unsent inst conv inv : StoredUnsent =
    { instanceId = inst; conversationId = conv; invocationId = inv; text = "u"; attachments = []; savedAt = t0 }

let snap ds us : DraftSnapshot = { drafts = ds; unsent = us }

let isWrite = function DraftWrite.Write _ -> true | _ -> false

[<Fact>]
let ``读完之前与保存关闭时不写盘`` () =
    let s = DraftSession()
    let current = snap [ draft "h" None "hi" ] []
    Assert.Equal(DraftWrite.Unchanged, s.Flush(true, current))
    s.Loaded None
    Assert.Equal(DraftWrite.Unchanged, s.Flush(false, current))
    Assert.True(isWrite (s.Flush(true, current)))
    Assert.Equal(DraftWrite.Unchanged, s.Flush(true, current))

// 覆盖旧 ComposerReliabilityTests：`saving while connected to one host keeps another host's stored drafts`（会话一侧）
[<Fact>]
let ``只恢复过的主机以内存为准，别的主机的已存内容原样保留`` () =
    let s = DraftSession()
    let other = snap [ draft "b" None "B 的草稿" ] []
    s.Loaded(Some(DraftCodec.serialize (snap [ draft "a" None "A 旧"; draft "b" None "B 的草稿" ] [])))
    Assert.True((s.Restore(true, "a")).IsSome)
    Assert.True((s.Restore(true, "a")).IsNone)
    let merged = s.ForSave(snap [ draft "a" None "A 新" ] [])
    Assert.Equal<string list>([ "B 的草稿"; "A 新" ], merged.drafts |> List.map _.text)
    ignore other

// 覆盖旧 ComposerReliabilityTests：`closing and reopening restores the draft text and marks attachments as needing reselection`（提示部分）
[<Fact>]
let ``放回后给出提示并认下出现过的对话`` () =
    let c = Guid.NewGuid()
    let s = DraftSession()
    s.Loaded(Some(DraftCodec.serialize (snap [ draft "a" (Some c) "草稿" ] [])))
    Assert.Equal(None, s.Restore(false, "a"))
    Assert.Equal(None, s.Restore(true, ""))
    Assert.True((s.Restore(true, "a")).IsSome)
    Assert.Equal(Some "已恢复上次未发送的草稿。", s.Restored("a", 0))
    Assert.Equal<Set<Guid>>(set [ c ], s.Known)

// 覆盖旧 ComposerReliabilityTests：`deleting a conversation removes its draft from memory and from disk`（会话一侧）
[<Fact>]
let ``列表里消失的对话视为已删，在途发送的不算`` () =
    let gone, busy, kept = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let s = DraftSession()
    s.Loaded(Some(DraftCodec.serialize (snap [ draft "a" (Some gone) "x"; draft "a" (Some busy) "y" ] [ unsent "a" kept (Guid.NewGuid()) ])))
    s.Restore(true, "a") |> ignore
    s.Restored("a", 0) |> ignore
    let removed = s.Prune("a", set [ kept ], set [ busy ])
    Assert.Equal<Set<Guid>>(set [ gone ], removed)
    Assert.Equal<Guid option list>([ Some busy ], s.Stored.drafts |> List.map _.conversationId)
    Assert.Equal<Set<Guid>>(set [ busy; kept ], s.Known)
    Assert.Empty(s.Prune("", Set.empty, Set.empty))

[<Fact>]
let ``未确认消息的保存时刻只记第一次`` () =
    let s = DraftSession()
    let id = Guid.NewGuid()
    Assert.Equal(t0, s.SavedAt(id, t0))
    Assert.Equal(t0, s.SavedAt(id, t0.AddMinutes 5.0))
    s.Clear() |> ignore
    Assert.Equal(t0.AddMinutes 6.0, s.SavedAt(id, t0.AddMinutes 6.0))

// 覆盖旧 ComposerReliabilityTests：`switching the setting off deletes the file and clear drafts empties everything`（会话一侧）
[<Fact>]
let ``清除后已存快照为空，下一次有内容的保存一定写`` () =
    let s = DraftSession()
    let current = snap [ draft "a" None "hi" ] []
    s.Loaded(Some(DraftCodec.serialize current))
    s.Restore(true, "a") |> ignore
    Assert.Equal(DraftWrite.Unchanged, s.Flush(true, current))
    Assert.Equal("本地草稿已清除。", s.Clear())
    Assert.True(DraftSnapshot.isEmpty s.Stored)
    Assert.True(s.IsLoaded)
    Assert.True(isWrite (s.Flush(true, current)))
    s.StorageCleared()
    Assert.True(isWrite (s.Flush(true, current)))
    Assert.Equal(DraftWrite.Delete, s.Flush(true, DraftSnapshot.empty))
