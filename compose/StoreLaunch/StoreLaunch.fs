namespace Wanxiang.App

open System
open Wanxiang.Store

/// 入口处打开事件存储的两条路径（从旧 Wanxiang.App/Program.fs 拆出）。
/// 协作：EventStore（打开、检查、修复）、RecoveryBundle（修复前建证据包）、LedgerStore（快照结论）。
/// 错误返回的是要原样打到 stderr 的整行文字；退出码由入口决定（都是 1）。
module StoreLaunch =

    /// 打开存储；数据目录被占用与读不了分开报（文字与旧实现相同）。
    let private openStore (dataDir: string) (manual: bool) : Result<EventStore, string> =
        try Ok(if manual then new EventStore(dataDir, policy = LedgerStore.SnapshotPolicy.Manual) else new EventStore(dataDir))
        with
        | :? InvalidOperationException as e -> Error(sprintf "data directory locked: %s" e.Message)
        | e -> Error(sprintf "event log not readable: %s" e.Message)

    /// `doctor --fix`（决策 51、105/Q110）：持锁后检查，有损坏先建证据包再截断。
    let fix (dataDir: string) (logInfo: string -> unit) : Result<unit, string> =
        match openStore dataDir true with
        | Error e -> Error e
        | Ok store ->
            let result =
                match Inspect.ofStore store with
                | Healthy _ -> Ok []
                | RepairRequired(_, plan) ->
                    match RecoveryBundle.create dataDir plan with
                    | Error e -> Error e
                    | Ok bundle ->
                        logInfo (sprintf "recovery bundle created at %s" bundle)
                        match Repair.repairWith store with
                        | Ok truncatedFiles ->
                            for f in truncatedFiles do
                                logInfo (sprintf "truncated %s" f)
                            Ok truncatedFiles
                        | Error e -> Error e
                | OutcomeUnknown(_, commitId, reason) ->
                    logInfo (sprintf "outcome of commit %d unknown (%s); reopening settled it, nothing to truncate" commitId reason)
                    Ok []
                | HostOwned _ -> Error "the event log is held by a running host; stop it first"
                | UnsupportedFormat msg -> Error msg
                | Unreadable msg -> Error msg
            store.Shutdown()
            result |> Result.map ignore |> Result.mapError (sprintf "fix failed: %s")

    /// 正常启动（S-001/S-003）：只读检查，损坏时以最长有效前缀进入只读降级模式，不自动截断；
    /// 只有格式不支持、读不了才拒绝启动。打开的存储直接交给主机，不再重放第二次。
    let openForStartup (dataDir: string) (logInfo: string -> unit) : Result<EventStore, string> =
        match openStore dataDir false with
        | Error e -> Error e
        | Ok store ->
            let r = Inspect.ofStore store
            match r with
            | Healthy o | RepairRequired(o, _) ->
                match o.startup.snapshot with
                | LedgerStore.Used through -> logInfo (sprintf "snapshot %d loaded; replayed %d commits after it" through o.startup.replayedCommits)
                | LedgerStore.Absent -> logInfo "no snapshot; full replay"
                | LedgerStore.Discarded why -> logInfo (sprintf "snapshot discarded (%s); full replay" why)
            | _ -> ()
            match r with
            | Healthy _ | HostOwned _ -> Ok store
            | OutcomeUnknown(o, commitId, reason) ->
                logInfo (sprintf "outcome of commit %d unknown (%s); starting read-only (valid through commit %d)" commitId reason o.lastCommitId)
                Ok store
            | RepairRequired(o, plan) ->
                logInfo (
                    sprintf
                        "event log damaged at %s (%s); starting read-only degraded mode (valid through commit %d). Run `wanxiang doctor --fix` to repair."
                        plan.damage.file
                        plan.damage.reason
                        o.lastCommitId)
                Ok store
            | UnsupportedFormat msg -> Error(sprintf "unsupported event log format: %s" msg)
            | Unreadable msg -> Error(sprintf "event log not readable: %s" msg)
