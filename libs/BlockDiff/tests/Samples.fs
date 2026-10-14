module Samples

/// 流式原文序列：空行分块；"!same" 表示把上一条原样再给一次
let raws =
    [ "Hello"; "Hello wor"; "Hello world\n\n"; "!same"; "Hello world\n\n```\ncode"; "Hello world\n\n```\ncode\n```\n\nTail"
      "Hello world\n\n```\ncode\n```\n\nTail"; "Hello world\n\nchanged"; ""; "Again" ]
