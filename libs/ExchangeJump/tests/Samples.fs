module Samples

/// 卡身份：(角色, 提交号)；None 为流式卡
let u n = Some("user", uint64 n)
let a n = Some("assistant", uint64 n)
let win1 = [| u 1; a 2; u 3; a 4; u 5; a 6 |]
let win2 = [| u 0; a 9; u 1; a 2; u 3; a 4; u 5; a 6; None |]
let isUser (r: string, _: uint64) = r = "user"
/// 脚本：(动作, 参数)
let script =
    [ "nav", 0; "jump", -1; "nav", 0; "jump", 1; "jump", 1; "jump", 1; "nav", 0
      "jump", -1; "jump", -1; "jump", -1; "older", 1; "jump", -1; "jump", -1; "nav", 0
      "mount2", 0; "nav", 0; "jump", -1; "jump", -1; "hidden", 1; "jump", -1; "hidden", 0
      "older", 0; "jump", -1; "nav", 0; "fail", 0; "older", 1; "jump", -1; "jump", -1; "mount1", 0; "reset", 0; "nav", 0; "jump", 1; "atbottom", 1; "nav", 0 ]
