module ScrollFollowSamples
/// (动作, 参数 a, 参数 b, 参数 c)
let script =
    [ "render", 0, 5, 0; "scroll", 1, 0, 0; "render", 0, 6, 0; "render", 0, 8, 0; "render", 1, 8, 0; "render", 1, 8, 0
      "render", 0, 9, 0; "render", 0, 30, 1; "button", 1, 0, 0; "button", 1, 1, 0; "return", 0, 0, 0; "scroll", 0, 0, 0
      "render", 0, 30, 0; "scroll", 0, 1, 0; "scroll", 1, 0, 0; "render", 1, 31, 0; "render", 0, 31, 0; "wheel", 0, 0, 0
      "pin", 0, 0, 0; "render", 0, 32, 0; "jump", 0, 0, 0; "jump", 1, 0, 0; "begin", 0, 0, 0; "render", 0, 33, 0
      "arrive", 0, 0, 0; "button", 0, 0, 0; "pin", 0, 0, 0; "reset", 0, 0, 0; "render", 0, 3, 0; "scroll", 1, 0, 0; "render", 0, 4, 0; "button", 0, 1, 0 ]
