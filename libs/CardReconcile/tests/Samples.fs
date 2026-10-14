module Samples

/// 每帧想要的身份序列；None = 无身份（错误卡、流式卡），"clear" 帧表示 Clear
let frames: string option list list =
    [ [ Some "a"; Some "b"; Some "c" ]
      [ Some "a"; Some "b"; Some "c"; Some "d" ]
      [ Some "a"; Some "b"; Some "c"; Some "d"; None ]
      [ Some "a"; Some "b"; Some "c"; Some "d"; None ]
      [ Some "a"; Some "x"; Some "c"; Some "d" ]
      [ Some "c"; Some "a" ]
      [ None; Some "c" ]
      [ Some "clear" ]
      [ Some "a" ]
      [] ]
