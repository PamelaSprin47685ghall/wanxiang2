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
