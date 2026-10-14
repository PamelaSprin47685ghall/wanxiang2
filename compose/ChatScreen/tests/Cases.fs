/// 金样两边共用的样本：同一份消息、同一组状态名
module Cases

open System
open Wanxiang.UI

let msg role text commit = { MessageView.empty with role = role; text = text; commitId = Some commit }

let messages =
    [ msg "user" "帮我列一个三天的京都行程" 1UL
      msg "assistant" "第一天：清水寺、二年坂。\n\n第二天：岚山竹林。" 2UL
      msg "user" "第三天呢？" 3UL
      msg "assistant" "第三天：伏见稻荷大社。" 4UL ]

let streaming = { MessageView.empty with role = "assistant"; text = "正在整理第四天" }

let parent = Guid(1, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)

/// 状态名、窗口宽
let names =
    [ "empty", 600.0
      "conversation", 600.0
      "conversation-wide", 1000.0
      "rename", 600.0
      "generating", 600.0
      "streaming", 600.0
      "crumb-clickable", 600.0
      "crumb-deleted", 600.0
      "compact", 400.0
      "readonly-title", 600.0 ]
