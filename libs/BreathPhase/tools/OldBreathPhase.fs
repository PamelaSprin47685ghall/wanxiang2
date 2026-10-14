namespace Wanxiang.UI

open System

// 照抄旧 Views/ChatView.fs StartSkeletonBreathing：timer.Tick 体即 Step，分支即 Start
type BreathPhase() =
    let mutable skeletonOpacityPhase = 0.0
    static member FrameMs = 50.0
    static member StepRadians = 0.12
    member _.Phase = skeletonOpacityPhase
    member _.Start(reduced: bool, attached: bool) : bool =
        if reduced then false
        elif not attached then false
        else
            skeletonOpacityPhase <- 0.0
            true
    member _.Step() : float =
        skeletonOpacityPhase <- skeletonOpacityPhase + 0.12
        let alpha = 0.75 + 0.2 * Math.Sin(skeletonOpacityPhase)
        alpha
