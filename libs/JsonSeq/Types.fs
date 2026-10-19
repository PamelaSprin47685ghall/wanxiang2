namespace Ledger

open System

/// 幂等戳：命令标识与命令内容哈希（哈希怎么算是调用方的事）。
type Stamp = { commandId: string; hash: string }

/// 一次原子提交。events 的顺序即应用顺序。
type Commit<'E> = { id: uint64; at: DateTimeOffset; stamp: Stamp option; events: 'E list }

module Commit =
    /// 换事件的表示（例如领域事件 <-> JSON），外壳不变。
    let map (f: 'A -> 'B) (c: Commit<'A>) : Commit<'B> =
        { id = c.id; at = c.at; stamp = c.stamp; events = List.map f c.events }

/// 提交在只追加介质上占据的区间 [start, end)。相邻提交首尾相接。
type Span = { start: int64; ``end``: int64 }

/// Propose 的裁决。只有 Proposed 需要组装方继续处理：持久化成功后 Record，确知未写入则 Abandon。
type Decision<'E, 'Err> =
    /// 下一条提交（id 已分配），尚未记入。
    | Proposed of Commit<'E>
    /// 同一 commandId 与 hash 已提交过：原提交 id 与区间。
    | Duplicate of id: uint64 * span: Span
    /// 同一 commandId 但 hash 不同。
    | StampConflict of existingId: uint64
    /// 领域折叠拒绝了这批事件。'Err 是折叠函数自己的错误类型。
    | Rejected of 'Err

/// 一条已持久化的提交为什么不能记入。
type RecordProblem<'Err> =
    /// id 不紧接上一条。
    | IdGap of expected: uint64 * got: uint64
    /// 区间不紧接上一条的末尾（介质上有缺口或重叠）。
    | SpanGap of expectedStart: int64 * got: Span
    /// commandId 已出现过。
    | RepeatedStamp of commandId: string
    /// 折叠拒绝。
    | FoldRejected of 'Err
    /// 有一条提议未落定，而记入的不是它。
    | Unsettled of proposedId: uint64

    /// 人能读懂的一句话。折叠错误用 %A 呈现。
    member p.Describe() : string =
        match p with
        | IdGap(e, g) -> sprintf "commit id gap: expected %d, got %d" e g
        | SpanGap(e, g) -> sprintf "commit span [%d, %d) does not start at %d (gap or overlap in the medium)" g.start g.``end`` e
        | RepeatedStamp id -> sprintf "commandId %s appears twice" id
        | FoldRejected err -> sprintf "fold rejected the commit: %A" err
        | Unsettled id -> sprintf "proposal %d is not settled; record or abandon it first" id

    override p.ToString() = p.Describe()

/// 账本的完整可保存状态。ends[i] 是 id = i + 1 的末尾偏移；起点由前一条末尾（或 origin）导出，
/// 每条提交 8 字节。
type Checkpoint<'S> =
    { state: 'S
      throughId: uint64
      /// commandId -> (hash, commitId)
      stamps: Map<string, string * uint64>
      /// 第一条提交的起点
      origin: int64
      ends: int64[] }

module Checkpoint =
    /// 换领域状态的表示（例如 'S <-> JSON），其余不变。
    let map (f: 'A -> 'B) (cp: Checkpoint<'A>) : Checkpoint<'B> =
        { state = f cp.state; throughId = cp.throughId; stamps = cp.stamps; origin = cp.origin; ends = cp.ends }
