namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 一个颜色的四个通道（与 Avalonia Color.FromArgb 同序）
[<Struct>]
type Argb = { A: byte; R: byte; G: byte; B: byte }

/// Apply 的结果
[<RequireQualifiedAccess>]
type ThemeStart =
    /// 同步到位：宿主把每支画笔直接设成目标色、停帧
    | Snap
    /// 开始补间：宿主启动帧计时器，每帧调 Frame
    | Tween

/// 一帧：要写的颜色；Finished 为 true 时补间结束，宿主停帧
type ThemeFrame<'K> =
    {
        Colors: ('K * Argb) list
        Finished: bool
    }

/// 旧 Design/Tokens.fs 主题颜色补间（200-320）的记账：每支画笔本段的起点色、目标色、起始时刻与时长；
/// 中途再次切换时按前段已走的时间算出逻辑当前色作为新起点（后来的切换接管前一段）。
/// 画笔、帧计时器、跨线程投递与 Changed 事件仍归宿主；颜色混合只用本库自己的逐通道插值。
type ThemeTween<'K when 'K: equality>() =
    let mutable from = Dictionary<'K, Argb>()
    let target = Dictionary<'K, Argb>()
    let mutable started = DateTimeOffset.MinValue
    let mutable segmentMs = ThemeTween<'K>.DurationMs

    /// 补间时长（旧 MotionLedger.themeColorTransition）
    static member DurationMs = 200.0
    /// 帧间隔：时长 / 12（旧 colorTransitionFrame，按 tick 取整）
    static member FrameTicks = max 1L (TimeSpan.FromMilliseconds(200.0).Ticks / 12L)

    /// 单通道插值；t = 1 时四舍五入精确落在目标
    static member LerpChannel (a: byte) (b: byte) (t: float) =
        let raw = Math.Round(float a + (float b - float a) * t) |> int

        if raw < 0 then 0uy
        elif raw > 255 then 255uy
        else byte raw

    static member Lerp (a: Argb) (b: Argb) (t: float) =
        let l = ThemeTween<'K>.LerpChannel

        {
            A = l a.A b.A t
            R = l a.R b.R t
            G = l a.G b.G t
            B = l a.B b.B t
        }

    member _.IsRunning = from.Count > 0

    /// 切换主题。brushes：每支画笔的 (键, 当前色, 新主题的目标色)；
    /// sameMode = 同主题重放；durationMs ≤ 0 = 减弱动效
    member _.Apply
        (now: DateTimeOffset, sameMode: bool, durationMs: float, brushes: ('K * Argb * Argb) list)
        : ThemeStart =
        if sameMode || durationMs <= 0.0 then
            from.Clear()
            target.Clear()

            for k, _, t in brushes do
                target[k] <- t

            ThemeStart.Snap
        else
            let rolloverT =
                if from.Count > 0 then
                    min 1.0 ((now - started).TotalMilliseconds / segmentMs)
                else
                    0.0

            let previousFrom = from
            let next = Dictionary<'K, Argb>()

            for k, current, _ in brushes do
                let start =
                    if previousFrom.Count > 0 then
                        ThemeTween<'K>.Lerp previousFrom[k] target[k] rolloverT
                    else
                        current

                next.Add(k, start)

            from <- next
            target.Clear()

            for k, _, t in brushes do
                target[k] <- t

            started <- now
            segmentMs <- durationMs
            ThemeStart.Tween

    /// 推进一帧；durationMs ≤ 0（运行中被改成减弱动效）立即到位
    member _.Frame(now: DateTimeOffset, durationMs: float) : ThemeFrame<'K> =
        if from.Count = 0 then
            { Colors = []; Finished = false }
        elif durationMs <= 0.0 then
            let colors = [ for KeyValue(k, t) in target -> k, t ]
            from.Clear()
            { Colors = colors; Finished = true }
        else
            let t = min 1.0 ((now - started).TotalMilliseconds / segmentMs)
            let colors = [ for KeyValue(k, f) in from -> k, ThemeTween<'K>.Lerp f target[k] t ]

            if t >= 1.0 then
                from.Clear()

            { Colors = colors; Finished = t >= 1.0 }

    /// 立即收尾（旧 completeTransition）：返回目标色，宿主写上并停帧
    member _.Complete() : ('K * Argb) list =
        from.Clear()
        [ for KeyValue(k, t) in target -> k, t ]
