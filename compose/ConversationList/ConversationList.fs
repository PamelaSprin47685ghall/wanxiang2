namespace Wanxiang.UI

open System

// 组合者：对话列表行（ConversationSummary）与其它只读事实的关联。
// 分叉谱系的解析归 ForkLineage，待删批次的状态归 PendingDeletion；这里只做「拿列表行去对照」的那一步。
// 模块名与库里同名：F# 按名合并同名模块，旧调用方 `ForkLineage.breadcrumb`、`PendingDeletion.visible` 不用改。

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ForkLineage =

    /// 面包屑文案。父对话已删除时只说“原对话已删除”，该分叉仍是完整可用的对话（决策 202）。
    let breadcrumb (summaries: ConversationSummary list) (lineage: Map<Guid, ForkLineage>) (conversationId: Guid) : ForkBreadcrumb option =
        match lineage.TryFind conversationId with
        | None -> None
        | Some l ->
            let parent = summaries |> List.tryFind (fun s -> s.id = l.parentId)
            let available = parent.IsSome && not l.parentDeleted
            if not available then
                Some
                    { parentId = l.parentId
                      parentAvailable = false
                      text = "原对话已删除"
                      description = "这个分叉的原对话已被删除，分叉本身仍可正常使用。" }
            else
                let title =
                    match parent with
                    | Some p -> p.title
                    | None -> l.parentTitle |> Option.defaultValue "原对话"
                let text =
                    match l.forkOrdinal with
                    | Some n -> sprintf "来自「%s」第 %d 条消息的分叉" title n
                    | None -> sprintf "来自「%s」的分叉" title
                Some
                    { parentId = l.parentId
                      parentAvailable = true
                      text = text
                      description = text + "，点击回到原对话" }

    /// 同一消息位置的版本。`messageCommitId` 是当前对话里这条用户消息，`prevCommitId` 是它前一条消息（第一条为 None）。
    /// - 这条消息是某个分叉自己的第一条消息（被编辑过的那条）：版本 = 父对话原文 + 同一分叉点的全部分叉；
    /// - 否则：版本 = 当前对话 + 以当前对话为父、分叉点正好在 `prevCommitId` 的全部分叉。
    /// 已删除的对话不在摘要里，自然不参与；不足 2 个版本时返回 None（不显示控件）。
    let siblings
        (summaries: ConversationSummary list)
        (lineage: Map<Guid, ForkLineage>)
        (conversationId: Guid)
        (messageCommitId: uint64)
        (prevCommitId: uint64 option)
        : SiblingVersions option =
        let byId = summaries |> List.map (fun s -> s.id, s) |> dict
        let owner, after, isForkOwn =
            match lineage.TryFind conversationId with
            | Some l when l.forkFirstMessageId = Some messageCommitId -> l.parentId, l.forkBaseMessageId, true
            | _ -> conversationId, prevCommitId, false
        let forks =
            lineage
            |> Map.toList
            |> List.filter (fun (id, l) -> l.parentId = owner && l.forkBaseMessageId = after && byId.ContainsKey id)
            // 创建顺序：被编辑那条消息的 commitId 单调递增（缺省排最后），再按创建时间、id 稳定
            |> List.sortBy (fun (id, l) -> defaultArg l.forkFirstMessageId UInt64.MaxValue, byId[id].createdAt, id)
            |> List.map fst
        let originalAvailable =
            byId.ContainsKey owner
            && (not isForkOwn || not (lineage.TryFind conversationId |> Option.exists (fun l -> l.parentDeleted)))
        let targets = (if originalAvailable then [ owner ] else []) @ forks
        let index = if isForkOwn then targets |> List.tryFindIndex ((=) conversationId) else Some 0
        match index with
        | Some i when List.length targets >= 2 -> Some { index = i; count = List.length targets; targets = targets }
        | _ -> None

module PendingDeletion =

    /// 列表视图：隐去宽限期内的行。
    let visible (summaries: ConversationSummary list) (state: PendingDeletions) : ConversationSummary list =
        if List.isEmpty state.batches then summaries
        else summaries |> List.filter (fun s -> not (PendingDeletion.isPending s.id state))
