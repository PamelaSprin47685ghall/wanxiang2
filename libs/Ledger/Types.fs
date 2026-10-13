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

/// 提交请求被拒的原因。'Err 是折叠函数自己的错误类型。
type Refusal<'Err> =
    /// 同一 commandId 与 hash 已提交过：原提交 id 与区间。
    | Duplicate of id: uint64 * span: Span
    /// 同一 commandId 但 hash 不同。
    | StampConflict of existingId: uint64
    /// 领域折叠拒绝了这批事件。
    | Rejected of 'Err
    /// 持久化方回复写入失败。
    | WriteFailed of reason: string

/// 重放的提交为什么不能成立。
type ReplayProblem<'Err> =
    /// id 不紧接上一条。
    | IdGap of expected: uint64 * got: uint64
    /// 区间不紧接上一条的末尾（介质上有缺口或重叠）。
    | SpanGap of expectedStart: int64 * got: Span
    /// commandId 已出现过。
    | RepeatedStamp of commandId: string
    /// 折叠拒绝。
    | FoldRejected of 'Err
    /// 账本有在途或等待中的提交请求。
    | Busy

    /// 人能读懂的一句话。折叠错误用 %A 呈现。
    member p.Describe() : string =
        match p with
        | IdGap(e, g) -> sprintf "commit id gap: expected %d, got %d" e g
        | SpanGap(e, g) -> sprintf "commit span [%d, %d) does not start at %d (gap or overlap in the medium)" g.start g.``end`` e
        | RepeatedStamp id -> sprintf "commandId %s appears twice" id
        | FoldRejected err -> sprintf "fold rejected the commit: %A" err
        | Busy -> "ledger has live submits in progress; replay must finish first"

    override p.ToString() = p.Describe()

/// 账本接收的消息。
type LedgerIn<'E> =
    /// 请求提交。ticket 是调用方的关联标识，原样出现在回复里。
    | Submit of ticket: Guid * events: 'E list * at: DateTimeOffset * stamp: Stamp option
    /// 持久化方回复：AppendRequested 中的提交已写入，止于 endOffset（起点就是上一条的末尾）。
    | Appended of id: uint64 * endOffset: int64
    /// 持久化方回复：写入失败，已回到写前状态。
    | AppendFailed of id: uint64 * reason: string
    /// 重放：一条从存储读出的提交及其区间。只在没有进行中的提交时接受。
    | Replay of Commit<'E> * Span

/// 账本发出的消息。
type LedgerOut<'E, 'Err> =
    /// 请持久化方从 startOffset 起写入这条提交，并以 Appended / AppendFailed 回复。同一时刻至多一条在途。
    | AppendRequested of ticket: Guid * Commit<'E> * startOffset: int64
    /// 提交已成立。ticket = None 表示来自重放。
    | Committed of ticket: Guid option * Commit<'E> * span: Span
    /// 提交请求被拒，账本不变。
    | Refused of ticket: Guid * Refusal<'Err>
    /// 重放的提交不能成立：这里就是损坏点，账本不变。
    | ReplayRejected of Commit<'E> * span: Span * problem: ReplayProblem<'Err>

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
