namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 配置写入同样遵循 request → ConfigApplied 的权威确认。
type ConfigFeedback = {
    successMessage: string
    onCompleted: bool -> unit
}

type ConfigFeedbackTracker() =
    let pending = Dictionary<Guid, ConfigFeedback>()
    let sentAt = Dictionary<Guid, DateTimeOffset>()

    member _.Count = pending.Count

    member _.Track(requestId: Guid, successMessage: string, onCompleted: bool -> unit) =
        sentAt[requestId] <- DateTimeOffset.UtcNow
        pending[requestId] <-
            { successMessage = successMessage
              onCompleted = onCompleted }

    member _.Resolve(requestId: Guid) =
        match pending.TryGetValue requestId with
        | true, feedback ->
            pending.Remove requestId |> ignore
            sentAt.Remove requestId |> ignore
            Some feedback
        | _ -> None

    /// 决策 443：旧主机会静默丢弃不认识的配置请求；超时后让调用方退出 pending。
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : ConfigFeedback list =
        let due = [ for KeyValue(id, at) in sentAt do if now - at >= timeout then id ]
        [ for id in due do
              sentAt.Remove id |> ignore
              match pending.TryGetValue id with
              | true, feedback ->
                  pending.Remove id |> ignore
                  feedback
              | _ -> () ]

    /// 连接断开时让所有等待中的编辑器退出 pending，避免永久灰掉。
    member _.RejectAll() =
        let callbacks = pending.Values |> Seq.map _.onCompleted |> Array.ofSeq
        sentAt.Clear()
        pending.Clear()
        callbacks |> Array.iter (fun callback -> callback false)

/// config.applied 到达后的提示（旧 AppShell.HandleEvent 的 ConfigApplied 分支）。
[<RequireQualifiedAccess>]
type ConfigAppliedNotice =
    | Success of text: string
    | Failure of text: string
    /// 调用方（首次运行卡片）自己会说话，这里不叠 toast。
    | Silent

module ConfigApplied =

    /// tracked：这次回执对上了一个登记过的请求时，是它登记的成功文案；没对上为 None。
    /// 返回提示与是否重新拉取目录（成功才拉）。
    let notice (tracked: string option) (ok: bool) (errors: string list) : ConfigAppliedNotice * bool =
        let failure () = ConfigAppliedNotice.Failure(String.Join("；", errors))
        match tracked with
        | Some successMessage ->
            if ok then
                (if String.IsNullOrEmpty successMessage then ConfigAppliedNotice.Silent else ConfigAppliedNotice.Success successMessage), true
            else failure (), false
        | None ->
            if ok then ConfigAppliedNotice.Success "配置已保存", true
            else failure (), false

/// 主机配置有误 → 恢复的提示（迁自旧 AppShell 的 ConfigChanged / HostConfigRejected 分支）。
module ConfigIssueNotice =
    /// ConfigChanged 时：只在之前确有「配置有误」时提示一次。
    let recovered (hadIssue: bool) : string option = if hadIssue then Some "主机配置已恢复正常。" else None
    /// HostConfigRejected 时：只在第一次出现有误时把摘要 toast 出来，之后只更新状态行。
    let rejected (alreadyHadIssue: bool) (summary: string) : string option = if alreadyHadIssue then None else Some summary
