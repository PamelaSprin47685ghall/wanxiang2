namespace Wanxiang.UI

open System
open System.Collections.Generic

[<Struct>]
type Argb = { A: byte; R: byte; G: byte; B: byte }

[<RequireQualifiedAccess>]
type ThemeStart =
    | Snap
    | Tween

type ThemeFrame<'K> = { Colors: ('K * Argb) list; Finished: bool }

// 照抄旧 Design/Tokens.fs apply / advanceFrame / completeTransition：
// SolidColorBrush 换成键 'K，palette 换成「本次给的目标色」表，帧计时器与 Changed 去掉，
// DateTime.UtcNow 换成 now。原地改 brush.Color 换成返回颜色列表。
type ThemeTween<'K when 'K: equality>() =
    let mutable transitionFrom = Dictionary<'K, Argb>()
    let mutable palette = Dictionary<'K, Argb>()
    let mutable transitionStarted = DateTimeOffset.MinValue
    let mutable transitionSegmentMs = 200.0
    static member DurationMs = 200.0
    static member FrameTicks = max 1L (TimeSpan.FromMilliseconds(200.0).Ticks / 12L)
    static member LerpChannel (from: byte) (target: byte) (t: float) =
        let raw = Math.Round(float from + (float target - float from) * t) |> int
        if raw < 0 then 0uy elif raw > 255 then 255uy else byte raw
    static member Lerp (from: Argb) (target: Argb) (t: float) =
        let l = ThemeTween<'K>.LerpChannel
        { A = l from.A target.A t; R = l from.R target.R t; G = l from.G target.G t; B = l from.B target.B t }
    member _.IsRunning = transitionFrom.Count > 0
    member _.Apply(now: DateTimeOffset, sameMode: bool, durationMs: float, brushes: ('K * Argb * Argb) list) : ThemeStart =
        let ofMode () =
            let d = Dictionary<'K, Argb>()
            for k, _, t in brushes do d[k] <- t
            d
        if sameMode || durationMs <= 0.0 then
            palette <- ofMode ()
            transitionFrom.Clear()
            ThemeStart.Snap
        else
            let rolloverT =
                if transitionFrom.Count > 0 then
                    let elapsed = (now - transitionStarted).TotalMilliseconds
                    min 1.0 (elapsed / transitionSegmentMs)
                else
                    0.0
            let previousFrom = transitionFrom
            let previousPalette = palette
            palette <- ofMode ()
            transitionFrom <- Dictionary<'K, Argb>()
            for brush, color, _ in brushes do
                let startColor =
                    if previousFrom.Count > 0 then
                        ThemeTween<'K>.Lerp previousFrom.[brush] previousPalette[brush] rolloverT
                    else
                        color
                transitionFrom.Add(brush, startColor)
            transitionStarted <- now
            transitionSegmentMs <- durationMs
            ThemeStart.Tween
    member _.Frame(now: DateTimeOffset, durationMs: float) : ThemeFrame<'K> =
        if transitionFrom.Count > 0 then
            if durationMs <= 0.0 then
                let colors = [ for KeyValue(brush, _) in transitionFrom -> brush, palette[brush] ]
                transitionFrom.Clear()
                { Colors = colors; Finished = true }
            else
                let elapsed = (now - transitionStarted).TotalMilliseconds
                let t = min 1.0 (elapsed / transitionSegmentMs)
                let colors = [ for KeyValue(brush, f) in transitionFrom -> brush, ThemeTween<'K>.Lerp f palette[brush] t ]
                if t >= 1.0 then
                    transitionFrom.Clear()
                { Colors = colors; Finished = t >= 1.0 }
        else { Colors = []; Finished = false }
    member _.Complete() : ('K * Argb) list =
        transitionFrom.Clear()
        [ for KeyValue(brush, c) in palette -> brush, c ]
