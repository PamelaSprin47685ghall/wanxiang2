module Wanxiang2.AppShell.Tests.ButtonFlowTests

// 流程按钮的活树证明：每个名字都从窗口里的真实控件走 Invoke（遮罩走 PointerPressed）。
// 快捷键、Dispatch 命令、RaiseEvent(Click) 都不算。效果只认壳自己发出的命令、事件、剪贴板和阶段。

open System
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI
open Wanxiang2.AppShell.Tests
open Wanxiang2.WorkspaceState

let private rootId = "11111111-1111-1111-1111-111111111111"
let private otherId = "22222222-2222-2222-2222-222222222222"
let private forkAId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
let private forkBId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"

let private catalogProviders =
    let provider = JsonObject()
    provider["id"] <- JsonValue.Create "p1"
    provider["label"] <- JsonValue.Create "P1"
    provider["models"] <- JsonArray([| JsonValue.Create "m1" :> JsonNode |])
    JsonArray([| provider :> JsonNode |])

let private connect (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "flow-inst" |})

    rig.Fake.Deliver(
        CatalogSnapshot
            {|
                providers = catalogProviders
                tools = JsonArray()
                generation = JsonObject()
            |}
    )

    Pump.settle ()

let private summaryItem id title last =
    JsonNode.Parse(
        sprintf
            """{"conversationId":"%s","title":"%s","lastMessage":"%s","pinned":false,"archived":false,"runtimeState":"idle","createdAt":"2026-10-20T08:00:00Z","updatedAt":"2026-10-21T08:00:00Z","messageCount":2,"isFork":false,"lastCommitId":2,"config":{"provider":"p1","model":"m1"}}"""
            id
            title
            last
    )

let private withFork (node: JsonNode) (parent: string) (first: int) =
    let o = node :?> JsonObject
    o["parentId"] <- parent
    o["isFork"] <- true
    o["forkFirstMessageId"] <- JsonValue.Create first
    node

let private messageItem commitId role contents =
    JsonNode.Parse(
        sprintf
            """{"commitId":%d,"committedAt":"2026-10-21T08:00:00Z","payload":{"role":"%s","contents":%s}}"""
            commitId
            role
            contents
    )

let private textContents (text: string) =
    sprintf """[{"type":"text","text":"%s"}]""" text

let private assistantContents =
    """[{"type":"reasoning","text":"先想清楚"},{"type":"text","text":"助手正文乙"},{"type":"functionCall","callId":"c1","name":"lookup","arguments":{"q":"x"}}]"""

let private snapshotOf (id: string) (title: string) (messages: JsonArray) =
    WireEvent.ConversationSnapshot
        {|
            conversationId = Guid.Parse id
            title = title
            lastCommitId = 20UL
            runtimeState = "idle"
            generationId = None
            messages = messages
            snapshotEarliestCommitId = 0UL
            snapshotHasMore = false
            config = SessionConfig.empty
        |}

let private shown (c: Control) =
    let rec up (v: Visual) =
        if isNull v then
            true
        else
            match v with
            | :? Control as ctrl when not ctrl.IsVisible -> false
            | _ -> up (v.GetVisualParent())

    up c

let private buttons (root: Control) =
    root.GetVisualDescendants()
    |> Seq.choose (fun v ->
        match v with
        | :? Button as b when shown b -> Some b
        | _ -> None)

let private autoName (c: Control) =
    let n = AutomationProperties.GetName c
    if isNull n then "" else n

let private dump (root: Control) =
    buttons root
    |> Seq.map (fun b ->
        let content =
            match b.Content with
            | :? string as s -> s
            | _ -> ""

        sprintf "%s〔%s〕%s" (autoName b) content (if b.IsEnabled then "" else " 禁用"))
    |> Seq.truncate 80
    |> String.concat " | "

let private findName (root: Control) (name: string) =
    buttons root |> Seq.tryFind (fun b -> autoName b = name)

let private findPrefix (root: Control) (prefix: string) =
    buttons root |> Seq.tryFind (fun b -> (autoName b).StartsWith prefix)

let private findContent (root: Control) (text: string) =
    buttons root
    |> Seq.tryFind (fun b ->
        match b.Content with
        | :? string as s -> s = text
        | _ -> false)

let private invoke (b: Button) =
    if not b.IsEnabled then
        failwith (sprintf "「%s」不可用，Invoke 不会进 Click" (autoName b))

    let peer = ControlAutomationPeer.CreatePeerForElement b
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()
    Pump.settle ()

let private clickName (root: Control) (name: string) =
    match findName root name with
    | Some b -> invoke b
    | None -> failwith (sprintf "找不到「%s」。树上：%s" name (dump root))

let private clickPrefix (root: Control) (prefix: string) =
    match findPrefix root prefix with
    | Some b ->
        invoke b
        b
    | None -> failwith (sprintf "找不到前缀「%s」。树上：%s" prefix (dump root))

let private clickContent (root: Control) (text: string) =
    match findContent root text with
    | Some b -> invoke b
    | None -> failwith (sprintf "找不到内容「%s」。树上：%s" text (dump root))

let private press (window: Window) (target: Control) =
    let args =
        PointerPressedEventArgs(
            target,
            new Pointer(1, PointerType.Mouse, true),
            window,
            Point(2.0, 2.0),
            0UL,
            PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None
        )

    target.RaiseEvent args
    Dispatcher.UIThread.RunJobs()
    Pump.settle ()

let private overlayScrim (root: Grid) (dialog: Control) =
    root.Children
    |> Seq.cast<Control>
    |> Seq.tryFind (fun c ->
        c.IsVisible
        && c.ZIndex >= 3
        && c.HorizontalAlignment = HorizontalAlignment.Stretch
        && c.VerticalAlignment = VerticalAlignment.Stretch
        && not (obj.ReferenceEquals(c, dialog)))

let private phase (rig: Rig) =
    match rig.Shell.Model.Conn with
    | ConnPhase.Authed(_, CatalogPhase.Ready(_, ws)) -> ws
    | other -> failwith (sprintf "不在已认证目录：%A" other)

let private sends (rig: Rig) =
    rig.Fake.Commands
    |> List.choose (fun cmd ->
        match cmd with
        | ClientCommand.SendUserMessage d -> Some d
        | _ -> None)

let private observes (rig: Rig) =
    rig.Fake.Events
    |> List.choose (fun ev ->
        match ev with
        | WireEvent.ObserveConversation d -> Some d.conversationId
        | _ -> None)

let private plain (tb: TextBlock) =
    let inlineText =
        if isNull tb.Inlines then
            ""
        else
            tb.Inlines
            |> Seq.choose (fun inline' ->
                match inline' with
                | :? Run as run when not (isNull run.Text) -> Some run.Text
                | _ -> None)
            |> String.concat ""

    if inlineText.Length > 0 then inlineText
    elif isNull tb.Text then ""
    else tb.Text

let private seen (root: Control) (needle: string) =
    root.GetVisualDescendants()
    |> Seq.exists (fun v ->
        match v with
        | :? TextBlock as tb -> (plain tb).Contains needle
        | _ -> false)

let private deliverList (rig: Rig) (items: JsonArray) =
    rig.Fake.Deliver(WireEvent.ConversationListSnapshot {| items = items; lastCommitId = 2UL |})
    Pump.settle ()

let private openRow (rig: Rig) (title: string) =
    match
        buttons rig.Views.Sidebar.View
        |> Seq.tryFind (fun b ->
            (autoName b).Contains title
            || b.GetVisualDescendants()
               |> Seq.exists (fun v ->
                   match v with
                   | :? TextBlock as tb -> not (isNull tb.Text) && tb.Text.Contains title
                   | _ -> false))
    with
    | Some row -> invoke row
    | None -> failwith (sprintf "没有会话行「%s」。树上：%s" title (dump rig.Views.Sidebar.View))

let private widths (rig: Rig) =
    rig.Root.Bounds.Width, rig.Views.Sidebar.View.Bounds.Width, rig.Views.Composer.Host.Bounds.Width

let private sameWidths (before: float * float * float) (after: float * float * float) (what: string) =
    let a, b, c = before
    let d, e, f = after

    Assert.True(
        abs (a - d) < 1.0 && abs (b - e) < 1.0 && abs (c - f) < 1.0,
        sprintf "%s 撑宽了壳 %0.1f/%0.1f 栏 %0.1f/%0.1f 舞台 %0.1f/%0.1f" what a d b e c f
    )

let private buttonOnMessage (root: Control) (name: string) (needle: string) (other: string) =
    let blob (c: Control) =
        c.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? TextBlock as tb ->
                let text = plain tb
                if text.Length = 0 then None else Some text
            | _ -> None)
        |> String.concat "\n"

    let rec owns (v: Visual) depth =
        if isNull v || depth > 16 then
            false
        else
            match v with
            | :? Control as c ->
                let text = blob c

                if text.Contains needle && not (text.Contains other) then
                    true
                else
                    owns (v.GetVisualParent()) (depth + 1)
            | _ -> owns (v.GetVisualParent()) (depth + 1)

    buttons root |> Seq.tryFind (fun b -> autoName b = name && owns b 0)

[<Fact>]
let ``流程按钮：输入区与未确认消息都从真实控件生效`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            connect rig

            deliverList rig (JsonArray(summaryItem rootId "流程甲" "甲", summaryItem otherId "流程乙" "乙"))

            openRow rig "流程甲"

            rig.Fake.Deliver(snapshotOf rootId "流程甲" (JsonArray(messageItem 1 "user" (textContents "流程甲的原消息"))))

            Pump.settle ()
            Assert.True(seen rig.Root "流程甲的原消息", "打开行后快照没有进消息区")

            clickName rig.Root "对话上下文"
            Assert.True(rig.Views.Settings.View.IsVisible, "对话上下文没有打开设置页")
            Assert.True(seen rig.Root "设置", "设置页没有内容")
            clickName rig.Root "返回对话"
            Assert.False(rig.Views.Settings.View.IsVisible, "返回对话后设置页还在")

            let bytes = [| 1uy; 2uy; 3uy |]

            rig.Fake.OnPickAttachment <- fun () -> rig.Shell.AttachFiles (rig.Shell.UploadOwner()) [ "pic.png", bytes ]

            clickName rig.Root "添加附件"
            Assert.Equal(1, rig.Views.Composer.Tray.Count)
            Assert.Equal(1, rig.Shell.Model.Composer.Attachments.Length)

            let draft = rig.Shell.Model.Composer.Attachments |> List.head

            rig.Fake.Deliver(
                WireEvent.AttachmentCommitted
                    {|
                        attachmentId = draft.Id
                        sha256 = UploadFacts.sha256Hex bytes
                        size = int64 bytes.Length
                    |}
            )

            Pump.settle ()
            Assert.True(rig.Shell.Model.Composer.Attachments |> List.head |> fun a -> a.Ready)

            rig.Views.Composer.Box.Input.Text <- "第一条要发出去"
            Pump.settle ()
            clickName rig.Root "发送"

            let sent = sends rig
            Assert.True(sent.Length >= 1, "点发送后没有 SendUserMessage")
            let first = sent |> List.last
            Assert.Equal("第一条要发出去", first.messageJson["contents"].AsArray().[0].["text"].GetValue<string>())
            let invocation = first.invocationId

            let genId = Guid.NewGuid()

            rig.Fake.Deliver(
                WireEvent.GenerationStarted
                    {|
                        conversationId = Guid.Parse rootId
                        generationId = genId
                        providerId = "p1"
                        model = "m1"
                    |}
            )

            Pump.settle ()
            Assert.Equal("停止生成", autoName rig.Views.Composer.Box.SendButton)

            rig.Views.Composer.Box.Input.Text <- "排队的第二条"
            Pump.settle ()

            match findName rig.Root "排队发送" with
            | None -> failwith (sprintf "生成中没有排队发送。树上：%s" (dump rig.Root))
            | Some queue ->
                Assert.True(queue.IsEnabled, sprintf "排队发送不可用：%A" (ToolTip.GetTip queue))
                invoke queue

            let queued =
                sends rig
                |> List.filter (fun d ->
                    d.messageJson["contents"].AsArray().[0].["text"].GetValue<string>() = "排队的第二条")

            Assert.Equal(1, queued.Length)
            Assert.NotEqual(invocation, queued.[0].invocationId)

            match phase rig with
            | WorkspacePhase.Active(_, RunState.Generating(id, _, _)) -> Assert.Equal(genId, id)
            | other -> failwith (sprintf "排队时生成已结束：%A" other)

            clickName rig.Root "停止生成"

            Assert.True(
                rig.Fake.Events
                |> List.exists (fun ev ->
                    match ev with
                    | WireEvent.GenerationCancel d -> d.generationId = genId && d.conversationId = Guid.Parse rootId
                    | _ -> false),
                "停止生成没有发出 GenerationCancel"
            )

            rig.Fake.Deliver(
                WireEvent.GenerationFinished
                    {|
                        conversationId = Guid.Parse rootId
                        generationId = genId
                        status = "cancelled"
                        error = None
                        usage = None
                    |}
            )

            Pump.settle ()

            rig.Fake.Deliver(
                WireEvent.CommandRejected
                    {|
                        invocationId = invocation
                        code = "rejected"
                        message = "主机拒绝了这条消息"
                        requiredCommitId = None
                    |}
            )

            Pump.settle ()
            Assert.True(findName rig.Root "重试" |> Option.isSome, sprintf "拒绝后没有重试。树上：%s" (dump rig.Root))

            openRow rig "流程乙"

            rig.Fake.Deliver(snapshotOf otherId "流程乙" (JsonArray(messageItem 1 "user" (textContents "流程乙的原消息"))))

            Pump.settle ()
            clickPrefix rig.Root "查看另一个对话的未确认消息" |> ignore

            Assert.Equal(Guid.Parse rootId, observes rig |> List.last)

            match phase rig with
            | WorkspacePhase.LoadingConversation id -> Assert.Equal(Guid.Parse rootId, id)
            | other -> failwith (sprintf "查看另一条没有进入该对话：%A" other)

            rig.Fake.Deliver(snapshotOf rootId "流程甲" (JsonArray(messageItem 1 "user" (textContents "流程甲的原消息"))))

            Pump.settle ()

            let before =
                sends rig |> List.filter (fun d -> d.invocationId = invocation) |> List.length

            clickName rig.Root "重试"
            let after = sends rig |> List.filter (fun d -> d.invocationId = invocation)
            Assert.True(after.Length = before + 1, sprintf "重试没有用原 invocation 再发，之前 %d 之后 %d" before after.Length)
        finally
            window.Close())

[<Fact>]
let ``流程按钮：消息、工具、思考、错误卡与版本都从真实控件生效`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            connect rig

            let rootSummary = summaryItem rootId "版本根对话" "根"
            let forkA = withFork (summaryItem forkAId "分叉甲" "甲") rootId 10
            let forkB = withFork (summaryItem forkBId "分叉乙" "乙") rootId 20
            deliverList rig (JsonArray(rootSummary, forkA, forkB))
            openRow rig "版本根对话"

            let rich =
                JsonArray(messageItem 1 "user" (textContents "用户原文甲"), messageItem 2 "assistant" assistantContents)

            rig.Fake.Deliver(snapshotOf rootId "版本根对话" rich)
            Pump.settle ()
            Assert.True(seen rig.Root "用户原文甲")
            Assert.True(seen rig.Root "助手正文乙")

            match findName rig.Root "上一个版本" with
            | Some previous -> Assert.False(previous.IsEnabled, "根对话第一条不应能回到上一个版本")
            | None -> failwith (sprintf "根对话没有版本条。树上：%s" (dump rig.Root))

            clickName rig.Root "下一个版本"
            Assert.Equal(Guid.Parse forkAId, observes rig |> List.last)

            rig.Fake.Deliver(snapshotOf forkAId "分叉甲" (JsonArray(messageItem 10 "user" (textContents "分叉甲的原文"))))

            Pump.settle ()
            clickName rig.Root "下一个版本"
            Assert.Equal(Guid.Parse forkBId, observes rig |> List.last)

            rig.Fake.Deliver(snapshotOf forkAId "分叉甲" (JsonArray(messageItem 10 "user" (textContents "分叉甲的原文"))))

            Pump.settle ()
            clickName rig.Root "上一个版本"
            Assert.Equal(Guid.Parse rootId, observes rig |> List.last)

            rig.Fake.Deliver(snapshotOf rootId "版本根对话" rich)
            Pump.settle ()

            let copiesBefore = rig.Fake.Copied.Length
            clickName rig.Root "复制用户消息 1"
            Assert.True(rig.Fake.Copied |> List.exists (fun t -> t.Contains "用户原文甲"), "复制用户消息没有进剪贴板")
            Assert.Equal("已复制", autoName (findName rig.Root "已复制" |> Option.get))
            clickName rig.Root "复制助手消息 2"
            Assert.True(rig.Fake.Copied |> List.exists (fun t -> t.Contains "助手正文乙"), "复制助手消息没有进剪贴板")
            Assert.True(rig.Fake.Copied.Length >= copiesBefore + 2)

            let beforeTool = widths rig
            let tool = clickPrefix rig.Root "展开工具调用"
            Assert.StartsWith("收起工具调用", autoName tool)
            sameWidths beforeTool (widths rig) "展开工具"
            invoke tool
            Assert.StartsWith("展开工具调用", autoName tool)
            sameWidths beforeTool (widths rig) "收起工具"

            let beforeThink = widths rig
            let thinking = clickPrefix rig.Root "展开思考过程"
            Assert.Equal("收起思考过程", autoName thinking)
            sameWidths beforeThink (widths rig) "展开思考"
            invoke thinking
            Assert.Equal("展开思考过程", autoName thinking)
            sameWidths beforeThink (widths rig) "收起思考"

            clickName rig.Root "重新生成"

            Assert.True(
                rig.Fake.Commands
                |> List.exists (fun cmd ->
                    match cmd with
                    | ClientCommand.RegenerateResponse d -> d.conversationId = Guid.Parse rootId
                    | _ -> false),
                "重新生成没有发出 RegenerateResponse"
            )

            match buttonOnMessage rig.Root "删除这条消息" "助手正文乙" "用户原文甲" with
            | None -> failwith (sprintf "助手卡上没有删除。树上：%s" (dump rig.Root))
            | Some deleteButton ->
                invoke deleteButton
                Assert.True(rig.Views.Overlay.Layer.IsDialogOpen, "删除没有打开确认框")
                clickContent rig.Root "取消"
                Assert.False(rig.Views.Overlay.Layer.IsDialogOpen, "取消没有关上确认框")

                Assert.False(
                    rig.Fake.Commands
                    |> List.exists (fun cmd ->
                        match cmd with
                        | ClientCommand.DeleteMessage _ -> true
                        | _ -> false),
                    "取消后仍然发出了 DeleteMessage"
                )

                Assert.True(seen rig.Root "助手正文乙")
                invoke deleteButton
                clickName rig.Root "删除"

            Assert.False(rig.Views.Overlay.Layer.IsDialogOpen, "确认删除后对话框还开着")

            let removed =
                rig.Fake.Commands
                |> List.choose (fun cmd ->
                    match cmd with
                    | ClientCommand.DeleteMessage d -> Some d.messageCommitId
                    | _ -> None)

            Assert.Equal<uint64 list>([ 2UL ], removed)

            rig.Fake.Deliver(snapshotOf rootId "版本根对话" (JsonArray(messageItem 1 "user" (textContents "用户原文甲"))))

            Pump.settle ()
            Assert.False(seen rig.Root "助手正文乙", "确认删除后助手消息还在")
            Assert.True(seen rig.Root "用户原文甲")

            let genId = Guid.NewGuid()

            rig.Fake.Deliver(
                WireEvent.GenerationStarted
                    {|
                        conversationId = Guid.Parse rootId
                        generationId = genId
                        providerId = "p1"
                        model = "m1"
                    |}
            )

            let payload = JsonObject()
            payload["role"] <- "assistant"
            let contents = JsonArray()
            let text = JsonObject()
            text["type"] <- "text"
            text["text"] <- "半截回复还在这里"
            contents.Add text
            payload["contents"] <- contents

            rig.Fake.Deliver(
                WireEvent.GenerationDelta
                    {
                        conversationId = Guid.Parse rootId
                        generationId = genId
                        payload = payload
                    }
            )

            let err: GenerationError =
                {
                    kind = GenerationErrorKind.ProviderTimeout
                    message = "请求超时"
                    retryable = true
                    detail = None
                    retryAfterSeconds = None
                }

            rig.Fake.Deliver(
                WireEvent.GenerationFinished
                    {|
                        conversationId = Guid.Parse rootId
                        generationId = genId
                        status = "failed"
                        error = Some err
                        usage = None
                    |}
            )

            Pump.settle ()
            Assert.True(findName rig.Root "继续生成" |> Option.isSome, sprintf "失败后没有继续生成。树上：%s" (dump rig.Root))
            Assert.True(findName rig.Root "重试生成" |> Option.isSome, sprintf "失败后没有重试生成。树上：%s" (dump rig.Root))
            clickName rig.Root "继续生成"

            let continued =
                rig.Fake.Commands
                |> List.choose (fun cmd ->
                    match cmd with
                    | ClientCommand.ContinueResponse d when d.conversationId = Guid.Parse rootId ->
                        Some(d.messageJson["contents"].AsArray().[0].["text"].GetValue<string>())
                    | _ -> None)

            Assert.Contains("半截回复还在这里", continued)
            Assert.True(findName rig.Root "重试生成" |> Option.isSome, "继续之后错误卡不该一起消失")
            clickName rig.Root "重试生成"

            Assert.True(
                rig.Fake.Commands
                |> List.exists (fun cmd ->
                    match cmd with
                    | ClientCommand.RegenerateResponse _ -> true
                    | _ -> false),
                "重试生成没有发出 RegenerateResponse"
            )

            // 主机确认命令并开始重新生成：错误卡随 GenerationStarted 离开（命令没确认前错误卡必须还在）。
            let regen =
                rig.Fake.Commands
                |> List.tryPick (function
                    | ClientCommand.RegenerateResponse d -> Some d
                    | _ -> None)
                |> Option.defaultWith (fun () -> failwith "没有 RegenerateResponse 命令")

            rig.Fake.Deliver(
                CommandCommitted
                    {|
                        invocationId = regen.invocationId
                        commandId = "cmd-retry"
                        commitId = 1UL
                    |}
            )

            let regenGenId = Guid.NewGuid()

            rig.Fake.Deliver(
                GenerationStarted
                    {|
                        conversationId = Guid.Parse rootId
                        generationId = regenGenId
                        providerId = "p1"
                        model = "m1"
                    |}
            )

            Pump.settle ()
            Assert.True(findName rig.Root "重试生成" |> Option.isNone, sprintf "重试后错误卡还在。树上：%s" (dump rig.Root))
            Assert.True(findName rig.Root "继续生成" |> Option.isNone, "重试后继续生成还在")

            clickName rig.Root "编辑并分叉"
            Assert.True(rig.Views.Overlay.Layer.IsDialogOpen, "编辑并分叉没有打开对话框")
            Assert.True(findName rig.Root "创建分叉" |> Option.isSome, sprintf "没有创建分叉。树上：%s" (dump rig.Root))
            clickName rig.Root "创建分叉"
            Assert.False(rig.Views.Overlay.Layer.IsDialogOpen)

            let forked =
                rig.Fake.Commands
                |> List.choose (fun cmd ->
                    match cmd with
                    | ClientCommand.ForkConversation d -> Some d
                    | _ -> None)

            Assert.Equal(1, forked.Length)
            Assert.Equal(Guid.Parse rootId, forked.[0].parentConversationId)

            match phase rig with
            | WorkspacePhase.LoadingConversation id -> Assert.Equal(forked.[0].conversationId, id)
            | other -> failwith (sprintf "创建分叉后没有进入新对话：%A" other)
        finally
            window.Close())

[<Fact>]
let ``流程按钮：壳层、确认框与抽屉遮罩都从真实控件生效`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            connect rig
            deliverList rig (JsonArray(summaryItem rootId "壳层对话" "壳"))
            openRow rig "壳层对话"

            rig.Fake.Deliver(snapshotOf rootId "壳层对话" (JsonArray(messageItem 1 "user" (textContents "确认框里的这句话"))))

            Pump.settle ()
            clickName rig.Root "新对话"

            match phase rig with
            | WorkspacePhase.IdleEmpty -> ()
            | other -> failwith (sprintf "新对话没有回到空对话：%A" other)

            clickName rig.Root "设置（Ctrl / ⌘ + ,）"
            Assert.True(rig.Views.Settings.View.IsVisible, "设置按钮没有打开设置页")
            clickName rig.Root "返回对话"
            Assert.False(rig.Views.Settings.View.IsVisible, "返回对话没有关上设置页")

            clickName rig.Root "命令面板"
            Assert.True(rig.Views.Overlay.Layer.IsDialogOpen, "命令面板没有打开")
            Assert.True(rig.Views.Overlay.Layer.DialogCard.IsVisible)

            match overlayScrim rig.Root rig.Views.Overlay.Layer.DialogCard with
            | None -> failwith "命令面板打开后找不到遮罩"
            | Some scrim -> press window scrim

            Assert.False(rig.Views.Overlay.Layer.IsDialogOpen, "点遮罩没有关上命令面板")
            Assert.False(rig.Views.Overlay.Layer.DialogCard.IsVisible)

            let rail = rig.Views.Sidebar.View
            Assert.True(rail.IsVisible, "侧栏一开始应该开着")
            clickName rig.Root "切换侧边栏（Ctrl / ⌘ + B）"
            Assert.True(rig.Views.Workspace.Shell.State.sidebarCollapsed, "第一次切换没有收起侧栏")
            Assert.False(rail.IsVisible, "收起后侧栏还占着")
            clickName rig.Root "切换侧边栏（Ctrl / ⌘ + B）"
            Assert.False(rig.Views.Workspace.Shell.State.sidebarCollapsed, "第二次切换没有展开侧栏")
            Assert.True(rail.IsVisible)
            Assert.True(rail.Bounds.Width > 200.0, sprintf "展开后栏宽 %0.1f" rail.Bounds.Width)

            openRow rig "壳层对话"

            rig.Fake.Deliver(snapshotOf rootId "壳层对话" (JsonArray(messageItem 7 "user" (textContents "确认框里的这句话"))))

            Pump.settle ()
            clickName rig.Root "删除这条消息"
            Assert.True(rig.Views.Overlay.Layer.IsDialogOpen)
            clickContent rig.Root "取消"
            Assert.False(rig.Views.Overlay.Layer.IsDialogOpen, "取消没有关上确认框")
            Assert.True(seen rig.Root "确认框里的这句话")

            Assert.False(
                rig.Fake.Commands
                |> List.exists (fun cmd ->
                    match cmd with
                    | ClientCommand.DeleteMessage _ -> true
                    | _ -> false)
            )

            clickName rig.Root "删除这条消息"
            clickName rig.Root "删除"
            Assert.False(rig.Views.Overlay.Layer.IsDialogOpen, "确认没有关上确认框")

            Assert.True(
                rig.Fake.Commands
                |> List.exists (fun cmd ->
                    match cmd with
                    | ClientCommand.DeleteMessage d -> d.messageCommitId = 7UL
                    | _ -> false),
                "确认没有发出 DeleteMessage"
            )

            rig.Fake.Deliver(snapshotOf rootId "壳层对话" (JsonArray()))
            Pump.settle ()
            Assert.False(seen rig.Root "确认框里的这句话", "确认删除后消息还在")

            window.Width <- 600.0
            window.Height <- 800.0
            Pump.settle ()
            Assert.True(rig.Views.Workspace.Shell.State.compactMode, "600 宽没有进入紧凑抽屉")

            let drawer = rig.Views.Workspace.Shell.Columns.Scrim

            if drawer.IsVisible then
                press window drawer

            Assert.False(rig.Views.Workspace.Shell.State.compactNavigationOpen)
            clickName rig.Root "切换侧边栏（Ctrl / ⌘ + B）"
            Assert.True(rig.Views.Workspace.Shell.State.compactNavigationOpen, "紧凑档切换没有打开抽屉")
            Assert.True(drawer.IsVisible, "抽屉打开后遮罩不可见")
            press window drawer
            Assert.False(rig.Views.Workspace.Shell.State.compactNavigationOpen, "点遮罩没有关上抽屉")
            Assert.False(drawer.IsVisible)
        finally
            window.Close())
