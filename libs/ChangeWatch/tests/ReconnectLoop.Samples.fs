module ReconnectLoopSamples

/// 与 ReconnectBackoff 同规则（本库不引用它）
let next (d: int) = min ((max 1000 d) * 2) 30000

let script =
    [
        "connect", 0
        "sched", 0
        "sched", 1
        "sched", 1
        "fire", 1
        "fire", 1
        "connect", 0
        "current", 1
        "current", 2
        "sched", 1
        "connect", 0
        "fire", 2
        "sched", 1
        "resume", 0
        "resume", 0
        "connect", 0
        "sched", 1
        "sched", 1
        "fire", 4
        "connect", 0
        "sched", 1
        "fire", 5
        "connect", 0
        "sched", 1
        "fire", 6
        "connect", 0
        "sched", 1
        "fire", 7
        "connect", 0
        "sched", 1
        "fire", 8
        "connect", 0
        "sched", 1
        "reset", 0
        "fire", 9
        "connect", 0
        "sched", 1
    ]
