/// 金标准渲染：同一份源码分别对旧程序集与新库编译，输出逐字节比较。
module Render

open System
open System.Text
open System.Text.Json.Nodes
open Wanxiang.UI

let private view (m: MessageView) =
    let sb = StringBuilder()
    sb.AppendFormat("role={0}|text={1}|reasoning={2}|commit={3}|at={4}\n", m.role, m.text, m.reasoning, string m.commitId, (m.committedAt |> Option.map (fun t -> t.ToString "O") |> Option.defaultValue "-")) |> ignore
    for c in m.toolCalls do sb.AppendFormat("  call {0} {1} {2} {3}\n", c.callId, c.name, c.argumentsJson, string c.result) |> ignore
    for (id, r) in m.toolResults do sb.AppendFormat("  result {0} {1}\n", id, r) |> ignore
    for a in m.attachments do sb.AppendFormat("  att {0} {1} {2} {3} image={4} {5}\n", a.sha256, a.size, a.mediaType, a.fileName, AttachmentRef.isImage a, AttachmentRef.formatSize a.size) |> ignore
    sb.AppendFormat("  visible={0} user={1} tool={2}\n", MessageView.hasVisibleBody m, MessageView.isUser m, MessageView.isTool m) |> ignore
    sb.ToString()

let all (lines: string list) =
    let now = DateTimeOffset(2026, 10, 12, 12, 0, 30, TimeSpan.Zero)
    let views = lines |> List.map (fun l -> MessageView.ofSnapshotItem (JsonNode.Parse l))
    let sb = StringBuilder()
    for v in views do sb.Append(view v) |> ignore
    sb.Append("== merged\n") |> ignore
    for v in MessageView.mergeToolResults views do sb.Append(view v) |> ignore
    for n in 0 .. views.Length do
        let prefix = List.take n views
        sb.AppendFormat("tail {0}: chain={1} latest={2} losable={3} unanswered={4}\n", n, (ReplyTail.chain prefix).Length, string (ReplyTail.latestText prefix), ReplyTail.hasLosableContent prefix, ReplyTail.isUnanswered now prefix) |> ignore
    for s in [ 0L; 1023L; 1024L; 1536L; 1048575L; 1048576L; 5000000000L ] do
        sb.AppendFormat("size {0} {1}\n", s, AttachmentRef.formatSize s) |> ignore
    sb.ToString()
