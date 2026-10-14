/// 金标准渲染：同一份源码分别对旧代码摘录与新库编译。
module Render

open Wanxiang.UI

let all () =
    let bools = [ false; true ]
    [ for prev, next in [ "", "a"; "a", "a"; "a", "b" ] do
          for exportOpen in bools do
              for browser in bools do
                  for token in bools do
                      for active in bools ->
                          sprintf "%s->%s export=%b browser=%b token=%b active=%b => %A" prev next exportOpen browser token active
                              (InstanceSwitch.plan prev next exportOpen browser token active) ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
