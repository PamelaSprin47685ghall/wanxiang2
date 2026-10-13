namespace Wanxiang.Server

/// 快照在启动检查里的结论（与存储的类型解耦，由调用方映射）。
[<RequireQualifiedAccess>]
type SnapshotState =
    | Absent
    | Used of throughCommit: uint64 * replayedCommits: int
    | Discarded of reason: string
    | NotChecked of message: string

/// 诊断检查「snapshot」的文字（从旧 Doctor 拆出）。快照问题不算故障：ok 恒为 true，只报告。
module SnapshotCheck =

    let run (state: SnapshotState) : bool * string =
        match state with
        | SnapshotState.Absent -> true, "no snapshot (full replay on next start)"
        | SnapshotState.Used(through, n) -> true, sprintf "valid through commit %d (%d commits replayed after it)" through n
        | SnapshotState.Discarded reason -> true, sprintf "discarded: %s (falls back to full replay)" reason
        | SnapshotState.NotChecked m -> true, sprintf "not checked: %s" m
