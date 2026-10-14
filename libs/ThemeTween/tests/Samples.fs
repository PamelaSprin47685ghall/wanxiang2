module Samples

open Wanxiang.UI

let c a r g b = { A = a; R = r; G = g; B = b }
let light = [ "bg", c 255uy 250uy 250uy 250uy; "text", c 255uy 20uy 20uy 20uy; "scrim", c 128uy 0uy 0uy 0uy ]
let dark = [ "bg", c 255uy 18uy 18uy 20uy; "text", c 255uy 235uy 235uy 240uy; "scrim", c 160uy 0uy 0uy 0uy ]

/// (动作, 毫秒, 参数)：dark / light / same / reduced 切换；frame / frameReduced；complete
let script =
    [ "dark", 0.0; "frame", 17.0; "frame", 100.0; "light", 120.0; "frame", 140.0; "frame", 250.0; "frame", 320.0; "frame", 330.0
      "dark", 400.0; "frameReduced", 410.0; "frame", 420.0; "light", 500.0; "complete", 510.0; "frame", 520.0
      "same", 600.0; "dark", 700.0; "reduced", 750.0; "frame", 760.0; "light", 800.0; "dark", 1100.0; "frame", 1300.0 ]
