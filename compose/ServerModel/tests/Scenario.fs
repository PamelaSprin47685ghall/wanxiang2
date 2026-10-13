module Scenario

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server

let private msg (role: string) (contents: string) : JsonNode =
    JsonNode.Parse(sprintf """{"role":"%s","contents":%s}""" role contents)

let private norm (s: string) =
    let s = Regex.Replace(s, "\"\\d{4}-\\d\\d-\\d\\dT[^\"]*\"", "\"<t>\"")
    s

/// 真实组合：EventStore 写入会话与消息，ServerModel 投影成线上视图。
let all () : string =
    let dir = Path.Combine(Path.GetTempPath(), "wx-sm-golden-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        use store = new Wanxiang.Store.EventStore(dir)
        let a = Guid "aaaaaaaa-0000-0000-0000-000000000001"
        let b = Guid "bbbbbbbb-0000-0000-0000-000000000002"
        let cfg : SessionConfig =
            { provider = "openai"; model = "m"; instructions = None; tools = []; temperature = None
              topP = None; maxTokens = None; thinkingBudget = None; extraJson = None }
        let att = """[{"$type":"text","text":"看图"},{"$type":"data","additionalProperties":{"wanxiang":{"type":"attachment","sha256":"abc123","mediaType":"image/png","fileName":"a.png","size":42}}}]"""
        let rec_ c p = AgentMessageRecorded { conversationId = c; payloadJson = p }
        store.SubmitEvents [ ConversationCreated { conversationId = a; title = "甲"; config = cfg } ] |> ignore
        store.SubmitEvents [ rec_ a (msg "user" att) ] |> ignore
        store.SubmitEvents [ rec_ a (msg "assistant" """[{"$type":"text","text":"一只猫。\n第二行"}]""") ] |> ignore
        for i in 1 .. 4 do
            store.SubmitEvents [ rec_ a (msg "user" (sprintf """[{"$type":"text","text":"问%d"}]""" i)) ] |> ignore
        store.SubmitEvents [ ConversationCreated { conversationId = b; title = "乙"; config = cfg } ] |> ignore
        store.SubmitEvents [ ConversationFlagsChanged { conversationId = b; pinned = true; archived = false } ] |> ignore
        store.SubmitEvents [ ConversationForked { conversationId = Guid "cccccccc-0000-0000-0000-000000000003"; parentConversationId = a; forkAfterId = Some 2UL } ] |> ignore
        let proj = store.Projection
        let ca = (Projection.tryConversation proj a).Value
        let sb = Text.StringBuilder()
        let line (s: string) = sb.AppendLine(norm s) |> ignore
        line "## list"; line ((ServerModel.conversationListItems proj (fun g -> if g = a then "running" else "idle")).ToJsonString())
        line "## messages"; line ((ServerModel.conversationMessages proj ca).ToJsonString())
        let tail, cursor, more = ServerModel.conversationMessagesTail proj ca 2
        line "## tail 2"; line (tail.ToJsonString()); line (sprintf "cursor=%A more=%b" cursor more)
        let page, more2 = ServerModel.historyPageItems proj ca cursor 2
        line "## page"; line (page.ToJsonString()); line (sprintf "more=%b" more2)
        let exportLine (q: ConversationExportQuery) =
            match ServerModel.exportPage proj q with
            | Ok pg -> line (sprintf "ok at=%d before=%d total=%d more=%b title=%s items=%s" pg.atCommitId pg.beforeCommitId pg.totalMessages pg.hasMore pg.title (pg.items.ToJsonString()))
            | Error e -> line ("error " + e)
        let eid = Guid "eeeeeeee-0000-0000-0000-000000000005"
        line "## export"
        exportLine { exportId = eid; conversationId = a; atCommitId = None; beforeCommitId = 0UL }
        exportLine { exportId = eid; conversationId = a; atCommitId = Some 7UL; beforeCommitId = 4UL }
        exportLine { exportId = eid; conversationId = a; atCommitId = Some 7UL; beforeCommitId = 99UL }
        exportLine { exportId = eid; conversationId = a; atCommitId = None; beforeCommitId = 3UL }
        exportLine { exportId = eid; conversationId = a; atCommitId = Some 999UL; beforeCommitId = 0UL }
        exportLine { exportId = eid; conversationId = Guid.Empty; atCommitId = None; beforeCommitId = 0UL }
        line "## refs"; line (sprintf "%A" (ServerModel.attachmentRefsOf (msg "user" att)))
        line "## referenced"; line (sprintf "%A" (ServerModel.referencedAttachments proj |> Set.toList))
        sb.ToString()
    finally
        try Directory.Delete(dir, true) with _ -> ()
