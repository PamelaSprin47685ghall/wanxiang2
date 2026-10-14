module Samples

/// (温度, topP, 最大 token, 上下文条数, 工具轮数, 指令, 自动标题)
let forms : (string * string * string * string * string * string * bool) list =
    [ "", "", "", "200", "12", "", true
      null, null, null, null, null, null, false
      " 0.7 ", "0.95", "4096", "0", "1", "  你是助手  ", false
      "2", "1", "1", "50", "3", "x", true
      "2.01", "-0.1", "0", "-1", "0", "", true
      "abc", "1e-1", "1.5", "1.5", "abc", "", true
      "1,5", "0.5", " 12 ", " 7 ", " 8 ", "\n", true
      "NaN", "Infinity", "2147483648", "2147483647", "2147483647", "多行\n指令", false ]
let floats = [ None; Some 0.0; Some 0.7; Some 0.125; Some 1.005; Some 2.0; Some 1234.5678 ]
