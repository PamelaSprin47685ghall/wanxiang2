namespace Wanxiang2.WorkspaceState

open System

/// 断线重连的等待时长与退避数学。
module ReconnectBackoff =

    /// 首次重连的等待时长
    [<Literal>]
    let baseDelayMs = 1000

    /// 上限。再长用户会以为程序不再尝试了。
    [<Literal>]
    let maxDelayMs = 30000

    /// 下一次的等待时长：翻倍并封顶。
    let next (current: int) : int =
        let grown = (max baseDelayMs current) * 2
        min grown maxDelayMs

    /// 从一次失败开始，连续失败若干次后各次的等待时长。
    let schedule (failures: int) : int list =
        List.init failures id
        |> List.scan (fun delay _ -> next delay) baseDelayMs
        |> List.truncate failures
