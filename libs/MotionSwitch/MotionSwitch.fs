namespace Wanxiang2.MotionSwitch

open System

/// 一项有语义名的时长（旧 Design/MotionPolicy.fs MotionLedger）。数值相同而语义不同的分开记。
[<RequireQualifiedAccess>]
type Timing =
    | BusySpinnerFrame
    | CopyConfirmationHold
    | SearchInputDebounce
    | SkeletonBreathFrame
    | CaretBreathFrame
    | ConversationSkeletonDelay
    | SmoothScrollFrame
    | SmoothScrollDuration
    | HostViewportPoll
    | ToastDismissTick
    | ToastDwellFailure
    | ToastDwellWarning
    | ToastDwellDefault
    | OutboxDeliveryCheckTick
    | ExportExpiryCheckTick
    | OlderPageTimeout
    | HostRequestTimeout
    | OlderPageCheckTick
    | ImportResponseTimeout
    | ProgressTextThrottle
    | ThemeColorTransition
    | ControlStateTransition
    | CaretBreathPhase
    | CopyConfirmationFade
    | ScrimFade
    | DisclosureChevronRotate
    | ControlRowFade
    | ToastFade

/// 时长初始值：照抄旧 MotionLedger。只有数据；活的可变值归主题管理者。
module MotionTimings =

    let all : Timing list =
        [ Timing.BusySpinnerFrame
          Timing.CopyConfirmationHold
          Timing.SearchInputDebounce
          Timing.SkeletonBreathFrame
          Timing.CaretBreathFrame
          Timing.ConversationSkeletonDelay
          Timing.SmoothScrollFrame
          Timing.SmoothScrollDuration
          Timing.HostViewportPoll
          Timing.ToastDismissTick
          Timing.ToastDwellFailure
          Timing.ToastDwellWarning
          Timing.ToastDwellDefault
          Timing.OutboxDeliveryCheckTick
          Timing.ExportExpiryCheckTick
          Timing.OlderPageTimeout
          Timing.HostRequestTimeout
          Timing.OlderPageCheckTick
          Timing.ImportResponseTimeout
          Timing.ProgressTextThrottle
          Timing.ThemeColorTransition
          Timing.ControlStateTransition
          Timing.CaretBreathPhase
          Timing.CopyConfirmationFade
          Timing.ScrimFade
          Timing.DisclosureChevronRotate
          Timing.ControlRowFade
          Timing.ToastFade ]

    /// 初始毫秒数
    let initialMs (timing: Timing) : float =
        match timing with
        | Timing.BusySpinnerFrame -> 40.0
        | Timing.CopyConfirmationHold -> 1500.0
        | Timing.SearchInputDebounce -> 160.0
        | Timing.SkeletonBreathFrame -> 50.0
        | Timing.CaretBreathFrame -> 50.0
        | Timing.ConversationSkeletonDelay -> 300.0
        | Timing.SmoothScrollFrame -> 16.0
        | Timing.SmoothScrollDuration -> 180.0
        | Timing.HostViewportPoll -> 500.0
        | Timing.ToastDismissTick -> 200.0
        | Timing.ToastDwellFailure -> 9000.0
        | Timing.ToastDwellWarning -> 6500.0
        | Timing.ToastDwellDefault -> 4000.0
        | Timing.OutboxDeliveryCheckTick -> 1000.0
        | Timing.ExportExpiryCheckTick -> 1000.0
        | Timing.OlderPageTimeout -> 10000.0
        | Timing.HostRequestTimeout -> 12000.0
        | Timing.OlderPageCheckTick -> 500.0
        | Timing.ImportResponseTimeout -> 30000.0
        | Timing.ProgressTextThrottle -> 100.0
        | Timing.ThemeColorTransition -> 200.0
        | Timing.ControlStateTransition -> 200.0
        | Timing.CaretBreathPhase -> 900.0
        | Timing.CopyConfirmationFade -> 150.0
        | Timing.ScrimFade -> 150.0
        | Timing.DisclosureChevronRotate -> 150.0
        | Timing.ControlRowFade -> 150.0
        | Timing.ToastFade -> 300.0

    /// 几何动画一律禁止：状态反馈只改颜色与透明度（旧 MotionLedger.geometryAnimationAllowed）
    let geometryAnimationAllowed = false

/// 减弱动效开关（旧 MotionPolicy 的 reduced / setReduced / Changed / duration）。
/// 一个实例由主题管理者拥有；值变了才通知。
type MotionSwitch() =
    let mutable reduced = false
    let changed = Event<bool>()

    member _.IsReduced = reduced

    /// 值变了才触发 Changed
    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.SetReduced(value: bool) =
        if reduced <> value then
            reduced <- value
            changed.Trigger value

    /// 经开关门控的时长：减弱动效时为零（瞬时切换）
    member _.Duration(milliseconds: float) : TimeSpan =
        if reduced then TimeSpan.Zero else TimeSpan.FromMilliseconds milliseconds
