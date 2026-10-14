/// 金标准渲染：同一份源码分别对旧程序集与新库编译。
module Render

open Wanxiang.UI

let all () =
    [ OpenProviderSettings; OpenConversationSettings; SwitchModel; StartNewConversation ]
    |> List.map (fun r -> sprintf "%A\t%s\n" r (ErrorRecovery.label r))
    |> String.concat ""
