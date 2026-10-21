module Tests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

let private forkLineage parent =
    {
        parentId = Some parent
        parentTitle = Some "原对话"
        parentDeleted = false
        forkOrdinal = Some 3
        forkBaseMessageId = Some 4UL
        forks = [ Guid.NewGuid(), "子分叉", Some 9UL ]
    }

// 迁自旧 R14TransferTests。
[<Fact>]
let ``312 载荷里出现密钥类字段一律去掉`` () =
    let raw = JsonObject()
    raw["commitId"] <- 5UL

    raw["payload"] <-
        JsonNode.Parse(
            """{"role":"assistant","apiKey":"sk-xx","contents":[{"type":"text","text":"hi","Authorization":"Bearer y"}]}"""
        )

    let json =
        ConversationTransfer.toJson
            (Guid.NewGuid())
            "t"
            5UL
            DateTimeOffset.UtcNow
            ConversationTransfer.rootLineage
            [ raw ]

    Assert.DoesNotContain("sk-xx", json)
    Assert.DoesNotContain("Bearer y", json)
    Assert.Contains("\"hi\"", json)
    Assert.Contains("\"kind\": \"root\"", json)

[<Fact>]
let ``312 Markdown 头部写明来源、分叉和隐私说明`` () =
    let md =
        ConversationTransfer.withPreamble
            "# 标题\n\n## 用户\n\n你好\n"
            (ConversationTransfer.markdownPreamble (forkLineage (Guid.NewGuid())) 2 DateTimeOffset.UtcNow)

    Assert.StartsWith("# 标题\n\n> 导出自万象", md)
    Assert.Contains("「原对话」第 3 条消息的分叉", md)
    Assert.Contains("分出了 1 个分叉：「子分叉」", md)
    Assert.Contains("不含 API 密钥", md)
    Assert.Contains("## 用户", md)

[<Fact>]
let ``313 不认识的文件给出可读原因，不部分导入`` () =
    let expectError (text: string) (fragment: string) =
        match ConversationTransfer.parseImportWithin 5000 (int64 text.Length) text with
        | Error e -> Assert.Contains(fragment, e)
        | Ok _ -> failwithf "should reject %s" text

    expectError "# 标题\n\n正文" "不是有效的 JSON"
    expectError """{"hello":1}""" "不是万象导出的对话文件"
    expectError """{"format":"wanxiang.conversation","formatVersion":9,"messages":[]}""" "更新版本"
    expectError """{"format":"wanxiang.conversation","formatVersion":1,"messages":[]}""" "没有可导入的消息"

    expectError
        """{"format":"wanxiang.conversation","formatVersion":1,"messages":[{"payload":{"role":"robot"}}]}"""
        "没有可导入的消息"

    match ConversationTransfer.parseImportWithin 5000 (ConversationTransfer.maxImportBytes + 1L) "" with
    | Error e -> Assert.Contains("超过", e)
    | Ok _ -> failwith "oversize"

// 迁自旧 R14TransferTests `313 导出的 JSON 能解析回预览…`：原用例的导出来自 EventStore 投影，这里直接用 toJson 产出同形的文件。
[<Fact>]
let ``313 导出的 JSON 能解析回预览：标题、条数、分叉说明`` () =
    let raw n =
        JsonNode
            .Parse(
                sprintf """{"commitId":%d,"payload":{"role":"user","contents":[{"type":"text","text":"m%d"}]}}""" n n
            )
            .AsObject()

    let text =
        ConversationTransfer.toJson
            (Guid.NewGuid())
            "旅行计划"
            4UL
            DateTimeOffset.UtcNow
            (forkLineage (Guid.NewGuid()))
            [ for n in 1..4 -> raw n ]

    match ConversationTransfer.parseImportWithin 5000 (int64 text.Length) text with
    | Ok p ->
        Assert.Equal("旅行计划", p.title)
        Assert.Equal(4, p.messages.Length)
        Assert.Equal(0, p.skipped)
        Assert.Contains("分叉关系不会恢复", p.lineageNote.Value)
        Assert.Equal("旅行计划（导入）", ConversationTransfer.importedTitle p.title)
    | Error e -> failwith e
