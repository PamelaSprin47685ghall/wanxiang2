module Samples

/// n 条已提交消息，提交号从 start 起；streaming 时尾部加一张流式卡
let msgs (start: int) (n: int) (streaming: bool) =
    [ for i in start .. start + n - 1 -> Some(uint64 i) ] @ (if streaming then [ None ] else [])
let script =
    [ "plan", 100, 30, false; "plan", 100, 90, false; "plan", 100, 90, true; "expand", 0, 0, false; "plan", 100, 90, false
      "expand", 0, 0, false; "plan", 100, 90, false; "expand", 0, 0, false; "reset", 0, 0, false; "plan", 100, 150, false
      "ensure", 105, 0, false; "plan", 100, 150, false; "ensure", 105, 0, false; "ensure", 999, 0, false
      "reset", 0, 0, false; "plan", 100, 150, false; "planp", 50, 200, false; "planp", 50, 200, false; "planp", 0, 250, false
      "plan", 0, 250, true ]
