namespace Wanxiang.UI

open System

/// 删除即撤销（§214）：删除不再先弹「无法撤销」确认框，而是先从列表里隐去，
/// 给出带「撤销」的提示条；宽限期过后才真正向主机提交删除命令。
/// 宽限期内关掉应用 = 什么都没删——宁可少删，不可误删。
/// 本模块是纯状态：计时与命令发送由 AppShell 持有，便于确定性测试。
type PendingDeletionBatch =
    {
        token: Guid
        ids: Guid list
        /// 提示条里给用户看的说法：「已删除「标题」」/「已删除 3 个对话」
        label: string
    }

type PendingDeletions =
    {
        batches: PendingDeletionBatch list
        /// 各批的到期时刻（token → 时刻）；未登记到期时刻的批次只能由宿主显式 commit。
        due: Map<Guid, DateTimeOffset>
    }

module PendingDeletion =

    /// 宽限期：比默认提示条停留略长，读完再决定。
    let grace = TimeSpan.FromSeconds 8.0

    let empty = { batches = []; due = Map.empty }

    let label (titles: string list) : string =
        match titles with
        | [ title ] -> sprintf "已删除「%s」" title
        | many -> sprintf "已删除 %d 个对话" (List.length many)

    /// 登记一批待删；已在别的批次里的 id 不重复登记。返回新状态与该批（空批返回 None）。
    let schedule
        (titles: (Guid * string) list)
        (state: PendingDeletions)
        : PendingDeletions * PendingDeletionBatch option =
        let already = state.batches |> List.collect _.ids |> Set.ofList

        let fresh =
            titles
            |> List.filter (fun (id, _) -> not (already.Contains id))
            |> List.distinctBy fst

        if List.isEmpty fresh then
            state, None
        else
            let batch =
                {
                    token = Guid.NewGuid()
                    ids = List.map fst fresh
                    label = label (List.map snd fresh)
                }

            { state with
                batches = state.batches @ [ batch ]
            },
            Some batch

    /// 撤销：把整批移出待删，返回被恢复的 id（批次已提交或不存在时为空）。
    let undo (token: Guid) (state: PendingDeletions) : PendingDeletions * Guid list =
        match state.batches |> List.tryFind (fun b -> b.token = token) with
        | Some b ->
            {
                batches = state.batches |> List.filter (fun x -> x.token <> token)
                due = state.due.Remove token
            },
            b.ids
        | None ->
            { state with
                due = state.due.Remove token
            },
            []

    /// 宽限期到：取出该批交给调用方提交。已撤销的批次返回空，不会误删。
    let commit (token: Guid) (state: PendingDeletions) : PendingDeletions * Guid list = undo token state

    /// 登记一批待删，并记下到期时刻 now + grace（宿主不再为每批各持一个计时器的「何时到」）。
    let scheduleAt
        (now: DateTimeOffset)
        (titles: (Guid * string) list)
        (state: PendingDeletions)
        : PendingDeletions * PendingDeletionBatch option =
        match schedule titles state with
        | next, Some batch ->
            { next with
                due = next.due.Add(batch.token, now + grace)
            },
            Some batch
        | next, None -> next, None

    /// 到 now 为止到期的批次：按登记顺序交出 token，并清掉它们的到期时刻（每批只交出一次）。
    /// 批次本身仍在，宿主随后对每个 token 调用 commit 取出 id 提交。
    let tick (now: DateTimeOffset) (state: PendingDeletions) : PendingDeletions * Guid list =
        let dueTokens =
            state.batches
            |> List.choose (fun b ->
                match state.due.TryFind b.token with
                | Some t when t <= now -> Some b.token
                | _ -> None)

        { state with
            due = dueTokens |> List.fold (fun m t -> Map.remove t m) state.due
        },
        dueTokens

    /// 最近的到期时刻；没有待到期的批次为 None。宿主据此安排下一次计时。
    let nextDue (state: PendingDeletions) : DateTimeOffset option =
        if state.due.IsEmpty then
            None
        else
            Some(state.due |> Map.toSeq |> Seq.map snd |> Seq.min)

    let isPending (id: Guid) (state: PendingDeletions) : bool =
        state.batches |> List.exists (fun b -> List.contains id b.ids)

    /// 删掉当前对话时落到哪一行：侧栏当前可见顺序里第一个没被删的对话；
    /// 当前对话不在被删之列时返回 None（不碰主视图）。迁自旧 AppShell.NeighborExcluding。
    let neighborAfter (active: Guid option) (removedIds: Guid list) (visibleOrder: string list) : Guid option =
        match active with
        | Some currentId when List.contains currentId removedIds ->
            let removed = Set.ofList removedIds

            visibleOrder
            |> List.tryPick (fun id ->
                match Guid.TryParse id with
                | true, g when not (removed.Contains g) -> Some g
                | _ -> None)
        | _ -> None

    /// 撤销成功的提示。迁自旧 AppShell.ScheduleDelete 的撤销回调。
    let restoredText (count: int) =
        if count = 1 then "已恢复对话" else sprintf "已恢复 %d 个对话" count
