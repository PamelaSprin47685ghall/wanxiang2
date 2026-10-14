namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 一个命令对应的 UI 后续动作。只有主机 CommandCommitted 后才执行。
type CommandFeedback = {
    successMessage: string option
    onCommitted: unit -> unit
    onCompleted: bool -> unit
}

/// request → commit/reject 的轻量状态机，避免 MainView 散落维护 invocation dictionary。
type CommandFeedbackTracker() =
    let pending = Dictionary<Guid, CommandFeedback>()
    /// 决策 443：记录发出时刻，供「主机迟迟不回应」超时判定。
    let sentAt = Dictionary<Guid, DateTimeOffset * bool>()

    member _.Count = pending.Count

    /// 登记一个已发出的命令。expirable = false 的命令（消息投递类）由发件箱自己判定超时。
    member _.TrackId(invocationId: Guid, expirable: bool, successMessage: string option, onCommitted: unit -> unit) =
        sentAt[invocationId] <- (DateTimeOffset.UtcNow, expirable)
        pending[invocationId] <-
            { successMessage = successMessage
              onCommitted = onCommitted
              onCompleted = ignore }

    member _.TrackIdWithCompletion(
        invocationId: Guid,
        expirable: bool,
        successMessage: string option,
        onCommitted: unit -> unit,
        onCompleted: bool -> unit
    ) =
        sentAt[invocationId] <- (DateTimeOffset.UtcNow, expirable)
        pending[invocationId] <-
            { successMessage = successMessage
              onCommitted = onCommitted
              onCompleted = onCompleted }

    member _.Commit(invocationId: Guid) =
        match pending.TryGetValue invocationId with
        | true, feedback ->
            pending.Remove invocationId |> ignore
            sentAt.Remove invocationId |> ignore
            Some feedback
        | _ -> None

    member _.Reject(invocationId: Guid) =
        match pending.TryGetValue invocationId with
        | true, feedback ->
            pending.Remove invocationId |> ignore
            sentAt.Remove invocationId |> ignore
            Some feedback
        | _ -> None

    member _.RejectAll() =
        let callbacks = pending.Values |> Seq.map _.onCompleted |> Array.ofSeq
        pending.Clear()
        sentAt.Clear()
        callbacks |> Array.iter (fun callback -> callback false)

    member _.Clear() =
        pending.Clear()
        sentAt.Clear()

    /// 取出超过 timeout 仍未得到 committed/rejected 的命令（决策 443）。
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : CommandFeedback list =
        let due =
            [ for KeyValue(id, (at, canExpire)) in sentAt do
                  if canExpire && now - at >= timeout then id ]
        [ for id in due do
              sentAt.Remove id |> ignore
              match pending.TryGetValue id with
              | true, feedback ->
                  pending.Remove id |> ignore
                  feedback
              | _ -> () ]
