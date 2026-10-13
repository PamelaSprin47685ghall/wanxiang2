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

/// 对一次提交请求的裁决。裁决本身不改变账本。
type Decision<'E, 'P> =
    /// 可以提交：调用方把它持久化后，用 Record 交回账本。
    | Accept of Commit<'E>
    /// 同一 commandId 与 hash 已提交过：原提交 id 及其位置。
    | Duplicate of id: uint64 * position: 'P
    /// 同一 commandId 但 hash 不同。
    | StampConflict of existingId: uint64
    /// 领域折叠拒绝了这批事件。
    | Rejected of string

/// 账本的完整可保存状态：领域状态、最后 id、幂等索引、每条提交的位置。
/// 'P 是「提交存在哪里」的标记，账本只保存和回传，不解释。
type Checkpoint<'S, 'P> =
    { state: 'S
      throughId: uint64
      /// commandId -> (hash, commitId)
      stamps: Map<string, string * uint64>
      /// positions[i] 是 id = i + 1 的位置
      positions: 'P list }
