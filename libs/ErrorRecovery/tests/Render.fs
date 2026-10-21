module Render

open Wanxiang.UI

let all () =
    [
        OpenProviderSettings
        OpenConversationSettings
        SwitchModel
        StartNewConversation
    ]
    |> List.map (fun r -> sprintf "%A\t%s\n" r (ErrorRecovery.label r))
    |> String.concat ""
