namespace Wanxiang.UI

/// 各状态的工具调用数
type ToolCounts =
    {
        running: int
        awaiting: int
        success: int
        failed: int
        unknown: int
    }

/// 概览的颜色档（限定访问）
[<RequireQualifiedAccess>]
type OverviewTone =
    | Warning
    | Accent
    | Danger
    | Muted

/// 概览的图标（限定访问）
[<RequireQualifiedAccess>]
type OverviewIcon =
    | Info
    | Spinner
    | Alert
    | Check

/// 旧 Views/MessageCard.fs toolActivityBlock 的概览文案、颜色与图标
module ToolOverview =

    let total (c: ToolCounts) =
        c.running + c.awaiting + c.success + c.failed + c.unknown

    let text (c: ToolCounts) =
        let n = total c

        if n = 1 then
            if c.running = 1 then "调用 1 个工具 · 执行中"
            elif c.awaiting = 1 then "调用 1 个工具 · 等待批准"
            elif c.success = 1 then "调用 1 个工具 · 已完成"
            elif c.failed = 1 then "调用 1 个工具 · 执行失败"
            else "调用 1 个工具 · 结果未知"
        else
            let parts =
                [
                    if c.success > 0 then
                        sprintf "%d 个完成" c.success
                    if c.awaiting > 0 then
                        sprintf "%d 个等待批准" c.awaiting
                    if c.running > 0 then
                        sprintf "%d 个执行中" c.running
                    if c.failed > 0 then
                        sprintf "%d 个失败" c.failed
                    if c.unknown > 0 then
                        sprintf "%d 个结果未知" c.unknown
                ]

            sprintf
                "调用 %d 个工具 · %s"
                n
                (if List.isEmpty parts then
                     "无状态"
                 else
                     String.concat " · " parts)

    /// 等待批准优先于执行中：要用户动手的最先被看到；有待定的绝不显示完成
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
