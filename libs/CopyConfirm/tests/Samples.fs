module Samples

/// (动作, 毫秒)：copy / copyReduced / tick / reset / detach
let script =
    [ "tick", 0.0; "copy", 0.0; "tick", 100.0; "tick", 150.0; "tick", 1000.0; "tick", 1499.0; "tick", 1500.0; "tick", 1600.0
      "copy", 2000.0; "tick", 2100.0; "copy", 2120.0; "tick", 2200.0; "tick", 2269.0; "tick", 2270.0; "tick", 3500.0; "tick", 3620.0
      "copyReduced", 4000.0; "tick", 4001.0; "tick", 5500.0
      "copy", 6000.0; "reset", 6050.0; "tick", 6060.0; "copy", 7000.0; "detach", 8000.0; "tick", 8000.0
      "copy", 9000.0; "tick", 11000.0; "copyReduced", 12000.0; "copy", 12010.0; "tick", 12100.0 ]
