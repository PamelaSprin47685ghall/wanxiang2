namespace Wanxiang.UI

open System

/// 复制按钮此刻该画成什么样
[<RequireQualifiedAccess>]
type CopyVisual =
    /// 复制图标、原提示
    | Default
    /// 勾号「已复制」，透明度 1.0
    | Confirmed
    /// 勾号「已复制」，透明度先压到淡出值（旧 Tokens.opacityCopyConfirmFade）
    | Fading

/// 旧 MessageCard 复制按钮（两处）与 MarkdownRenderer 代码块复制按钮的确认记账：
/// 复制后先淡一下（FadeMs），保持勾号 HoldMs 后复位；减弱动效不做淡出。
/// 计时器、图标、提示与读屏名仍归宿主：宿主在 NextDue 时刻调 Tick 取当前形态。
type CopyConfirm() =
    let mutable confirmedUntil: DateTimeOffset option = None
    let mutable fadeUntil: DateTimeOffset option = None

    /// 勾号保持时长（旧 MotionLedger.copyConfirmationHold）
    static member HoldMs = 1500.0
    /// 淡出时长（旧 MotionLedger.copyConfirmationFade）
    static member FadeMs = 150.0

    /// 已复制：重新起算保持与淡出；reduced = 减弱动效
    member _.Copied(now: DateTimeOffset, reduced: bool) =
        confirmedUntil <- Some(now.AddMilliseconds CopyConfirm.HoldMs)
        fadeUntil <- if reduced then None else Some(now.AddMilliseconds CopyConfirm.FadeMs)

    /// 按 now 推进并给出形态；到点的阶段在这里结束
    member _.Tick(now: DateTimeOffset) : CopyVisual =
        match fadeUntil with
        | Some t when now >= t -> fadeUntil <- None
        | _ -> ()
        match confirmedUntil with
        | Some t when now >= t ->
            confirmedUntil <- None
            fadeUntil <- None
        | _ -> ()
        match confirmedUntil, fadeUntil with
        | None, _ -> CopyVisual.Default
        | Some _, Some _ -> CopyVisual.Fading
        | Some _, None -> CopyVisual.Confirmed

    /// 下一个需要调 Tick 的时刻；None 表示没有在途阶段，宿主可停表
    member _.NextDue: DateTimeOffset option =
        match fadeUntil, confirmedUntil with
        | Some f, _ -> Some f
        | None, c -> c

    /// 立即复位（旧 restoreDefaultState）
    member _.Reset() =
        confirmedUntil <- None
        fadeUntil <- None

    /// 按钮离开可视树：与 Reset 相同，旧代码在 DetachedFromVisualTree 里复位
    member this.Detached() = this.Reset()
