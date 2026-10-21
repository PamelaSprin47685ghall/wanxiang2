namespace Wanxiang.UI

/// 一次刷新的结果：剩余秒数、是否走完、是否该播报、计时器是否继续
type CountdownFrame =
    {
        Left: int
        /// 走完（left ≤ 0）：显示「现在可以重试了」、改用成功色
        Ready: bool
        /// 只在第一次走完时为 true：宿主此刻把读屏播报设为 polite
        Announce: bool
        /// 还有剩余：计时器继续每秒刷新
        KeepTicking: bool
    }

/// 旧 Views/MessageCard.fs 限流倒计时（1109-1150）的刷新记账：剩余秒数由宿主按截止时刻算好传入，
/// 本对象纯状态记住「已播报过」并告诉宿主要不要继续走表。无独立定时器，文字与颜色由错误面板呈现。
type CountdownTicker() =
    let mutable announced = false
    let mutable last: int option = None

    /// 刷新间隔（毫秒）
    static member IntervalMs = 1000.0

    /// 最近一次刷新的剩余秒数
    member _.Left = last

    /// 刷新一次（构造时、计时器每拍、挂到可视树时都调）
    member _.Refresh(left: int) : CountdownFrame =
        last <- Some left
        let ready = left <= 0
        let announce = ready && not announced

        if announce then
            announced <- true

        {
            Left = left
            Ready = ready
            Announce = announce
            KeepTicking = not ready
        }
