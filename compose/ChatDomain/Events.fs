namespace Wanxiang.Core

open System

module Events =

    let eventType (ev: EventData) : string = EventJson.eventType ev

    let eventVersion (_ev: EventData) : int = EventJson.Version

    /// 一次原子提交：一条 NDJSON 行。events 数组顺序即领域应用顺序。
    type Commit =
        {
            formatVersion: int
            id: CommitId
            /// 进程世代标识（RFC 5848 Reboot Session ID 思路）：重启后新 bootId，
            /// 用于检测序号回退与跨世代的提交边界。同一进程内所有提交共享一个 bootId。
            bootId: Guid
            /// 提交来源（如 "wanxiang"）：诊断用，不参与幂等。
            source: string
            committedAtUtc: DateTimeOffset
            commandId: string option
            commandType: string option
            /// 规范化命令载荷的 SHA-256（用于幂等冲突检测）
            commandHash: string option
            events: EventData list
        }

    module Commit =
        let create (id: CommitId) (nowUtc: DateTimeOffset) (events: EventData list) : Commit =
            {
                formatVersion = Constants.FormatVersion
                id = id
                bootId = Constants.DefaultBootId
                source = Constants.DefaultSource
                committedAtUtc = nowUtc
                commandId = None
                commandType = None
                commandHash = None
                events = events
            }

        let withCommand (commandId: string) (commandType: string) (commandHash: string) (commit: Commit) : Commit =
            { commit with
                commandId = Some commandId
                commandType = Some commandType
                commandHash = Some commandHash
            }

        /// 指定进程世代（bootId）与来源：单写者创建提交时用。
        /// 默认 create 用哨兵值，仅测试/无服务端场景出现。
        let withOrigin (bootId: Guid) (source: string) (commit: Commit) : Commit =
            { commit with
                bootId = bootId
                source = source
            }
