namespace Wanxiang2.Theme

open System
open System.Collections.Generic

/// 活的可变值：订阅时先给当前值，之后每次 Set 都推送。控件经 Bind 订阅，管理者改值即跟随。
/// 只在 UI 线程上读写。
type LiveValue<'T when 'T: equality>(initial: 'T) =
    let mutable value = initial
    let subs = List<IObserver<'T>>()

    member _.Value = value

    /// 改值；与当前值相同时不推送
    member _.Set(next: 'T) =
        if next <> value then
            value <- next
            for s in List.ofSeq subs do s.OnNext next

    interface IObservable<'T> with
        member _.Subscribe(o: IObserver<'T>) =
            subs.Add o
            o.OnNext value
            { new IDisposable with member _.Dispose() = subs.Remove o |> ignore }

/// 活值的派生视图：源值每次推送都经 f 变换后推送（样式 Setter 经 ToBinding 订阅它）
module Live =
    let map (f: 'A -> 'B) (source: IObservable<'A>) : IObservable<'B> =
        { new IObservable<'B> with
            member _.Subscribe(o: IObserver<'B>) =
                source.Subscribe
                    { new IObserver<'A> with
                        member _.OnNext v = o.OnNext(f v)
                        member _.OnError e = o.OnError e
                        member _.OnCompleted() = o.OnCompleted() } }

    /// 两个活值合成一个
    let map2 (f: 'A -> 'B -> 'C) (a: LiveValue<'A>) (b: LiveValue<'B>) : IObservable<'C> =
        { new IObservable<'C> with
            member _.Subscribe(o: IObserver<'C>) =
                let push () = o.OnNext(f a.Value b.Value)
                let watch (x: IObservable<'T>) =
                    x.Subscribe { new IObserver<'T> with
                                    member _.OnNext _ = push ()
                                    member _.OnError e = o.OnError e
                                    member _.OnCompleted() = () }
                let da = watch a
                let db = watch b
                { new IDisposable with member _.Dispose() = da.Dispose(); db.Dispose() } }

/// 间距、圆角、描边、字形档（SpacingRamp 的刻度，作为活值的初始值）
[<RequireQualifiedAccess>]
type Space =
    | None
    | XXs
    | Xs
    | Sm
    | Md
    | Lg
    | Xl
    | X2l
    | X3l
    | X4l
    | RadiusXs
    | RadiusSm
    | RadiusMd
    | RadiusLg
    | RadiusPill
    | StrokeThin
    | StrokeThick
    | GlyphSm
    | GlyphMd
    | GlyphLg
    /// 区块标签字距（旧 Spacing.Tracking.section）
    | TrackingSection

module Space =
    open Wanxiang.UI

    let all =
        [ Space.None; Space.XXs; Space.Xs; Space.Sm; Space.Md; Space.Lg; Space.Xl; Space.X2l; Space.X3l; Space.X4l
          Space.RadiusXs; Space.RadiusSm; Space.RadiusMd; Space.RadiusLg; Space.RadiusPill
          Space.StrokeThin; Space.StrokeThick; Space.GlyphSm; Space.GlyphMd; Space.GlyphLg; Space.TrackingSection ]

    let initial (s: Space) =
        match s with
        | Space.None -> Spacing.spaceNone
        | Space.XXs -> Spacing.spaceXXs
        | Space.Xs -> Spacing.spaceXs
        | Space.Sm -> Spacing.spaceSm
        | Space.Md -> Spacing.spaceMd
        | Space.Lg -> Spacing.spaceLg
        | Space.Xl -> Spacing.spaceXl
        | Space.X2l -> Spacing.space2xl
        | Space.X3l -> Spacing.space3xl
        | Space.X4l -> Spacing.space4xl
        | Space.RadiusXs -> Spacing.Radius.xs
        | Space.RadiusSm -> Spacing.Radius.sm
        | Space.RadiusMd -> Spacing.Radius.md
        | Space.RadiusLg -> Spacing.Radius.lg
        | Space.RadiusPill -> Spacing.Radius.pill
        | Space.StrokeThin -> Spacing.Stroke.thin
        | Space.StrokeThick -> Spacing.Stroke.thick
        | Space.GlyphSm -> Spacing.Glyph.sm
        | Space.GlyphMd -> Spacing.Glyph.md
        | Space.GlyphLg -> Spacing.Glyph.lg
        | Space.TrackingSection -> Spacing.Tracking.section

/// 统一缓动 easeOutCubic 的 Avalonia 形态；曲线数学归 ScrollTween 包
type EaseOutCubicEasing() =
    inherit Avalonia.Animation.Easings.Easing()
    override _.Ease(t: float) = Wanxiang.UI.EaseOutCubic.ease t
