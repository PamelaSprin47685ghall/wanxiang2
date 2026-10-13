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

/// 提交请求被拒的原因。
type Refusal<'P> =
    /// 同一 commandId 与 hash 已提交过：原提交 id 与位置。
    | Duplicate of id: uint64 * position: 'P
    /// 同一 commandId 但 hash 不同。
    | StampConflict of existingId: uint64
    /// 领域折叠拒绝了这批事件。
    | Rejected of reason: string
    /// 持久化方回复写入失败。
    | WriteFailed of reason: string

/// 账本接收的消息。
type LedgerIn<'E, 'P> =
    /// 请求提交。ticket 是调用方的关联标识，原样出现在回复里。
    | Submit of ticket: Guid * events: 'E list * at: DateTimeOffset * stamp: Stamp option
    /// 持久化方回复：AppendRequested 中的提交已写入，位于 position。
    | Appended of id: uint64 * position: 'P
    /// 持久化方回复：写入失败，已回到写前状态。
    | AppendFailed of id: uint64 * reason: string
    /// 重放：一条从存储读出的提交及其位置。只在没有进行中的提交时接受。
    | Replay of Commit<'E> * 'P

/// 账本发出的消息。
type LedgerOut<'E, 'P> =
    /// 请持久化方写入这条提交，并以 Appended / AppendFailed 回复。同一时刻至多一条在途。
    | AppendRequested of ticket: Guid * Commit<'E>
    /// 提交已成立。ticket = None 表示来自重放。
    | Committed of ticket: Guid option * Commit<'E> * position: 'P
    /// 提交请求被拒，账本不变。
    | Refused of ticket: Guid * Refusal<'P>
    /// 重放的提交不能成立（id 断档、commandId 重复、折叠失败）：这里就是损坏点，账本不变。
    | ReplayRejected of Commit<'E> * position: 'P * reason: string

/// 账本的完整可保存状态：领域状态、最后 id、幂等索引、每条提交的位置。
/// 'P 是「提交存在哪里」的标记，账本只保存和回传，不解释。
type Checkpoint<'S, 'P> =
    { state: 'S
      throughId: uint64
      /// commandId -> (hash, commitId)
      stamps: Map<string, string * uint64>
      /// positions[i] 是 id = i + 1 的位置
      positions: 'P list }
