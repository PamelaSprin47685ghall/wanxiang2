namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 本地草稿会话（迁自旧 AppShell 的 storedDrafts、draftsLoaded、restoredInstances、knownConversationIds、unsentSavedAt，
/// 及 CurrentDraftSnapshot、FlushDrafts、RestoreLocalDrafts、ClearLocalDrafts、PruneDeletedConversationDrafts、LoadLocalDrafts
/// 里不碰界面、不碰输入框与发件箱的部分）。
/// 读写落点（文件 / IndexedDB）、输入框、发件箱、提示都归宿主：本对象只收事实、回数据。
/// 合作顺序（宿主每步做什么）由本对象的方法次序表达：
///   读完 → Loaded；连上主机 → Restore（给出要放回的快照）→ 宿主放回 → Restored（给出提示）；
///   定时 / 关窗 / 页面隐藏 → 宿主由内存算出当前快照 → Flush（给出该怎么写）；
///   列表快照 → Prune（给出已删对话）；清除 → Clear。
type DraftSession() =
    let gate = DraftStore()
    let mutable stored = DraftSnapshot.empty
    let mutable loaded = false
    let restored = HashSet<string>()
    let mutable known: Set<Guid> = Set.empty
    let savedAt = Dictionary<Guid, DateTimeOffset>()

    /// 本机已存的内容读完（宿主读到的文本；读失败或没有为 None）。读完之前不写盘，避免用空内存覆盖尚未读到的内容。
    member _.Loaded(text: string option) =
        stored <- gate.Loaded text
        loaded <- true

    member _.IsLoaded = loaded
    member _.Stored = stored
    member _.Known = known

    /// 未确认消息第一次被存下的时刻：之后保存沿用同一时刻。供宿主算当前快照时使用。
    member _.SavedAt(invocationId: Guid, now: DateTimeOffset) : DateTimeOffset =
        match savedAt.TryGetValue invocationId with
        | true, t -> t
        | _ ->
            savedAt[invocationId] <- now
            now

    /// 落盘用的快照：已恢复主机以内存为准，其他主机的已存内容原样保留。设置页摘要也用它。
    member _.ForSave(current: DraftSnapshot) : DraftSnapshot =
        DraftPolicy.mergeForSave stored (Set.ofSeq restored) current

    /// 保存。保存关闭或尚未读完时不写；内容没变也不写。
    member this.Flush(persist: bool, current: DraftSnapshot) : DraftWrite =
        if loaded && persist then gate.Save(this.ForSave current)
        else DraftWrite.Unchanged

    /// 连上这台主机：每台主机只放回一次。返回要放回内存的已存快照；不该放回时为 None。
    member _.Restore(persist: bool, instanceId: string) : DraftSnapshot option =
        if loaded && persist && instanceId <> "" && restored.Add instanceId then Some stored
        else None

    /// 宿主放回之后：记下这台主机出现过的对话，返回给用户的一句话（什么都没放回为 None）。
    member _.Restored(instanceId: string, unsentRestored: int) : string option =
        known <- Set.union known (DraftRestore.knownConversations stored instanceId)
        DraftRestore.restoreNotice stored instanceId unsentRestored

    /// 列表快照到达：列表里消失、且没有在途发送的已知对话视为已删。
    /// 返回已删对话（宿主据此清内存草稿与发件箱）；同时从已存内容和已知集合里去掉它们，并认下这次列表。
    member _.Prune(instanceId: string, listed: Set<Guid>, inFlight: Set<Guid>) : Set<Guid> =
        if instanceId = "" then Set.empty
        else
            let gone = DraftPolicy.deletedConversations listed known inFlight
            stored <- DraftRestore.pruneStored stored instanceId gone
            known <- DraftRestore.knownAfterPrune known gone listed
            gone

    /// 宿主删掉了落点内容（关闭保存时）：下一次保存一定会写。
    member _.StorageCleared() = gate.Cleared()

    /// 清除本地草稿：已存快照与保存时刻清空，落点闸门复位。宿主另清内存草稿、发件箱与落点。
    /// 与旧实现相同，不清读完标志、已恢复主机与已知对话。返回给用户的提示。
    member _.Clear() : string =
        stored <- DraftSnapshot.empty
        savedAt.Clear()
        gate.Cleared()
        DraftRestore.clearedText
