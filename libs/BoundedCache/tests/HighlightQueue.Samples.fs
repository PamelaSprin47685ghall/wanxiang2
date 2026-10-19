module HighlightQueueSamples
/// (动作, 键)：claim / ok / fail（完成）/ fired（通知到点）
let script =
    [ "claim", "a"; "claim", "a"; "claim", "b"; "ok", "a"; "claim", "a"; "ok", "b"; "fail", "a"; "fired", ""
      "claim", "c"; "fail", "c"; "claim", "c"; "ok", "c"; "ok", "zz"; "fired", ""; "fired", ""; "ok", "c" ]
