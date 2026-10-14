namespace Wanxiang.UI

// 照抄旧 Views/MessageCard.fs toolActivityBlock：计数由状态列表算出（这里由计数反推一份状态列表）、
// overviewBaseText、overviewBrush、overviewIcon（刷子与控件换成档位）
type ToolCounts = { running: int; awaiting: int; success: int; failed: int; unknown: int }
[<RequireQualifiedAccess>]
type OverviewTone = | Warning | Accent | Danger | Muted
[<RequireQualifiedAccess>]
type OverviewIcon = | Info | Spinner | Alert | Check

type private S = Running | AwaitingApproval | Success | Failed | Unknown

module ToolOverview =
    let private statusesOf (c: ToolCounts) =
        List.replicate c.running Running @ List.replicate c.awaiting AwaitingApproval @ List.replicate c.success Success
        @ List.replicate c.failed Failed @ List.replicate c.unknown Unknown

    let total (c: ToolCounts) = List.length (statusesOf c)

    let text (c: ToolCounts) =
        let statuses = statusesOf c
        let total = List.length statuses
        let runningCount = statuses |> List.filter (function Running -> true | _ -> false) |> List.length
        let awaitingCount = statuses |> List.filter (function AwaitingApproval -> true | _ -> false) |> List.length
        let successCount = statuses |> List.filter (function Success -> true | _ -> false) |> List.length
        let failedCount = statuses |> List.filter (function Failed -> true | _ -> false) |> List.length
        let unknownCount = statuses |> List.filter (function Unknown -> true | _ -> false) |> List.length
        if total = 1 then
            match List.head statuses with
            | Running -> "调用 1 个工具 · 执行中"
            | AwaitingApproval -> "调用 1 个工具 · 等待批准"
            | Success -> "调用 1 个工具 · 已完成"
            | Failed -> "调用 1 个工具 · 执行失败"
            | Unknown -> "调用 1 个工具 · 结果未知"
        else
            let parts = [
                if successCount > 0 then yield sprintf "%d 个完成" successCount
                if awaitingCount > 0 then yield sprintf "%d 个等待批准" awaitingCount
                if runningCount > 0 then yield sprintf "%d 个执行中" runningCount
                if failedCount > 0 then yield sprintf "%d 个失败" failedCount
                if unknownCount > 0 then yield sprintf "%d 个结果未知" unknownCount
            ]
            let summary =
                if List.isEmpty parts then "无状态"
                else String.concat " · " parts
            sprintf "调用 %d 个工具 · %s" total summary

    let tone (c: ToolCounts) =
        if c.awaiting > 0 then OverviewTone.Warning
        elif c.running > 0 then OverviewTone.Accent
        elif c.failed > 0 then OverviewTone.Danger
        elif c.unknown > 0 then OverviewTone.Warning
        else OverviewTone.Muted

    let icon (c: ToolCounts) =
        if c.awaiting > 0 then OverviewIcon.Info
        elif c.running > 0 then OverviewIcon.Spinner
        elif c.failed > 0 then OverviewIcon.Alert
        elif c.unknown > 0 then OverviewIcon.Info
        else OverviewIcon.Check
