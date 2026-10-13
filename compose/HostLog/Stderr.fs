namespace Wanxiang.Server

open Wanxiang.Core

/// stderr 结构化 JSON Lines 输出（决策 198/41），公开面与旧 `Stderr` 模块相同。
/// 机制在库 JsonLinesLog；本模块拥有进程内唯一的一份日志（写 stderr、最近错误上限 200），
/// 以及与领域相关的两种截尾记录。
module Stderr =

    let private log = JsonLinesLog((fun line -> eprintfn "%s" line), (fun () -> System.DateTimeOffset.UtcNow), 200)

    let setInstanceId (id: string) = log.SetInstanceId id
    let registerSecrets (secrets: string seq) = log.RegisterSecrets secrets
    let clearSecrets () = log.ClearSecrets()
    let redact (text: string) : string = log.Redact text
    let write (event: string) (fields: (string * obj) list) = log.Error(event, fields)
    let info (message: string) = log.Info message

    /// 运行时截尾记录（决策 40）：字段 file、commitId、byteOffset、exceptionType、exceptionMessage、rawCommit、action。
    let truncated (commit: Events.Commit) (file: string) (byteOffset: int64) (err: WanxiangError) =
        write
            "jsonseq-tail-truncated"
            [ "phase", "runtime-commit"
              "file", file
              "commitId", commit.id
              "byteOffset", byteOffset
              "exceptionType", WanxiangError.code err
              "exceptionMessage", WanxiangError.message err
              "action", "truncated-and-id-reused"
              "rawCommit", redact (CommitCodec.commitToJsonLine commit) ]

    /// 启动截尾记录。
    let replayTruncated (file: string) (reason: string) =
        write "jsonseq-replay-truncated" [ "phase", "startup-replay"; "file", file; "reason", reason ]

    /// 最近错误事件 JSONL（support bundle 用；已脱敏）。
    let recentErrorLines () : string list = log.RecentErrors
