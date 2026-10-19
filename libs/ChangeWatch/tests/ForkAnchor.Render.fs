module ForkAnchorRender
open Wanxiang.UI

// 新旧共用：固定输入跑 ForkAnchor，逐行输出。
let all () =
    let sb = System.Text.StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let lists = [ []; [ 5UL ]; [ 1UL; 2UL; 3UL ]; [ 7UL; 3UL; 9UL; 1UL ]; [ 2UL; 2UL; 4UL ] ]
    let targets = [ None; Some 0UL; Some 1UL; Some 3UL; Some 4UL; Some 100UL ]
    for ids in lists do
        for t in targets do
            line (sprintf "forkAfter %A %A = %A" ids t (ForkAnchor.forkAfter ids t))
    for text in [ ""; "hi"; "多行\n文本"; "quote \" and <tag> & emoji 😀"; "  " ] do
        line ("edited " + (ForkAnchor.editedMessage text).ToJsonString())
    line ("noConversation " + ForkAnchor.noConversationText)
    sb.ToString()
