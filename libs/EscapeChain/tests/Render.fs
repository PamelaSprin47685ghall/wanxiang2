/// 金标准渲染：同一份源码分别对旧代码摘录与新库编译。
module Render

open Wanxiang.UI

let all () =
    let bools = [ false; true ]
    let order = [ "0192b4a0-0000-7000-8000-000000000001"; "not-a-guid"; "0192b4a0-0000-7000-8000-000000000003" ]
    [ for a in bools do
          for b in bools do
              for c in bools do
                  for d in bools -> sprintf "esc %b %b %b %b => %A" a b c d (EscapeChain.decide a b c d)
      for a in bools do
          for b in bools -> sprintf "blocked %b %b => %b" a b (EscapeChain.blockedByOverlay a b)
      for i in [ -1; 0; 1; 2; 3; 9 ] -> sprintf "at %d => %A" i (EscapeChain.conversationAt i order) ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
