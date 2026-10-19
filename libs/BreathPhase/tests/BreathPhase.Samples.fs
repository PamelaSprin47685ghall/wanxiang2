module BreathPhaseSamples
/// (动作, reduced, attached)：start 或 step
let script =
    [ yield "start", true, true; yield "start", false, false; yield "start", false, true
      for _ in 1 .. 60 do yield "step", false, false
      yield "start", false, true; yield "step", false, false; yield "step", false, false ]
