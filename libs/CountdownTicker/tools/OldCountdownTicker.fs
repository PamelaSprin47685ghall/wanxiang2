namespace Wanxiang.UI

type CountdownFrame =
    { Left: int
      Ready: bool
      Announce: bool
      KeepTicking: bool }

// 照抄旧 Views/MessageCard.fs refresh ()：控件属性换成记录字段。
// 旧 Announce 是「SetLiveSetting 这一刻发生了」，KeepTicking 是 `refresh () > 0`。
type CountdownTicker() =
    let mutable announcedReady = false
    let mutable countdownLeft = 0
    let mutable touched = false
    static member IntervalMs = 1000.0
    member _.Left = if touched then Some countdownLeft else None
    member _.Refresh(left: int) : CountdownFrame =
        let mutable announce = false
        if left <= 0 && not announcedReady then
            announcedReady <- true
            announce <- true
        let visible = left <= 0
        countdownLeft <- left
        touched <- true
        { Left = left; Ready = visible; Announce = announce; KeepTicking = left > 0 }
