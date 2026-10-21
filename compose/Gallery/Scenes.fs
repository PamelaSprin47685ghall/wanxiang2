namespace Wanxiang2.Gallery

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState

/// 画廊的场景：每个场景是一串消息，喂进 Workspace.update 之后投影出 WorkspaceViewState。
///
/// 场景不回退成手搓的视图状态记录——那样画廊就会与真实界面漂移。
/// 这里唯一允许的「假」是线协议快照的 JSON：它是主机真正发过来的字节形状，
/// 走的是 WireEvent.CatalogSnapshot / ConversationListSnapshot / ConversationSnapshot 这条真实解析路径。
module Scenes =

    /// 场景的驱动脚本：一串消息，按顺序喂给 Workspace.update。
    type Step =
        /// 一条线协议事件（主机发来的）
        | Wire of WireEvent
        /// 一条用户/宿主消息
        | Msg of Msg

    /// 画廊的固定「现在」。
    ///
    /// 用 UtcNow 的话同一场景每次跑出的时间戳都不同（截图里的时刻会漂），
    /// 渲染就不可复现、像素基线也没法比。这里钉死一个时刻。
    let fixedNow = DateTimeOffset.Parse "2026-10-21T08:00:00Z"

    /// 跑一串步骤，返回最终模型与投影。
    let run (steps: Step list) : WorkspaceModel * WorkspaceViewState =
        let mutable model = Workspace.init ()
        let now = fixedNow

        for step in steps do
            let msg =
                match step with
                | Wire ev -> Msg.WireRecv ev
                | Msg m -> m

            let next, _ = Workspace.update now msg model
            model <- next

        model, Workspace.project model

    /// 解析一段 JSON 文本（画廊的快照都是字面量 JSON，与主机实际下发的形状一致）。
    let private json (text: string) : JsonNode = JsonNode.Parse text

    /// 解析一段 JSON 数组。
    let private jsonArray (text: string) : JsonArray = (JsonNode.Parse text).AsArray()

    /// 当前对话的 id（画廊固定用这一条）。
    let conversationId = "11111111-1111-1111-1111-111111111111"

    // ---------------------------------------------------------------- 目录

    /// 一个可用的模型服务目录快照（主机真实下发的形状）。
    let catalogSnapshot: WireEvent =
        let providers =
            json
                """[{"id":"openai","label":"OpenAI","kind":"openai","baseUrl":"https://api.openai.com/v1","hasApiKey":true,"models":["gpt-5","gpt-5-mini"],"defaultModel":"gpt-5","timeoutSeconds":120,"maxRetries":2,"enabled":true}]"""

        let tools =
            json """[{"id":"read_file","label":"读取文件","description":"读取本机文件","source":"builtin"}]"""

        let generation =
            json
                """{"temperature":0.7,"maxContextMessages":200,"autoTitle":true,"maxToolRounds":12,"defaultProvider":"openai","defaultModel":"gpt-5"}"""

        WireEvent.CatalogSnapshot
            {|
                providers = providers.AsArray()
                tools = tools.AsArray()
                generation = generation.AsObject()
            |}

    /// 认证成功 + 目录就绪：后面所有场景的公共前缀。
    let authed: Step list =
        [
            Wire(WireEvent.AuthAccepted {| instanceId = "gallery-instance" |})
            Wire catalogSnapshot
        ]

    // ---------------------------------------------------------------- 对话列表

    /// 一条对话的列表行（主机真实下发的字段名）。
    let private summaryItem id title preview pinned archived running =
        json (
            sprintf
                """{"conversationId":"%s","title":"%s","lastMessage":"%s","pinned":%b,"archived":%b,"runtimeState":"%s","createdAt":"2026-10-20T08:00:00Z","updatedAt":"2026-10-21T08:00:00Z","messageCount":4,"isFork":false,"lastCommitId":12,"config":{"provider":"openai","model":"gpt-5"}}"""
                id
                title
                preview
                pinned
                archived
                (if running then "generating" else "idle")
        )

    /// 三条对话的列表快照（置顶 / 普通 / 归档）。
    let listSnapshot: WireEvent =
        let items: JsonNode array =
            [|
                summaryItem conversationId "画廊里的第一条对话" "上一条回复的摘要" true false false
                summaryItem "22222222-2222-2222-2222-222222222222" "第二条对话" "另一条摘要" false false false
                summaryItem "33333333-3333-3333-3333-333333333333" "归档的对话" "归档行" false true false
            |]

        WireEvent.ConversationListSnapshot
            {|
                items = JsonArray items
                lastCommitId = 12UL
            |}

    // ---------------------------------------------------------------- 消息

    /// 一条消息条目（快照里的 {commitId, committedAt, payload} 形状）。
    let private messageItem commitId role contents =
        json (
            sprintf
                """{"commitId":%d,"committedAt":"2026-10-21T08:00:00Z","payload":{"role":"%s","contents":%s}}"""
                commitId
                role
                contents
        )

    /// 一条助手回复（纯文本）。
    let assistantText (commitId: int) (text: string) =
        messageItem commitId "assistant" (sprintf """[{"type":"text","text":"%s"}]""" text)

    /// 一条用户消息。
    let userText (commitId: int) (text: string) =
        messageItem commitId "user" (sprintf """[{"type":"text","text":"%s"}]""" text)

    /// 一条带工具调用的助手消息。
    let assistantToolCall (commitId: int) =
        messageItem
            commitId
            "assistant"
            """[{"type":"functionCall","callId":"call-1","name":"read_file","arguments":{"path":"/tmp/example.txt","encoding":"utf-8"}},{"type":"text","text":"正在读取文件。"}]"""

    /// 一条带思维链的助手消息。
    let assistantReasoning (commitId: int) =
        messageItem
            commitId
            "assistant"
            """[{"type":"reasoning","text":"先看目录，再决定读哪个文件。"},{"type":"text","text":"我先确认一下目录结构。"}]"""

    /// 当前对话的快照（标题 + 消息）。
    let conversationSnapshot (messages: JsonArray) : WireEvent =
        WireEvent.ConversationSnapshot
            {|
                conversationId = Guid.Parse conversationId
                title = "画廊里的第一条对话"
                lastCommitId = 12UL
                runtimeState = "idle"
                generationId = None
                messages = messages
                snapshotEarliestCommitId = 0UL
                snapshotHasMore = false
                config = SessionConfig.empty
            |}

    /// 对话就绪（目录 + 列表 + 当前对话）。
    let withConversation (messages: JsonArray) : Step list =
        authed @ [ Wire listSnapshot; Wire(conversationSnapshot messages) ]

    // ---------------------------------------------------------------- 流式

    /// 生成开始的线协议事件（当前对话）。
    let generationStarted (generationId: string) : WireEvent =
        WireEvent.GenerationStarted
            {|
                conversationId = Guid.Parse conversationId
                generationId = Guid.Parse generationId
                providerId = "openai"
                model = "gpt-5"
            |}

    /// 一条流式增量：**真的** generation.delta，带真实的助手正文 payload。
    /// 走这条路径 `RunState.Generating` 里才有 partial，投影才会多出一行流式消息。
    let generationDelta (generationId: string) (text: string) : WireEvent =
        WireEvent.GenerationDelta
            {
                conversationId = Guid.Parse conversationId
                generationId = Guid.Parse generationId
                payload = json (sprintf """{"role":"assistant","contents":[{"type":"text","text":"%s"}]}""" text)
            }
