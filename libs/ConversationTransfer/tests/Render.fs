/// 金标准渲染：同一份源码分别对旧程序集与新库编译。导出时间固定、用 UTC 偏移，Markdown 头部的本地时间由 TZ=UTC 固定。
module Render

open System
open System.Text
open System.Text.Json.Nodes
open Wanxiang.UI

let private g (n: int) = Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)
let private at = DateTimeOffset(2026, 10, 12, 4, 5, 6, TimeSpan.Zero)

let lineages : ExportLineage list =
    [ ConversationTransfer.rootLineage
      { parentId = Some(g 1); parentTitle = Some "原 \"对话\""; parentDeleted = false; forkOrdinal = Some 3; forkBaseMessageId = Some 4UL
        forks = [ g 2, "子<分叉>", Some 9UL; g 3, "无点", None ] }
      { parentId = Some(g 1); parentTitle = None; parentDeleted = true; forkOrdinal = None; forkBaseMessageId = None; forks = [] }
      { parentId = Some(g 1); parentTitle = None; parentDeleted = false; forkOrdinal = None; forkBaseMessageId = None; forks = [] } ]

let private raw () : JsonObject list =
    [ JsonNode.Parse("""{"commitId":5,"committedAt":"2026-10-12T04:00:00Z","payload":{"role":"user","contents":[{"type":"text","text":"你好 & <b>"}]}}""").AsObject()
      JsonNode.Parse("""{"commitId":"6","payload":{"role":"assistant","API_KEY":"x","contents":[{"type":"text","text":"hi","authorization":"Bearer y","nested":[{"password":1,"keep":[null,true]}]},{"type":"attachment","sha256":"aa"}]}}""").AsObject()
      JsonNode.Parse("""{"payload":{"role":"tool","contents":[]}}""").AsObject() ]

let imports =
    [ """{"format":"wanxiang.conversation","formatVersion":1,"exportedAt":"2026-10-12T04:05:06Z","conversation":{"id":"0192b4a0-0000-7000-8000-000000000001","title":"  "},"lineage":{"kind":"fork","parentTitle":"P"},"messages":[{"payload":{"role":"user","contents":[]}},{"payload":{"role":"robot"}},5,{"payload":{"role":"assistant","secret":"s","contents":[{"type":"attachment","sha256":"x"}]}}]}"""
      """{"format":"wanxiang.conversation","formatVersion":1,"conversation":{"title":"T"},"messages":[{"payload":{"role":"system"}}]}"""
      """{"format":"wanxiang.conversation","formatVersion":2,"messages":[]}"""
      """{"format":"other"}"""
      """[1]"""
      """not json""" ]

// 旧代码直接读 Wire 的上限；新库由调用方传入同一个值 5000。
#if INTERACTIVE
let private parseImport = ConversationTransfer.parseImport
#else
let private parseImport = ConversationTransfer.parseImportWithin 5000
#endif

let all () =
    let sb = StringBuilder()
    for l in lineages do
        sb.AppendLine(ConversationTransfer.toJson (g 7) "标题 \"x\"" 42UL at l (raw ())) |> ignore
        sb.AppendLine(String.Join("|", ConversationTransfer.lineageLines l)) |> ignore
        sb.AppendLine(ConversationTransfer.withPreamble "# T\n\nbody" (ConversationTransfer.markdownPreamble l 3 at)) |> ignore
        sb.AppendLine(ConversationTransfer.withPreamble "# 无空行" "PRE") |> ignore
    let exported = ConversationTransfer.toJson (g 7) "旅行" 42UL at lineages[1] (raw ())
    for text in exported :: imports do
        match parseImport (int64 text.Length) text with
        | Ok p ->
            sb.AppendLine(sprintf "OK %s %A %A %A skipped=%d att=%b" p.title p.originalConversationId (p.exportedAt |> Option.map (fun t -> t.ToString("o", Globalization.CultureInfo.InvariantCulture))) p.lineageNote p.skipped p.hasAttachments) |> ignore
            for m in p.messages do sb.AppendLine("  " + m.ToJsonString()) |> ignore
        | Error e -> sb.AppendLine("ERR " + e) |> ignore
    sb.AppendLine(sprintf "%A" (parseImport (ConversationTransfer.maxImportBytes + 1L) "")) |> ignore
    for t in [ "a.md"; "a"; "b.MD" ] do sb.AppendLine(ConversationTransfer.safeJsonFileName "x" t) |> ignore
    sb.AppendLine(ConversationTransfer.importedTitle "旅行") |> ignore
    for k in [ "apiKey"; "Authorization"; "token"; "Bearer" ] do sb.AppendLine(sprintf "%s %b" k (ConversationTransfer.isSecretKey k)) |> ignore
    sb.ToString()
