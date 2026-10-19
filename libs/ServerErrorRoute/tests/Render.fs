module Render

open Wanxiang.UI

let messages =
    [ "attachment ABCDEF0123 not found"
      "attachment abc not found in store"
      "attachment ok"
      "attachments X not found"
      "消息记账失败：磁盘已满"
      "消息记账失败"
      "数据库锁定"
      "" ]

let all () =
    [ for active in [ true; false ] do
          for m in messages -> sprintf "%b|%s => %A" active m (ServerErrorRoute.classify active m) ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
