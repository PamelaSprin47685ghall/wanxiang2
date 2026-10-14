// 照抄旧 Design/MotionPolicy.fs MotionLedger（extract-ledger@0c0b2a6）；controlStateDuration 依赖旧 MotionPolicy，去掉。
module OldMotion
open System
module MotionLedger =

    let busySpinnerFrame = TimeSpan.FromMilliseconds 40.0
    let copyConfirmationHold = TimeSpan.FromMilliseconds 1500.0
    let geometryAnimationAllowed = false

    /// 搜索输入防抖：停止输入 160ms 后才触发过滤，避免逐键重建列表。
    let searchInputDebounce = TimeSpan.FromMilliseconds 160.0
    /// 骨架屏呼吸帧：50ms 一级，在 0.55~0.95 之间平滑起伏。
    let skeletonBreathFrame = TimeSpan.FromMilliseconds 50.0
    /// 流式光标呼吸帧：50ms 一级，驱动 opacity 在呼吸区间内起伏。
    /// 与 skeletonBreathFrame 数值相同但语义不同（光标不是骨架加载态），分记。
    let caretBreathFrame = TimeSpan.FromMilliseconds 50.0
    /// 对话切换骨架延迟：对话快照 300ms 内到达就不挂任何 loading chrome。
    let conversationSkeletonDelay = TimeSpan.FromMilliseconds 300.0
    /// 平滑滚动帧节奏：约 60fps 的 16ms tick。
    let smoothScrollFrame = TimeSpan.FromMilliseconds 16.0
    /// 平滑滚动总时长：16ms 帧驱动下的 180ms ease-out cubic 动画。
    let smoothScrollDuration = TimeSpan.FromMilliseconds 180.0
    /// 浏览器宿主 CSS 视口轮询周期：Browser 后端高 DPI 下取真实 CSS 像素宽。
    let hostViewportPoll = TimeSpan.FromMilliseconds 500.0
    /// toast 自动消失倒计时 tick：按剩余毫秒递减的 200ms 步进。
    let toastDismissTick = TimeSpan.FromMilliseconds 200.0

    /// toast 停留时长（自动消失倒计时前的停留）：OverlayHost 按语气分三档，
    /// 失败留最久让人读完、warning 次之、常规最短；三档语义名收拢在此，
    /// 递减步进仍是上面的 toastDismissTick。
    let toastDwellFailure = TimeSpan.FromMilliseconds 9000.0
    let toastDwellWarning = TimeSpan.FromMilliseconds 6500.0
    let toastDwellDefault = TimeSpan.FromMilliseconds 4000.0
    /// outbox 投递过期检查 tick：配合投递超时逐格清扫未 ACK 投递。
    let outboxDeliveryCheckTick = TimeSpan.FromMilliseconds 1000.0
    /// 导出过期检查 tick：配合导出有效期逐格清扫；与 outbox 同为 1000ms 但语义不同，分记。
    let exportExpiryCheckTick = TimeSpan.FromMilliseconds 1000.0
    /// 决策 310：更早一页历史的等待上限。过了就判失败、给行内重试，绝不无限挂起。
    let olderPageTimeout = TimeSpan.FromMilliseconds 10000.0
    /// 决策 443：配置写入/命令等主机确认的上限。旧主机会静默丢弃不认识的请求。
    let hostRequestTimeout = TimeSpan.FromMilliseconds 12000.0
    /// 更早一页历史的超时检查 tick。
    let olderPageCheckTick = TimeSpan.FromMilliseconds 500.0
    /// 决策 313：导入命令的等待上限（旧主机不认识命令时会静默忽略）。
    let importResponseTimeout = TimeSpan.FromMilliseconds 30000.0
    /// 进度文字节流时长（预留）：传输事件可远快于人眼阅读，100ms 约每秒 10 次，既顺滑又可读。
    let progressTextThrottle = TimeSpan.FromMilliseconds 100.0
    /// 主题颜色过渡时长（预留）：200ms 约 60fps 下 12 帧，落在常见 150~300ms 状态色过渡区间，可感知而不拖沓。
    let themeColorTransition = TimeSpan.FromMilliseconds 200.0

    /// 控件状态反馈补间时长：hover/press/focus/selected 的底色过渡。
    /// 由 120ms 提到 200ms，与 themeColorTransition 同档形成统一的状态过渡区间：
    /// 短了瞬切失去缓冲，长了按钮发黏。只过渡颜色，不引入任何几何/位移/尺寸动画。
    let controlStateTransition = TimeSpan.FromMilliseconds 200.0

    /// 控件状态补间的实际时长：尊重 MotionPolicy 的减弱动效偏好，降级为零（瞬时切换）。

    /// 流式光标呼吸单程时长（下限→上限→下限约 1.8s 一周期）。
    /// 呼吸只改 opacity，不改尺寸，几何契约不变。
    let caretBreathPhase = TimeSpan.FromMilliseconds 900.0

    /// 复制确认反馈的淡入淡出时长：copy → ✓ → 复原之间的过渡只改 opacity。
    let copyConfirmationFade = TimeSpan.FromMilliseconds 150.0

    /// scrim（compact 抽屉遮罩与对话框遮罩共用一层）的不透明度淡入淡出时长：
    /// 只改 opacity、不碰任何几何/位移动画；150ms 与复制确认同一手感区间。
    /// 生效时长由消费点经 MotionPolicy 降级（减弱动效归零，瞬时切换）。
    let scrimFade = TimeSpan.FromMilliseconds 150.0

    /// 披露箭头（chevron）旋转过渡：150ms，只改 RotateTransform.Angle，
    /// 不碰布局几何（MessageCard 三处披露箭头）。
    /// 与 controlRowFade 同为 150ms 但语义不同（transform vs opacity），分记。
    let disclosureChevronRotate = TimeSpan.FromMilliseconds 150.0

    /// 控件行/条级反馈淡入淡出：150ms，只补间 Opacity
    ///（MessageCard 操作条、Composer 附件 chip），与复制确认同一手感区间。
    let controlRowFade = TimeSpan.FromMilliseconds 150.0

    /// toast 进出场淡入淡出时长：约 300ms（200~300ms 状态过渡区间的舒缓档），
    /// 只补间 Opacity，不做任何位移/尺寸动画；经 MotionPolicy 门控，减弱动效归零即时切换。
    let toastFade = TimeSpan.FromMilliseconds 300.0

let all : (string * float) list =
    [ "BusySpinnerFrame", MotionLedger.busySpinnerFrame.TotalMilliseconds
      "CopyConfirmationHold", MotionLedger.copyConfirmationHold.TotalMilliseconds
      "SearchInputDebounce", MotionLedger.searchInputDebounce.TotalMilliseconds
      "SkeletonBreathFrame", MotionLedger.skeletonBreathFrame.TotalMilliseconds
      "CaretBreathFrame", MotionLedger.caretBreathFrame.TotalMilliseconds
      "ConversationSkeletonDelay", MotionLedger.conversationSkeletonDelay.TotalMilliseconds
      "SmoothScrollFrame", MotionLedger.smoothScrollFrame.TotalMilliseconds
      "SmoothScrollDuration", MotionLedger.smoothScrollDuration.TotalMilliseconds
      "HostViewportPoll", MotionLedger.hostViewportPoll.TotalMilliseconds
      "ToastDismissTick", MotionLedger.toastDismissTick.TotalMilliseconds
      "ToastDwellFailure", MotionLedger.toastDwellFailure.TotalMilliseconds
      "ToastDwellWarning", MotionLedger.toastDwellWarning.TotalMilliseconds
      "ToastDwellDefault", MotionLedger.toastDwellDefault.TotalMilliseconds
      "OutboxDeliveryCheckTick", MotionLedger.outboxDeliveryCheckTick.TotalMilliseconds
      "ExportExpiryCheckTick", MotionLedger.exportExpiryCheckTick.TotalMilliseconds
      "OlderPageTimeout", MotionLedger.olderPageTimeout.TotalMilliseconds
      "HostRequestTimeout", MotionLedger.hostRequestTimeout.TotalMilliseconds
      "OlderPageCheckTick", MotionLedger.olderPageCheckTick.TotalMilliseconds
      "ImportResponseTimeout", MotionLedger.importResponseTimeout.TotalMilliseconds
      "ProgressTextThrottle", MotionLedger.progressTextThrottle.TotalMilliseconds
      "ThemeColorTransition", MotionLedger.themeColorTransition.TotalMilliseconds
      "ControlStateTransition", MotionLedger.controlStateTransition.TotalMilliseconds
      "CaretBreathPhase", MotionLedger.caretBreathPhase.TotalMilliseconds
      "CopyConfirmationFade", MotionLedger.copyConfirmationFade.TotalMilliseconds
      "ScrimFade", MotionLedger.scrimFade.TotalMilliseconds
      "DisclosureChevronRotate", MotionLedger.disclosureChevronRotate.TotalMilliseconds
      "ControlRowFade", MotionLedger.controlRowFade.TotalMilliseconds
      "ToastFade", MotionLedger.toastFade.TotalMilliseconds ]
