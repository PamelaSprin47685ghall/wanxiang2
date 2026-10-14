module Samples

/// 按键脚本：(动作, 输入框文本, 光标, 是否有选区)
type Op =
    | Record of string
    | Up of string * int * bool
    | Down of string * int * bool
    | Escape
    | Reset
    | Draft of string

let script =
    [ Up("", 0, false); Down("", 0, false); Escape
      Record "first"; Record "second"; Record "second"; Record "  "; Record "third\nline"
      Up("half", 4, false); Up("half", 0, false); Draft "x"; Up("third\nline", 10, false); Up("second", 3, false); Up("first", 0, false); Up("first", 0, false)
      Down("first", 0, false); Down("first", 5, false); Down("second", 6, true); Down("second", 6, false); Down("third\nline", 10, false); Draft "half"
      Up("a\nb", 0, false); Escape; Up("a\nb", 2, false); Up("a\nb", 1, false); Escape; Draft "a\nb"
      Up("x", 0, true); Up("x", 1, false); Up("x", 0, false); Reset; Draft "now"; Escape
      Up(null, 5, false); Down(null, 0, false); Down("third\nline", 10, false) ]
    @ [ for i in 1 .. 105 -> Record(sprintf "m%d" i) ]
    @ [ for _ in 1 .. 101 -> Up("", 0, false) ]
