namespace Wanxiang.UI

open System

/// 旧 Views/ChatView.fs 骨架屏呼吸（940-975）的相位记账：每拍相位加 0.12 弧度，
/// 透明度 0.75 + 0.2 · sin(相位)，落在 0.55 ~ 0.95。与流式光标的 CaretBreath（按时间算）是两回事：
/// 这里按拍数走，拍子由宿主的 DispatcherTimer（旧 MotionLedger.skeletonBreathFrame，50 毫秒）给。
type BreathPhase() =
    let mutable phase = 0.0

    /// 每拍间隔（毫秒）
    static member FrameMs = 50.0
    /// 每拍相位增量（弧度）
    static member StepRadians = 0.12

    member _.Phase = phase

    /// 要不要开始呼吸：减弱动效或尚未挂到可视树时不开始（宿主改用静态透明度），开始时相位归零
    member _.Start(reduced: bool, attached: bool) : bool =
        if reduced || not attached then false
        else
            phase <- 0.0
            true

    /// 走一拍，返回这一拍的透明度
    member _.Step() : float =
        phase <- phase + BreathPhase.StepRadians
        0.75 + 0.2 * Math.Sin phase
