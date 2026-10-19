/// 金标准渲染：同一份源码分别对旧代码摘录与新库编译。
module ConfigFeedbackRender
open Wanxiang.UI

let all () =
    [ for tracked in [ None; Some ""; Some "已切换模型" ] do
          for ok in [ true; false ] do
              for errors in [ []; [ "密钥为空" ]; [ "a"; "b" ] ] ->
                  sprintf "%A %b %A => %A" tracked ok errors (ConfigApplied.notice tracked ok errors) ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
