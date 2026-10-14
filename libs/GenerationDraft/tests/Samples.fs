module Samples

/// 事件脚本："edit"、"cat:<值>"（目录到达）、"scope:<主机>"、"discard:<值>"、"begin:<值>"（能保存才开始）、
/// "ok"/"fail"（回复最早一个在途保存）、"okstale"（回复已被切主机作废的保存）
let script =
    [ "scope:A"; "cat:1"; "edit"; "cat:2"; "cat:1"; "begin:5"; "edit"; "ok"; "begin:6"; "fail"; "begin:6"; "ok"
      "edit"; "scope:B"; "edit"; "begin:7"; "scope:A"; "begin:7"; "scope:B"; "okstale"; "scope:A"; "discard:3"
      "cat:4"; "edit"; "begin:8"; "discard:9"; "edit"; "ok"; "cat:10"; "discard:10"; "cat:11" ]
