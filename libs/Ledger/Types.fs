namespace Ledger

open System
open System.Text.Json.Nodes

/// 幂等戳：调用方给出命令标识与命令内容哈希（如何算哈希由调用方决定）。
type Stamp = { commandId: string; hash: string }

/// 一次原子提交。events 的顺序即应用顺序。
type Commit<'E> = { id: uint64; at: DateTimeOffset; stamp: Stamp option; events: 'E list }

/// 事件编解码。由调用方提供；本库只负责外壳。
type Codec<'E> = { encode: 'E -> JsonNode; decode: JsonNode -> Result<'E, string> }

/// 日志中的一条记录（与具体分帧无关）。
/// complete = false 表示尾部被截断；偏移由存储方定义，本库只回传。
type LogRecord = { payload: string; complete: bool; startOffset: int64; endOffset: int64 }

/// 存储能力，由调用方提供（例如用某个追加文件实现）。
/// readFrom：从偏移读出全部记录；append：追加一条并持久化，返回新的末尾偏移；
/// truncate：截断到偏移。
type Log = { readFrom: int64 -> LogRecord list; append: string -> int64; truncate: int64 -> unit }

/// 续播基线：状态、已包含的最后提交 id、对应日志偏移、幂等索引。
/// Checkpoint.empty 即从头重放。
type Checkpoint<'S> =
    { state: 'S
      throughId: uint64
      offset: int64
      /// commandId -> (hash, commitId)
      stamps: Map<string, string * uint64> }

module Checkpoint =
    let empty (state: 'S) : Checkpoint<'S> = { state = state; throughId = 0UL; offset = 0L; stamps = Map.empty }

/// 日志损坏的位置与原因。prefix 是损坏点之前的有效前缀；validUntil 之后的字节尚未被改动。
type Damage<'S> =
    { reason: string
      validUntil: int64
      /// 损坏点之后还有多少条记录（修复时会一并丢弃）
      recordsAfter: int
      prefix: Checkpoint<'S> }

/// 提交结果。
type SubmitResult<'E> =
    /// 已落盘并更新状态。
    | Committed of Commit<'E>
    /// 同一 commandId 与 hash 已提交过，返回原提交 id，未写入。
    | Duplicate of uint64
    /// 同一 commandId 但 hash 不同，未写入。
    | StampConflict of existingId: uint64
    /// 领域折叠拒绝了这批事件，未写入。
    | Rejected of string
    /// 存储写入失败，已回滚到写前偏移，状态不变。
    | WriteFailed of exn
