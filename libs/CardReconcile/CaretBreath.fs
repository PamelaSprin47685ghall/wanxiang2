namespace Wanxiang.UI

open System

/// 流式光标的呼吸数学。时钟（运行原点、单调时钟）仍归 MessageCard，这里只是纯函数。
/// halfMs 即旧 MotionLedger.caretBreathPhase（900 毫秒）；一周期 = 2 × halfMs，t=0 落在峰值。
module CaretBreath =

    /// 相位（弧度）：π/2 + 2π · t / (2 · halfMs)
    let phase (halfMs: float) (elapsedMs: float) =
        Math.PI / 2.0 + 2.0 * Math.PI * elapsedMs / (2.0 * halfMs)

    /// 呼吸波：0..1，1 = 峰值
    let wave (halfMs: float) (elapsedMs: float) =
        (1.0 + Math.Sin(phase halfMs elapsedMs)) / 2.0

    /// 不透明度：把波映射进 [lo, hi]，末端裁剪保证不越界（旧 Tokens.opacityCaretBreathMin/Max = 0.55/1.0）
    let opacity (lo: float) (hi: float) (halfMs: float) (elapsedMs: float) =
        let raw = lo + (hi - lo) * wave halfMs elapsedMs
        min hi (max lo raw)
