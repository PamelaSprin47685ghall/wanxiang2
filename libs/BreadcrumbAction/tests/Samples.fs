module Samples

let crumbs : (string * string * bool * int) option list =
    [ None; Some("来自「旅行计划」", "回到父对话「旅行计划」", true, 7); Some("来自已删除的对话", "父对话已删除", false, 8); None; Some("来自「A」", "回到 A", true, 9) ]
