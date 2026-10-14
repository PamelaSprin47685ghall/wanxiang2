namespace OldAppShell

open Wanxiang.UI

open System

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs：
// RestoreLocalDrafts（knownConversationIds 累加、输入框填充条件、hasDrafts / needsReselect / message）、
// UpdateDraftSummary（texts / unsent）、PruneDeletedConversationDrafts（storedDrafts 过滤、knownConversationIds 更新）、ClearLocalDrafts 文案。
// 读到的字段换成参数，toast 换成返回文案；循环与过滤逐行照抄。
module DraftRestore =
    let knownConversations (storedDrafts: DraftSnapshot) (instanceId: string) : Set<Guid> =
        let mutable knownConversationIds: Set<Guid> = Set.empty
        for d in storedDrafts.drafts do
            if d.instanceId = instanceId then
                d.conversationId |> Option.iter (fun id -> knownConversationIds <- knownConversationIds.Add id)
        for u in storedDrafts.unsent do
            if u.instanceId = instanceId then knownConversationIds <- knownConversationIds.Add u.conversationId
        knownConversationIds

    let restoreNotice (storedDrafts: DraftSnapshot) (instanceId: string) (unsentRestored: int) : string option =
        let mutable shown = None
        let toast text _ = shown <- Some text
        let Info = ()
        let hasDrafts =
            storedDrafts.drafts |> List.exists (fun d -> d.instanceId = instanceId && (not (String.IsNullOrWhiteSpace d.text) || not (List.isEmpty d.attachments)))
        if hasDrafts || unsentRestored > 0 then
            let needsReselect =
                storedDrafts.drafts |> List.exists (fun d -> d.instanceId = instanceId && not (List.isEmpty d.attachments))
            let message =
                if unsentRestored > 0 then "已恢复上次未发送的内容；未确认的消息可重试，不会重复保存。"
                else "已恢复上次未发送的草稿。"
            toast (if needsReselect then message + "附件需要重新选择。" else message) Info
        shown

    let fillComposer (composerText: string) (restoredText: string) =
        let composer = {| Text = composerText |}
        let live = {| text = restoredText |}
        String.IsNullOrWhiteSpace composer.Text && not (String.IsNullOrWhiteSpace live.text)

    let summaryCounts (snapshot: DraftSnapshot) : int * int =
        let texts = snapshot.drafts |> List.filter (fun d -> not (String.IsNullOrWhiteSpace d.text) || not (List.isEmpty d.attachments)) |> List.length
        let unsent = snapshot.unsent |> List.length
        texts, unsent

    let pruneStored (stored: DraftSnapshot) (instanceId: string) (gone: Set<Guid>) : DraftSnapshot =
        let mutable storedDrafts = stored
        for id in gone do
            storedDrafts <-
                { drafts = storedDrafts.drafts |> List.filter (fun d -> not (d.instanceId = instanceId && d.conversationId = Some id))
                  unsent = storedDrafts.unsent |> List.filter (fun u -> not (u.instanceId = instanceId && u.conversationId = id)) }
        storedDrafts

    let knownAfterPrune (knownConversationIds: Set<Guid>) (gone: Set<Guid>) (listed: Set<Guid>) =
        Set.union (Set.difference knownConversationIds gone) listed

    let clearedText = "本地草稿已清除。"
