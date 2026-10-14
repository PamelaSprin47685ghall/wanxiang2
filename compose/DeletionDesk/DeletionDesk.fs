namespace Wanxiang.UI

open System
open Wanxiang.Core

/// 删除流程交给宿主的提示语气（宿主映射到 ToastTone）
[<RequireQualifiedAccess>]
type DeletionTone =
    | Info
    | Success
    | Warning

/// 删除流程读到的事实与要落到的界面面。都是事实通知，不是步骤脚本。
type DeletionSurface = {
    Now: unit -> DateTimeOffset
    /// 当前打开的对话
    ActiveConversation: unit -> Guid option
    /// 对话标题（列表里没有就是「对话」）
    TitleOf: Guid -> string option
    /// 侧栏当前可见顺序（邻位规则用）
    VisibleOrder: unit -> string list
    /// 待删集合变了：侧栏按它隐去 / 放回行（宿主用 ConversationList 的 visible 过滤摘要）
    PendingChanged: PendingDeletions -> unit
    /// 单条删除：焦点落到被删行的邻行
    FocusAfterDelete: Guid -> unit
    /// 当前对话被删 / 撤销恢复：打开这一段（None = 回到新对话）
    Open: Guid option -> unit
    Render: unit -> unit
    Toast: string -> DeletionTone -> unit
    /// 带「撤销」动作的提示条
    ToastUndo: string -> (unit -> unit) -> unit
    /// 一次性计时（计时器归宿主）；返回值可取消
    After: TimeSpan -> (unit -> unit) -> IDisposable
    /// 发出命令（宿主经 WsClient 发送，失败路径也在宿主）
    SendCommand: ClientCommand -> unit
    NewInvocation: unit -> Guid
}

/// 组合者：删除即撤销（§214）。登记待删 → 隐去行 → 当前对话落邻位 → 带「撤销」的提示条 → 宽限期后提交。
/// 计时器只盯最早到期的那一批；到期批次逐个提交再盯下一批。
type DeletionDesk(commands: CommandFeedbackTracker, surface: DeletionSurface) =
    let mutable pending = PendingDeletion.empty
    let mutable timer: IDisposable option = None

    let commit (token: Guid) =
        let next, ids = PendingDeletion.commit token pending
        pending <- next
        for id in ids do
            let command = DeleteConversation {| invocationId = surface.NewInvocation(); conversationId = id |}
            commands.TrackWithCompletion(
                command, None, ignore,
                (fun ok -> if not ok then surface.Toast "有对话没删掉：主机拒绝了这次请求，它已回到列表。" DeletionTone.Warning))
            surface.SendCommand command
        if not (List.isEmpty ids) then surface.PendingChanged pending

    let rec arm () =
        timer |> Option.iter (fun t -> t.Dispose())
        timer <- None
        match PendingDeletion.nextDue pending with
        | Some due ->
            let wait = due - surface.Now()
            let wait = if wait > TimeSpan.FromMilliseconds 1.0 then wait else TimeSpan.FromMilliseconds 1.0
            timer <-
                Some(surface.After wait (fun () ->
                    timer <- None
                    let next, tokens = PendingDeletion.tick (surface.Now()) pending
                    pending <- next
                    for token in tokens do commit token
                    arm ()))
        | None -> ()

    /// 待删集合（侧栏过滤、测试用）
    member _.Pending = pending

    /// 删除这些对话（单条或批量）：先隐去，宽限期后才发命令
    member _.Schedule(ids: Guid list) =
        let titled = ids |> List.map (fun id -> id, surface.TitleOf id |> Option.defaultValue "对话")
        let neighbor = PendingDeletion.neighborAfter (surface.ActiveConversation()) ids (surface.VisibleOrder())
        let deletingCurrent = surface.ActiveConversation() |> Option.exists (fun id -> List.contains id ids)
        match ids with
        | [ single ] -> surface.FocusAfterDelete single
        | _ -> ()
        let next, batch = PendingDeletion.scheduleAt (surface.Now()) titled pending
        pending <- next
        match batch with
        | None -> ()
        | Some batch ->
            surface.PendingChanged pending
            if deletingCurrent then surface.Open neighbor
            arm ()
            surface.ToastUndo batch.label (fun () ->
                let restored, ids = PendingDeletion.undo batch.token pending
                pending <- restored
                arm ()
                if not (List.isEmpty ids) then
                    surface.PendingChanged pending
                    if deletingCurrent && ids.Length = 1 then surface.Open(Some ids.Head)
                    surface.Render()
                    surface.Toast (PendingDeletion.restoredText ids.Length) DeletionTone.Success)
            surface.Render()

    /// 立即提交这一批（宽限期到；测试与退出前冲刷用）
    member _.Commit(token: Guid) = commit token

    /// 这条对话是否在宽限期里（列表快照过滤用）
    member _.IsPending(id: Guid) = PendingDeletion.isPending id pending
