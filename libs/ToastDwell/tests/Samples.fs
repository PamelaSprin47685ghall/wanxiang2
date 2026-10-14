module Samples

let dwells = [ 9000.0, false; 6500.0, true; 4000.0, false; 4000.0, true; 9000.0, true ]
/// 每条提示上跑的事件脚本："t" 跳一次 200ms，"in" 指针进入，"out0/out1" 指针离开（按钮无/有焦点），"f" 按钮得焦，"u" 按钮失焦
let script = [ "t"; "t"; "in"; "t"; "out0"; "t"; "f"; "in"; "out1"; "t"; "u"; "t" ] @ List.replicate 60 "t"
