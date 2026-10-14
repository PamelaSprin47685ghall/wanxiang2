namespace Wanxiang.UI

// 照抄旧 Views/MessageCard.fs 的 caretBreathPhase / caretBreathWave / caretBreathOpacity；
// MotionLedger.caretBreathPhase 与 Tokens.opacityCaretBreath* 改为参数
open System

module CaretBreath =
    let phase (halfMs: float) (elapsedMs: float) =
        let elapsed = TimeSpan.FromMilliseconds elapsedMs
        Math.PI / 2.0
        + 2.0 * Math.PI * elapsed.TotalMilliseconds
          / (2.0 * (TimeSpan.FromMilliseconds halfMs).TotalMilliseconds)

    let wave (halfMs: float) (elapsedMs: float) =
        (1.0 + Math.Sin(phase halfMs elapsedMs)) / 2.0

    let opacity (lo: float) (hi: float) (halfMs: float) (elapsedMs: float) =
        let raw = lo + (hi - lo) * wave halfMs elapsedMs
        min hi (max lo raw)
