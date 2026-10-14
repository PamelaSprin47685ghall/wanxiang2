module Samples

/// (动作, 毫秒)：read = 读取中分页到达；force = 急件；idle = 非高频态；tail = 尾巴计时器响
let script =
    [ "read", 0.0; "read", 10.0; "read", 30.0; "tail", 100.0; "read", 150.0; "read", 220.0; "read", 260.0; "tail", 300.0
      "tail", 400.0; "read", 410.0; "force", 420.0; "tail", 510.0; "read", 515.0; "idle", 516.0; "read", 600.0 ]
