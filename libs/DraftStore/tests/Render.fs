/// 金标准渲染：同一份源码分别对旧程序集与新库编译。保存序列经 `save` 回调取得「对落点做了什么」。
module Render

open System
open System.Text
open Wanxiang.UI

let private g (n: int) = Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)
let private at = DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours 8.0)
let private r name : DraftAttachmentRef = { fileName = name; mediaType = "text/plain"; size = 3L; sha256 = name + "-sha" }

/// 保存序列：空、写、重复、改、清空、再清空、再写。
let snapshots : DraftSnapshot list =
    let d1 = { instanceId = "h1"; conversationId = Some(g 1); text = "一"; attachments = [ r "a" ] }
    let u1 = { instanceId = "h1"; conversationId = g 2; invocationId = g 3; text = "未发"; attachments = []; savedAt = at }
    [ DraftSnapshot.empty
      { drafts = [ d1 ]; unsent = [] }
      { drafts = [ d1 ]; unsent = [] }
      { drafts = [ d1 ]; unsent = [ u1 ] }
      DraftSnapshot.empty
      DraftSnapshot.empty
      { drafts = []; unsent = [ u1 ] } ]

let all (inputs: string list) (saves: DraftSnapshot list -> string list) =
    // 金样在 invariant 文化下生成（savedAt 为 24 小时制）；%A 对 DateTimeOffset 走当前线程文化，
    // 先钉死再渲染，测试在任何 locale 下字节可比。
    System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo.InvariantCulture
    let sb = StringBuilder()
    for text in inputs do
        let s = DraftCodec.tryParse text
        sb.AppendLine("<< " + text).AppendLine(sprintf "%A" s).AppendLine(DraftCodec.serialize s) |> ignore
    for op in saves snapshots do sb.AppendLine("save: " + op) |> ignore
    let stored : DraftSnapshot =
        { drafts =
            [ { instanceId = "h1"; conversationId = None; text = "旧1"; attachments = [] }
              { instanceId = "h2"; conversationId = None; text = "旧2"; attachments = [] } ]
          unsent = [ { instanceId = "h2"; conversationId = g 2; invocationId = g 3; text = "u"; attachments = []; savedAt = at } ] }
    let current : DraftSnapshot = { drafts = [ { instanceId = "h1"; conversationId = Some(g 9); text = "新"; attachments = [] } ]; unsent = [] }
    sb.AppendLine(sprintf "%A" (DraftPolicy.mergeForSave stored (set [ "h1" ]) current)) |> ignore
    sb.AppendLine(sprintf "%A" (DraftPolicy.deletedConversations (set [ g 1 ]) (set [ g 1; g 2; g 3 ]) (set [ g 3 ]))) |> ignore
    sb.ToString()
