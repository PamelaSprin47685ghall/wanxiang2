module Cases

// 工作区整树金样的状态表（旧 AppShell.Build 布局段与新 Workspace 共用）

open System
open System.Text.Json.Nodes
open Wanxiang.Protocol

let conversation = Guid "6f1d2a4e-0000-4000-8000-000000000001"

/// 审批位里放一张待批准卡
let approval =
    ToolApprovalRequested
        {| approvalId = Guid "6f1d2a4e-0000-4000-8000-0000000000a1"; conversationId = conversation
           generationId = Guid "6f1d2a4e-0000-4000-8000-0000000000b1"; callId = "call-1"
           toolName = "builtin_file_read"; summary = "读取文件 /home/a/notes.txt"
           arguments = JsonNode.Parse """{"path":"/home/a/notes.txt"}"""; policy = "always"; expiresInSeconds = 60 |}

/// (状态, 视口宽)
let names =
    [ "wide", 1280.0
      "collapsed", 1280.0
      "starting", 1280.0
      "approval", 1280.0
      "compact", 600.0
      "drawer", 600.0
      "compact-approval", 600.0 ]
