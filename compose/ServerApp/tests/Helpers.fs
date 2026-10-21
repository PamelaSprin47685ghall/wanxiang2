module Wanxiang.Tests.Helpers

open System
open System.IO
open Wanxiang.Core

/// 测试辅助：临时目录。
let tempDir () =
    let dir =
        Path.Combine(Path.GetTempPath(), "wanxiang-test-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory dir |> ignore
    dir

let cleanup (dir: string) =
    try
        Directory.Delete(dir, true)
    with _ ->
        ()

let userMessageJson (text: string) : System.Text.Json.Nodes.JsonNode =
    let o = System.Text.Json.Nodes.JsonObject()
    o["role"] <- "user"
    let contents = System.Text.Json.Nodes.JsonArray()
    let tc = System.Text.Json.Nodes.JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let assistantMessageJson (text: string) : System.Text.Json.Nodes.JsonNode =
    let o = System.Text.Json.Nodes.JsonObject()
    o["role"] <- "assistant"
    let contents = System.Text.Json.Nodes.JsonArray()
    let tc = System.Text.Json.Nodes.JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let testConfig () =
    {
        provider = "openai"
        model = "test-model"
        instructions = None
        tools = []
        temperature = None
        topP = None
        maxTokens = None
        thinkingBudget = None
        extraJson = None
    }

let newConversationId () = Guid.NewGuid()

/// 只读检查并断言健康，返回 outcome（S-001：检查与修复分离后，测试统一走 inspect）。
let replayOutcome (dir: string) : Wanxiang.Store.ReplayOutcome =
    match Wanxiang.Store.Inspect.run dir with
    | Wanxiang.Store.Healthy o -> o
    | Wanxiang.Store.RepairRequired(_, _) -> failwith "expected healthy log"
    | Wanxiang.Store.UnsupportedFormat msg -> failwith msg
    | Wanxiang.Store.Unreadable msg -> failwith msg

/// 打开存储、存一次快照、关闭（快照归 LedgerStore；测试用它代替旧的 Snapshot.write）。
let saveSnapshot (dir: string) : Result<unit, string> =
    use s =
        new Wanxiang.Store.EventStore(dir, policy = LedgerStore.SnapshotPolicy.Manual)

    s.SaveSnapshot()
