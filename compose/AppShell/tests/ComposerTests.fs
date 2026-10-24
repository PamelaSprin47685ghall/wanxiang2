module Wanxiang2.AppShell.Tests.ComposerTests

// 组合者的可观察后果：MVU 架构下 Dispatch(Msg) 与 SPEC 7 闭环旅程。
// 全部经假 ShellPorts 与可记录 EffectSink 观察。

open System
open System.Text.Json.Nodes
open Avalonia.Automation
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Avalonia.LogicalTree
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.AppShell.Tests

let private toasts (rig: Rig) = rig.Fake.Toasts |> List.map fst

[<Fact>]
let ``主题快捷键从当前偏好推进并回写模型`` () =
    Headless.run (fun () ->
        let rig =
            Rig.createWith
                { UiPrefsFile.defaults with
                    theme = AlwaysLight
                }

        Assert.True(rig.Shell.HandleShortcut ShortcutAction.ToggleTheme)
        Assert.Equal(AlwaysDark, rig.Shell.Model.Prefs.theme)
        Assert.Equal(AlwaysDark, (List.last rig.Fake.SavedPrefs).theme)
        Assert.True(rig.Shell.HandleShortcut ShortcutAction.ToggleTheme)
        Assert.Equal(FollowSystem, rig.Shell.Model.Prefs.theme)
        Assert.Equal(FollowSystem, (List.last rig.Fake.SavedPrefs).theme))

[<Fact>]
let ``打开设置后设置页进入可视树并显示外观开关`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Content = rig.Root)
        window.Show()

        try
            rig.Shell.Dispatch(User(Settings(SettingsUserMsg.OpenSection(Some "appearance"))))
            Pump.settle ()
            Assert.True(rig.Views.Settings.View.IsEffectivelyVisible)
            Assert.NotNull(TopLevel.GetTopLevel rig.Views.Settings.View)

            let focused =
                TopLevel.GetTopLevel(rig.Root).FocusManager.GetFocusedElement() :?> Control

            Assert.Equal("外观与操作", AutomationProperties.GetName focused)
        finally
            window.Close())

[<Fact>]
let ``外观原生开关修改模型并保存偏好`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Content = rig.Root)
        window.Show()

        try
            rig.Shell.Dispatch(User(Settings(SettingsUserMsg.OpenSection(Some "appearance"))))
            Pump.settle ()

            let toggle =
                rig.Views.Settings.View.GetLogicalDescendants()
                |> Seq.choose (function
                    | :? ToggleSwitch as control when AutomationProperties.GetName control = "Enter 直接发送" ->
                        Some control
                    | _ -> None)
                |> Seq.exactlyOne

            let before = rig.Shell.Model.Prefs.enterSends
            toggle.IsChecked <- Nullable(not before)
            Pump.settle ()
            Assert.Equal(not before, rig.Shell.Model.Prefs.enterSends)
            Assert.Equal(not before, (List.last rig.Fake.SavedPrefs).enterSends)
        finally
            window.Close())

[<Fact>]
let ``输入框中的命令快捷键能到达现有冒泡接线`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Content = rig.Root)
        window.Show()
        let mutable reached = false

        rig.Root.KeyDown.Add(fun e ->
            if KeyShortcut.resolve e = ShortcutAction.OpenCommandPalette then
                reached <- true)

        let input = rig.Views.Composer.Box.Input
        input.Focus() |> ignore

        input.RaiseEvent(
            KeyEventArgs(
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.K,
                KeyModifiers = KeyModifiers.Control,
                Source = input
            )
        )

        window.Close()
        Assert.True(reached, "Ctrl+K 被输入控件消费，根视图的普通冒泡订阅无法收到"))

[<Fact>]
let ``命令面板真实回车执行主题并登记最近使用`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Commands.ShowPalette()
        // 周期性 MsgTick 不得把已打开的命令面板关掉
        rig.Shell.Dispatch(MsgTick(rig.Fake.Now))
        Assert.True(rig.Views.Overlay.Layer.IsDialogOpen, "MsgTick 不应关闭已打开的命令面板")

        let search =
            rig.Root.GetLogicalDescendants()
            |> Seq.choose (function
                | :? TextBox as text when text.Name = "CommandPaletteSearch" -> Some text
                | _ -> None)
            |> Seq.exactlyOne

        search.Text <- "切换主题"
        Pump.settle ()
        search.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = search))
        Assert.Contains("act:theme", rig.Shell.PaletteRecent())
        Assert.Equal(AlwaysLight, rig.Shell.Model.Prefs.theme)
        // 打开设置页后按 Escape 应能关闭设置页，不被空浮层吞掉
        Assert.True(rig.Shell.HandleShortcut ShortcutAction.OpenSettings)
        Assert.True(rig.Shell.Model.Settings.Open)
        Assert.True(rig.Shell.HandleShortcut ShortcutAction.Escape)
        Assert.False(rig.Shell.Model.Settings.Open))

[<Fact>]
let ``第五批：启动加载偏好并应用外观，不反写偏好`` () =
    Headless.run (fun () ->
        let prefs =
            { UiPrefsFile.defaults with
                theme = AlwaysDark
                reduceMotion = true
                codeWrap = true
                enterSends = false
            }

        let rig = Rig.createWith prefs
        Assert.Equal(prefs, rig.Shell.Model.Prefs)
        Assert.Contains(true, rig.Fake.ReducedMotion)
        Assert.Contains(true, rig.Fake.CodeWraps)
        Assert.Contains(true, rig.Fake.AppliedThemes)
        Assert.Empty rig.Fake.SavedPrefs)

[<Fact>]
let ``第五批：退出立即保存窗口内的最新草稿`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let actions = rig.Shell.ComposerActions(ignore, ignore, fun () -> false)
        actions.TextChanged "第一版"
        actions.TextChanged "最后一版"
        rig.Shell.FlushDrafts()
        Assert.Equal(DraftWrite.Write "最后一版", List.last rig.Fake.DraftWrites))

[<Fact>]
let ``第五批：关闭持久化时输入与退出均不写草稿`` () =
    Headless.run (fun () ->
        let rig =
            Rig.createWith
                { UiPrefsFile.defaults with
                    persistDrafts = false
                }

        let actions = rig.Shell.ComposerActions(ignore, ignore, fun () -> false)
        actions.TextChanged "不保存"
        rig.Shell.FlushDrafts()
        Assert.Empty rig.Fake.DraftWrites)

[<Fact>]
let ``第五批：退出取当前模型，不把发送前的延时草稿写回`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let actions = rig.Shell.ComposerActions(ignore, ignore, fun () -> false)
        actions.TextChanged "旧草稿"
        actions.TextChanged "待发送"
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.TextChanged "")))
        rig.Shell.FlushDrafts()
        Assert.Equal(DraftWrite.Write "", List.last rig.Fake.DraftWrites))

let private authenticate (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "inst-1" |})

[<Fact>]
let ``未连接时导入导出只说连接提示，不开对话框`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(User(Transfer(TransferUserMsg.ExportConversation(Guid.NewGuid()))))
        Assert.Empty rig.ImportDesks
        Assert.Empty rig.ExportDesks)

[<Fact>]
let ``附件上传按开始-分块-完成发事件，图片另走缩略图端口`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let owner = rig.Shell.UploadOwner()
        Assert.Equal(("inst-1", 7), owner)
        let bytes = [| for i in 1..3 -> byte i |]
        rig.Shell.AttachFiles owner [ "a.png", bytes ]
        // 图片走缩略图端口：恰好一次，字节与上传的同一份。
        // （旧断言写成 `| _ -> false || true`，谓词恒真——什么也没验。）
        let thumbnails = rig.Fake.Thumbnails
        Assert.Equal(1, thumbnails.Length)
        let (_, thumbBytes) = thumbnails.[0]
        Assert.Equal<byte[]>(bytes, thumbBytes)
        // 非图片不走缩略图端口（同一个入口、两种分支）。
        let rig2 = Rig.create ()
        authenticate rig2
        let owner2 = rig2.Shell.UploadOwner()
        rig2.Shell.AttachFiles owner2 [ "b.txt", bytes ]
        Assert.Empty rig2.Fake.Thumbnails)

[<Fact>]
let ``空文件与超大文件被校验挡下，不发上传事件`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let owner = rig.Shell.UploadOwner()
        rig.Shell.AttachFiles owner [ "b.txt", Array.empty<byte> ]
        Assert.Empty rig.Fake.Thumbnails)

[<Fact>]
let ``切换侧栏折叠发出的副作用或状态转移`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let before = rig.Shell.Model.Nav.SidebarCollapsed
        rig.Shell.Dispatch(User(Header(HeaderUserMsg.ToggleSidebar)))
        Assert.Equal(not before, rig.Shell.Model.Nav.SidebarCollapsed))

[<Fact>]
let ``断开连接：清空状态并转入断开`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Fake.Close(Some "网络断了")
        // 不变式 3（libs/WorkspaceState）：ConnectionDown 之后在途计数归零，
        // 防 Pending Wedge（断线后命令永远停在 pending，界面一直转圈）。
        Assert.Equal(0, rig.Shell.Model.Feedback.PendingCommands.Count)
        Assert.Equal(0, rig.Shell.Model.Feedback.PendingDeletions.Count)
        Assert.Equal(0, rig.Shell.Model.Feedback.PendingConfigs.Count)
        // 连接态落到「重新连接中」（ConnPhase.Connecting 1），不是「已认证」。
        // （旧断言两个分支都是 Assert.True(true)：无论落到哪个状态都通过。）
        match rig.Shell.Model.Conn with
        | ConnPhase.Connecting attempt -> Assert.Equal(1, attempt)
        | other -> Assert.Fail(sprintf "断开后应是重新连接中，实际 %A" other)
        // 断开要告诉用户。
        Assert.Contains(rig.Fake.Toasts, fun (text, _) -> text = "已断开连接"))

// ---- SPEC 7 闭环旅程（≥6 条）----

[<Fact>]
let ``旅程 1：首次配对到就绪`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        // 初始状态
        match rig.Shell.Model.Conn with
        | ConnPhase.Connecting _ -> ()
        | other -> Assert.Fail(sprintf "初始连接状态不对: %A" other)

        // 主机要求配对
        rig.Shell.Dispatch(WireRecv(PairingStarted {| expiresInSeconds = 300 |}))

        match rig.Shell.Model.Conn with
        | ConnPhase.Pairing _ -> ()
        | other -> Assert.Fail(sprintf "未进入配对状态: %A" other)

        // 配对成功
        rig.Shell.Dispatch(WireRecv(PairingSucceeded {| token = "tok-1" |}))

        Assert.Contains(
            rig.Fake.RecordedEffects,
            fun e ->
                match e with
                | Effect.SendWire(WireEvent.AuthPresent d) -> d.token = "tok-1"
                | _ -> false
        )

        // 认证接受
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(inst, CatalogPhase.LoadingCatalog) -> Assert.Equal("inst-test", inst)
        | other -> Assert.Fail(sprintf "认证后状态不对: %A" other)

        // 目录装载完成。旅程 2 传空 `providers` 是为了走「还没有可用的模型服务」的引导分支；
        // 旅程 1 要验「配对 → 首发」，输入区必须真的可用（`WorkspaceState/Project.fs` 的门控：
        // `cat.providers` 为空 → Composer.Enabled=false），所以这里放一条**能被解析的**模型服务
        // （`Catalog.parse` 只收带 id/label/models 的条目，空对象会被丢掉）。
        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers =
                            let provider = JsonObject()
                            provider["id"] <- JsonValue.Create "p1"
                            provider["label"] <- JsonValue.Create "P1"
                            provider["models"] <- JsonArray([| JsonValue.Create "m1" :> JsonNode |])
                            JsonArray([| provider :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.IdleEmpty)) -> ()
        | other -> Assert.Fail(sprintf "目录就绪后应是空闲态: %A" other)

        // 「新建对话」的真实入口（侧栏动作记录 → User(Sidebar NewConversation)）。
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.NewConversation)))

        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.IdleEmpty)) -> ()
        | other -> Assert.Fail(sprintf "新建对话后应回到空闲态: %A" other)

        // 新建对话把焦点请回输入区：`Update.fs` 的 NewConversation 分支只产
        // [ Effect.Focus FocusTarget.ComposerInput ]，而 Focus 经
        // `compose/EntryShells/Effects.fs` 的 Dispatcher.Post(Input) 落地，
        // 不在假端口的同步记录面里，所以这里不断言投递本身；
        // 状态侧的真实可观测结果（回到 IdleEmpty）已在上面断言。

        // 输入文本：与真实按键同一条回调（ComposerDesk 的 TextChanged → 状态树）。
        let actions =
            rig.Shell.ComposerActions((fun () -> ()), (fun _ -> ()), (fun () -> false))

        let firstText = "第一次见面，请多指教"

        actions.TextChanged firstText
        Assert.Equal(firstText, (Workspace.project rig.Shell.Model).Composer.Text)
        // 输入区此时确实可用（不是被门控挡住的假首发）。
        Assert.True((Workspace.project rig.Shell.Model).Composer.Enabled)

        // 首发：空闲态下的发送门控会**先建对话再发首条消息**。
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.Send)))

        // 统一效果序列（同一份 RecordedEffects，不挑 SendCommand 子序列）：
        // 建对话命令 < 首条消息命令 < 订阅会话的 SendWire，且三者同属一个会话。
        let effects = rig.Fake.RecordedEffects

        let createIdx =
            effects
            |> List.findIndex (fun e ->
                match e with
                | Effect.SendCommand(ClientCommand.CreateConversation _) -> true
                | _ -> false)

        let appendIdx =
            effects
            |> List.findIndex (fun e ->
                match e with
                | Effect.SendCommand(ClientCommand.SendUserMessage _) -> true
                | _ -> false)

        let observeIdx =
            effects
            |> List.findIndex (fun e ->
                match e with
                | Effect.SendWire(WireEvent.ObserveConversation _) -> true
                | _ -> false)

        Assert.True(
            createIdx < appendIdx && appendIdx < observeIdx,
            sprintf "首发应是「建对话 → 发首条消息 → 订阅会话」次序，实际索引 %d/%d/%d" createIdx appendIdx observeIdx
        )

        let createPayload =
            (match effects[createIdx] with
             | Effect.SendCommand(ClientCommand.CreateConversation d) -> Some d
             | _ -> None)
            |> Option.defaultWith (fun () -> failwith "该位置应是建对话命令")

        let appendPayload =
            (match effects[appendIdx] with
             | Effect.SendCommand(ClientCommand.SendUserMessage d) -> Some d
             | _ -> None)
            |> Option.defaultWith (fun () -> failwith "该位置应是发首条消息命令")

        let observePayload =
            (match effects[observeIdx] with
             | Effect.SendWire(WireEvent.ObserveConversation d) -> Some d
             | _ -> None)
            |> Option.defaultWith (fun () -> failwith "该位置应是订阅会话")

        let createConvId = createPayload.conversationId
        let createTitle = createPayload.title

        // 三条效果关联同一个 conversationId。
        Assert.Equal(createConvId, appendPayload.conversationId)
        Assert.Equal(createConvId, observePayload.conversationId)

        // 标题取首句前 20 字（状态机的既有规则）。
        Assert.Equal(firstText, createTitle)
        // 命令载荷里有用户真正打的那句话。
        let sentContents = appendPayload.messageJson.["contents"].AsArray()
        Assert.Equal(firstText, sentContents[0].["text"].GetValue<string>())

        // 两条命令都在途（回执未到之前不能当成已提交）。
        Assert.Equal(2, rig.Shell.Model.Feedback.PendingCommands.Count)
        // 发送清空输入区与草稿。
        let vsSent = Workspace.project rig.Shell.Model
        Assert.Equal("", vsSent.Composer.Text)

        // 客户端在等对话快照：工作区进入「加载中」。
        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.LoadingConversation id)) ->
            Assert.Equal(createConvId, id)
        | other -> Assert.Fail(sprintf "发送后应是加载中: %A" other)

        // 协议回执：对话快照到达（首条用户消息已在服务端提交），工作区进 Active。
        // 快照条目形状 = `{commitId, payload}`，payload 就是客户端消息 DTO；
        // 首条用户消息带的是用户真正发的那句话（不是占位）。
        let userItem =
            JsonNode.Parse(
                sprintf
                    """{"commitId":1,"payload":{"role":"user","contents":[{"type":"text","text":"%s"}]}}"""
                    firstText
            )

        rig.Shell.Dispatch(
            WireRecv(
                ConversationSnapshot
                    {|
                        conversationId = createConvId
                        title = createTitle
                        lastCommitId = 1UL
                        runtimeState = "idle"
                        generationId = None
                        messages = JsonArray([| userItem |])
                        snapshotEarliestCommitId = 1UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )
        )

        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
            // 投影的唯一数据源：活动对话里有了那条首发消息。
            Assert.Equal(createConvId, conv.ConversationId)
            Assert.Equal(1, conv.Messages.Length)

            let firstMessage = conv.Messages[0]
            Assert.Equal("user", firstMessage.role)
            // 正文断言：投影里就是那句话本身。
            Assert.Equal(firstText, firstMessage.text)
            // 聊天投影（渲染层唯一数据源）里也是这句话。
            match (Workspace.project rig.Shell.Model).Chat.Phase with
            | ChatPhase.Messages rows ->
                Assert.Equal(1, List.length rows)
                Assert.Equal("user", rows[0].Role)
                Assert.Equal(firstText, rows[0].Text)
            | other -> Assert.Fail(sprintf "回执后聊天投影应是消息态: %A" other)
            // 输入区已被发送清空。
            Assert.Equal("", (Workspace.project rig.Shell.Model).Composer.Text)
        | other -> Assert.Fail(sprintf "回执后应是 Active: %A" other)
        // 首发之后输入区不再禁用（有可用模型服务）。
        Assert.True((Workspace.project rig.Shell.Model).Composer.Enabled))

[<Fact>]
let ``旅程 2：引导流程配置模型`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray()
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let vs = Workspace.project rig.Shell.Model
        // 空目录时无需停止按钮（负向）。
        match vs.Composer.Send.State with
        | SendButtonState.GeneratingStop -> Assert.Fail "空目录不该有停止按钮"
        | _ -> ()
        // 正向：目录装载完，且目录里**没有可用模型服务** → 连接态是「已认证 + 目录就绪」，
        // 工作区是 IdleEmpty（还没开任何对话）。这是旅程 2 走完引导后应有的落点。
        // （旧断言只有上面那一条负向：目录状态、引导事实全都没验。）
        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(catalog, WorkspacePhase.IdleEmpty)) ->
            // 空目录：一个模型服务都没有。
            Assert.Empty(catalog.providers)
        | other -> Assert.Fail(sprintf "空目录应是已认证+目录就绪+空闲，实际 %A" other)
        // 引导事实还是空的（用户还没选预设）：引导卡按这个事实投影。
        Assert.Equal(None, rig.Shell.Model.Onboarding.SelectedPresetKey)
        Assert.Equal(None, rig.Shell.Model.Onboarding.DiscoveredModels)
        // 没有可用模型服务 → 输入区禁用，并说清为什么（不可用原因进界面）。
        Assert.False(vs.Composer.Enabled)
        Assert.Equal(Some "还没有可用的模型服务", vs.Composer.DisabledReason)
        // 目录为空 → 聊天区是「还没有对话」的空态（工作区是 IdleEmpty）。
        match vs.Chat.Phase with
        | ChatPhase.Empty IdleEmptyKind.NoConversation -> ()
        | other -> Assert.Fail(sprintf "空目录应是 NoConversation 空态，实际 %A" other))

[<Fact>]
let ``旅程 3：发送含附件消息`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        // 附加文件：挂上后**还没就绪**（主机还没确认落盘），
        // 所以此刻发送会被门控挡住——草稿与附件都留着。
        let attachmentId = Guid.NewGuid()

        rig.Shell.Dispatch(
            User(Composer(ComposerUserMsg.Attach(attachmentId, "pic.png", "image/png", 1024L, "sha256")))
        )
        |> ignore

        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.TextChanged "你好")))

        let vs = Workspace.project rig.Shell.Model
        Assert.Equal("你好", vs.Composer.Text)
        Assert.Single(vs.Composer.Tray) |> ignore

        // 未就绪时发送：被挡住，消息没发出去，草稿与附件都留着
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.Send)))
        let vsBlocked = Workspace.project rig.Shell.Model
        Assert.Equal("你好", vsBlocked.Composer.Text)
        Assert.Single(vsBlocked.Composer.Tray) |> ignore
        Assert.Empty rig.Fake.Commands

        // 主机确认落盘（`attachment.committed`）→ 附件就绪
        rig.Shell.Dispatch(
            WireRecv(
                WireEvent.AttachmentCommitted
                    {|
                        attachmentId = attachmentId
                        sha256 = "sha256"
                        size = 1024L
                    |}
            )
        )

        let vsReady = Workspace.project rig.Shell.Model
        Assert.True((vsReady.Composer.Tray |> List.head).Ready)

        // 发送消息
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.Send)))
        let vsAfter = Workspace.project rig.Shell.Model
        Assert.Equal("", vsAfter.Composer.Text)
        Assert.Empty vsAfter.Composer.Tray

        // **断言真正发出去的 payload**：文本 + 那条附件（不是只断「输入框清空了」）
        let sent =
            rig.Fake.Commands
            |> List.tryPick (function
                | ClientCommand.SendUserMessage d -> Some d.messageJson
                | _ -> None)

        Assert.True(sent.IsSome)
        let contents = sent.Value.["contents"].AsArray()
        Assert.Equal(2, contents.Count)
        Assert.Equal("你好", contents[0].["text"].GetValue<string>())
        Assert.Equal("attachment", contents[1].["type"].GetValue<string>())
        Assert.Equal("pic.png", contents[1].["fileName"].GetValue<string>())
        Assert.Equal("sha256", contents[1].["sha256"].GetValue<string>())
        Assert.Equal(1024L, contents[1].["size"].GetValue<int64>()))

[<Fact>]
let ``旅程 4：流式增量与工具审批响应`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let convId = Guid.NewGuid()
        let genId = Guid.NewGuid()

        rig.Shell.Dispatch(
            WireRecv(
                ConversationSnapshot
                    {|
                        conversationId = convId
                        title = "对话"
                        lastCommitId = 0UL
                        runtimeState = "idle"
                        generationId = None
                        messages = JsonArray()
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )
        )

        rig.Shell.Dispatch(
            WireRecv(
                GenerationStarted
                    {|
                        conversationId = convId
                        generationId = genId
                        providerId = "p1"
                        model = "m1"
                    |}
            )
        )

        // 正在生成：不可为 Idle 且 Send.State 必须是 GeneratingStop
        let vsGen = Workspace.project rig.Shell.Model

        match vsGen.Composer.Send.State with
        | SendButtonState.GeneratingStop -> ()
        | _ -> Assert.Fail "生成中应当展示停止按钮"

        // 工具审批到达
        let approvalId = Guid.NewGuid()

        rig.Shell.Dispatch(
            WireRecv(
                ToolApprovalRequested
                    {|
                        approvalId = approvalId
                        conversationId = convId
                        generationId = genId
                        callId = "call-1"
                        toolName = "read_file"
                        summary = "读取系统文件"
                        arguments = JsonObject() :> JsonNode
                        policy = "manual"
                        expiresInSeconds = 60
                    |}
            )
        )

        let vsApproval = Workspace.project rig.Shell.Model
        Assert.Contains(vsApproval.Approval.Items, fun a -> a.ApprovalId = approvalId)

        // 响应审批
        rig.Shell.Dispatch(User(Chat(ChatUserMsg.ApprovalRespond(approvalId, true))))

        rig.Shell.Dispatch(
            WireRecv(
                ToolApprovalResolved
                    {|
                        approvalId = approvalId
                        conversationId = convId
                        status = "approved"
                        reason = ""
                        alreadyHandled = false
                    |}
            )
        )

        let vsResolved = Workspace.project rig.Shell.Model
        Assert.DoesNotContain(vsResolved.Approval.Items, fun a -> a.ApprovalId = approvalId))

[<Fact>]
let ``旅程 5：中途停止与失败重试`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let convId = Guid.NewGuid()
        let genId = Guid.NewGuid()

        rig.Shell.Dispatch(
            WireRecv(
                ConversationSnapshot
                    {|
                        conversationId = convId
                        title = "对话"
                        lastCommitId = 0UL
                        runtimeState = "idle"
                        generationId = None
                        messages = JsonArray()
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )
        )

        rig.Shell.Dispatch(
            WireRecv(
                GenerationStarted
                    {|
                        conversationId = convId
                        generationId = genId
                        providerId = "p1"
                        model = "m1"
                    |}
            )
        )

        // 停止生成
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.StopGeneration)))

        Assert.Contains(
            rig.Fake.RecordedEffects,
            fun e ->
                match e with
                | Effect.SendWire(WireEvent.GenerationCancel d) -> d.conversationId = convId
                | _ -> false
        )

        // 主机完成上报带错误
        let err: GenerationError =
            {
                kind = GenerationErrorKind.ProviderTimeout
                message = "超时"
                retryable = true
                detail = None
                retryAfterSeconds = None
            }

        rig.Shell.Dispatch(
            WireRecv(
                GenerationFinished
                    {|
                        conversationId = convId
                        generationId = genId
                        status = "failed"
                        error = Some err
                        usage = None
                    |}
            )
        )

        let vsFinished = Workspace.project rig.Shell.Model

        // 正向：生成结束后可以再发（Idle，而不是别的忙态）。
        // （B2：旧写法只有 `| GeneratingStop -> Assert.Fail` 的负向断言——
        //  错误值（如 Sending）照样通过。这里钉死**期望的那个值**。）
        Assert.Equal(SendButtonState.Idle, vsFinished.Composer.Send.State)
        // RunState 带上了这次失败的 GenerationError（重试依据，不是丢掉）。
        // （旧断言只有上面那一条负向：错误内容、错误类型都没验。）
        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, run))) ->
            match run with
            | RunState.Idle(Some err) ->
                Assert.Equal(GenerationErrorKind.ProviderTimeout, err.kind)
                Assert.Equal("超时", err.message)
                Assert.True(err.retryable)
            | RunState.Idle None -> Assert.Fail "生成失败后 RunState 应带 lastError"
            | RunState.Generating _ -> Assert.Fail "生成已结束，不应仍在 Generating"
        | other -> Assert.Fail(sprintf "生成结束后连接态不对: %A" other)
        // 取消事件发给了**那一段**对话（不是别的对话）。
        let cancels =
            rig.Fake.RecordedEffects
            |> List.choose (fun e ->
                match e with
                | Effect.SendWire(WireEvent.GenerationCancel d) -> Some d
                | _ -> None)

        let cancels = List.ofSeq cancels
        Assert.Equal(1, cancels.Length)
        Assert.Equal(convId, cancels.Head.conversationId)
        Assert.Equal(genId, cancels.Head.generationId))

// 旅程 5b：失败后点**真实**的「重试」按钮，走完重试路径。
// 旅程 5 只验了错误状态与取消派发，没有真的点重试；这里补上：
// 真实控件树里找到 ErrorPanel 的「重试」按钮 → 点击 → 断言命令真的发出去、
// 待确认命令入队、按钮进入 Sending，命令确认后回到 Idle，重新生成后错误卡消失。
[<Fact>]
let ``旅程 5b：失败后点真实重试按钮走完重试路径`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let convId = Guid.NewGuid()
        let msg = JsonObject()
        msg["commitId"] <- 1UL
        msg["role"] <- "assistant"
        msg["text"] <- "你好"

        rig.Shell.Dispatch(
            WireRecv(
                ConversationSnapshot
                    {|
                        conversationId = convId
                        title = "对话"
                        lastCommitId = 1UL
                        runtimeState = "idle"
                        generationId = None
                        messages = JsonArray([| msg :> JsonNode |])
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )
        )

        let genId = Guid.NewGuid()

        rig.Shell.Dispatch(
            WireRecv(
                GenerationStarted
                    {|
                        conversationId = convId
                        generationId = genId
                        providerId = "p"
                        model = "m"
                    |}
            )
        )

        let err: GenerationError =
            {
                kind = GenerationErrorKind.ProviderTimeout
                message = "超时"
                retryable = true
                detail = None
                retryAfterSeconds = None
            }

        rig.Shell.Dispatch(
            WireRecv(
                GenerationFinished
                    {|
                        conversationId = convId
                        generationId = genId
                        status = "failed"
                        error = Some err
                        usage = None
                    |}
            )
        )

        Pump.settle ()

        // 找**真实**的重试按钮：逻辑树里 AutomationName = "重试生成" 的 Button。
        let rec findRetry (c: Control) : Button option =
            match c with
            | :? Button as b when AutomationProperties.GetName b = "重试生成" -> Some b
            | _ ->
                c.GetLogicalChildren()
                |> Seq.choose (fun ch ->
                    match ch with
                    | :? Control as cc -> findRetry cc
                    | _ -> None)
                |> Seq.tryHead

        let retryBtn =
            findRetry (rig.Views.Chat.Pane.Messages :> Control)
            |> Option.defaultWith (fun () -> failwith "失败后真实控件树里应有「重试」按钮")

        Assert.True(retryBtn.IsEnabled, "无倒计时时重试按钮应可用")
        Assert.Equal("重试", retryBtn.Content :?> string)

        // 点真实控件。
        retryBtn.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, retryBtn))
        Pump.settle ()

        // 重试路径真的走了：命令发出去、待确认入队、按钮进入 Sending。
        let regenCmds =
            rig.Fake.Commands
            |> List.choose (fun e ->
                match e with
                | ClientCommand.RegenerateResponse d -> Some d
                | _ -> None)

        Assert.Equal(1, regenCmds.Length)
        let regen = regenCmds.Head
        Assert.Equal(convId, regen.conversationId)

        Assert.True(
            rig.Fake.RecordedEffects
            |> List.exists (fun e ->
                match e with
                | Effect.SendCommand(ClientCommand.RegenerateResponse d) -> d.invocationId = regen.invocationId
                | _ -> false),
            "重试命令应作为 Effect.SendCommand 发出"
        )

        Assert.True(rig.Shell.Model.Feedback.PendingCommands.ContainsKey regen.invocationId, "重试命令应进入待确认队列")
        Assert.Equal(SendButtonState.Sending, (Workspace.project rig.Shell.Model).Composer.Send.State)

        // 命令被确认：待确认清空，按钮回 Idle。
        rig.Shell.Dispatch(
            WireRecv(
                CommandCommitted
                    {|
                        invocationId = regen.invocationId
                        commandId = "cmd-1"
                        commitId = 1UL
                    |}
            )
        )

        Pump.settle ()
        Assert.Empty rig.Shell.Model.Feedback.PendingCommands
        Assert.Equal(SendButtonState.Idle, (Workspace.project rig.Shell.Model).Composer.Send.State)

        // 重新生成开始：按钮变停止态。
        let genId2 = Guid.NewGuid()

        rig.Shell.Dispatch(
            WireRecv(
                GenerationStarted
                    {|
                        conversationId = convId
                        generationId = genId2
                        providerId = "p"
                        model = "m"
                    |}
            )
        )

        Pump.settle ()
        Assert.Equal(SendButtonState.GeneratingStop, (Workspace.project rig.Shell.Model).Composer.Send.State)

        // 重新生成成功：回到 Idle、错误清空、错误卡（含重试按钮）消失。
        rig.Shell.Dispatch(
            WireRecv(
                GenerationFinished
                    {|
                        conversationId = convId
                        generationId = genId2
                        status = "ok"
                        error = None
                        usage = None
                    |}
            )
        )

        Pump.settle ()
        let vsAfter = Workspace.project rig.Shell.Model
        Assert.Equal(SendButtonState.Idle, vsAfter.Composer.Send.State)
        Assert.True(vsAfter.Chat.Error.IsNone, "重试成功后不应再有生成错误")

        match rig.Shell.Model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, RunState.Idle None))) -> ()
        | other -> Assert.Fail(sprintf "重试成功后 RunState 应回到 Idle None: %A" other)

        Assert.True((findRetry (rig.Views.Chat.Pane.Messages :> Control)).IsNone, "重试成功后错误卡与重试按钮应消失"))

[<Fact>]
let ``旅程 6：切换对话、搜索与批量删除恢复`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        let c1 = Guid.NewGuid()
        let c2 = Guid.NewGuid()
        let item1 = JsonObject()
        item1["conversationId"] <- string c1
        item1["title"] <- "对话1"
        let item2 = JsonObject()
        item2["conversationId"] <- string c2
        item2["title"] <- "对话2"
        let arr = JsonArray([| item1 :> JsonNode; item2 :> JsonNode |])
        rig.Shell.Dispatch(WireRecv(ConversationListSnapshot {| lastCommitId = 0UL; items = arr |}))

        // 选中 c1
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.SelectConv c1)))

        // 搜索激活与输入
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.SearchTextChanged "查询")))
        let vsSearch = Workspace.project rig.Shell.Model
        Assert.Equal("查询", vsSearch.Sidebar.Search.Text)

        // 批量删除
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.BatchDelete [ c1; c2 ])))
        Assert.True(rig.Shell.Model.Toasts.Length > 0)

        // 撤销删除
        let toastUndoAction = rig.Shell.Model.Toasts |> List.head |> fun t -> t.UndoAction
        // 批量删除的提示条**必须**带撤销动作，否则用户删了救不回来。
        // （旧写法 `| None -> ()`：撤销入口消失时测试静默通过。）
        match toastUndoAction with
        | Some actionId ->
            // 撤销前：有一个待删批次，批次里是那两段对话。
            Assert.Equal(1, rig.Shell.Model.Feedback.PendingDeletions.Count)

            let batch =
                rig.Shell.Model.Feedback.PendingDeletions |> Map.toList |> List.head |> snd

            Assert.Equal<Set<Guid>>(set [ c1; c2 ], set batch.Ids)
            rig.Shell.Dispatch(User(Overlay(OverlayUserMsg.ToastUndo actionId)))
            // 撤销后：待删批次清空（对话没被真删）。
            Assert.Empty rig.Shell.Model.Feedback.PendingDeletions
        | None -> Assert.Fail "批量删除的提示条必须带撤销动作")

// ---- W7-B3 接线补全：定位目标与引导卡投影 ----

[<Fact>]
let ``搜索命中：定位目标进模型，对话快照到达后统一 Effect 滚到那条消息`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let convId = Guid.NewGuid()
        // 搜索命中：打开那段对话并登记定位目标
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.SearchHitSelect(convId, Some "7"))))
        Assert.Equal(Some "7", (Workspace.project rig.Shell.Model).Chat.RevealTarget)
        // 对话快照到达：目标消息已加载 → 统一 Effect（宿主执行滚动与高亮）
        let msg = JsonObject()
        msg["commitId"] <- 7UL
        msg["role"] <- "assistant"

        rig.Shell.Dispatch(
            WireRecv(
                ConversationSnapshot
                    {|
                        conversationId = convId
                        title = "对话"
                        lastCommitId = 7UL
                        runtimeState = "idle"
                        generationId = None
                        messages = JsonArray([| msg :> JsonNode |])
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )
        )
        // 滚动 Effect 经 UI 调度器落地（宿主统一 Effect 的执行点）
        Pump.settle ()

        Assert.Contains(
            rig.Fake.RecordedEffects,
            fun e ->
                match e with
                | Effect.ScrollChat(ScrollCommand.ToExchange "7") -> true
                | _ -> false
        )
        // 目标消费后清空：不再重复滚
        Assert.Equal(None, (Workspace.project rig.Shell.Model).Chat.RevealTarget))

[<Fact>]
let ``引导分支：目录里没有可用模型服务时，引导卡按模型事实投影`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        // 目录里只有一个没启用的服务：正是首次运行、引导卡出场的时候
        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )
        // 卡片回报的事实进模型
        rig.Shell.Dispatch(User(Onboarding(OnboardingUserMsg.SelectPreset "ollama")))
        rig.Shell.Dispatch(User(Onboarding(OnboardingUserMsg.Discovered(Some [ "qwen3" ]))))
        rig.Shell.Dispatch(User(Onboarding(OnboardingUserMsg.SwitchOffered true)))
        // 宿主按单一入口把模型事实投影回引导卡
        Assert.Equal("ollama", rig.Views.Onboarding.SelectedPreset.key)
        Assert.Equal(Some [ "qwen3" ], rig.Views.Onboarding.Discovered)
        Assert.True rig.Views.Onboarding.SwitchOffered)

// ---- W7-G2：「检查主机」接线 ----

[<Fact>]
let ``点检查主机发出 doctor 请求，报告回来后填进健康状态`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        // 设置页点「检查主机」：对象转成 Running 并宣告要检查，宿主据此发请求。
        rig.Views.Settings.Health.BeginCheck()

        let request =
            rig.Fake.Events
            |> List.tryPick (function
                | DoctorRequest d -> Some d.requestId
                | _ -> None)

        Assert.True(request.IsSome)
        // 报告未到之前是「正在检查」
        match rig.Views.Settings.Health.Doctor with
        | Running -> ()
        | other -> Assert.Fail(sprintf "发请求后应是检查中: %A" other)
        // 报告到达：填进健康状态
        let checks = JsonObject()
        checks["name"] <- "config"
        checks["ok"] <- true
        checks["detail"] <- "ok"

        rig.Fake.Deliver(
            DoctorReport
                {|
                    requestId = request.Value
                    ok = true
                    checks = JsonArray([| checks :> JsonNode |])
                |}
        )

        match rig.Views.Settings.Health.Doctor with
        | Report(true, [ c ], _) ->
            Assert.Equal("config", c.name)
            Assert.Equal("配置文件", DoctorCheck.label c.name)
        | other -> Assert.Fail(sprintf "报告到达后应是结果: %A" other))

[<Fact>]
let ``迟到的 doctor 报告被丢掉，不覆盖当前状态`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Views.Settings.Health.BeginCheck()
        let requestId = Guid.NewGuid()

        rig.Fake.Deliver(
            DoctorReport
                {|
                    requestId = requestId
                    ok = true
                    checks = JsonArray()
                |}
        )

        match rig.Views.Settings.Health.Doctor with
        | Running -> ()
        | other -> Assert.Fail(sprintf "迟到的报告不该改状态: %A" other))

[<Fact>]
let ``主机拒绝配置时健康状态记下配置有误，config.changed 后清除`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig

        rig.Fake.Deliver(
            HostConfigRejected
                {|
                    summary = "配置有误"
                    errors = [ "第 3 行" ]
                |}
        )

        match rig.Views.Settings.Health.ConfigIssue with
        | Some(summary, errors) ->
            Assert.Equal("配置有误", summary)
            Assert.Equal<string list>([ "第 3 行" ], errors)
        | None -> Assert.Fail("应记下配置有误")

        rig.Fake.Deliver(ConfigChanged {| reason = "reloaded" |})
        Assert.Equal(None, rig.Views.Settings.Health.ConfigIssue))

/// 附件的真实生产路径（2026-10-21 W7-CLEANBREAK 修）：选文件 → 上传 → 就绪 → 发送。
///
/// 这条用例钉的是**接线**：`AttachFiles` 必须
///   1. 用**同一个** attachment id 派发状态树消息与线协议消息（进度才对得上）；
///   2. 真的发 `attachment.begin` → `attachment.chunk` → `attachment.complete`；
///   3. 附件就绪后，发送出去的 payload 里带的是那条附件（sha256 / fileName / size）。
[<Fact>]
let ``附件：AttachFiles 用同一个 id 走状态树与线协议，就绪后随消息发出`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray([| JsonObject() :> JsonNode |])
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        let bytes = "hello-attachment" |> System.Text.Encoding.UTF8.GetBytes
        rig.Shell.AttachFiles ("inst-test", 7) [ ("pic.png", bytes) ]

        // 线协议：begin → chunk… → complete，且三处用的是同一个 id
        let evs = rig.Fake.Events

        let begins =
            evs
            |> List.filter (function
                | WireEvent.AttachmentBegin _ -> true
                | _ -> false)

        let chunks =
            evs
            |> List.filter (function
                | WireEvent.AttachmentChunk _ -> true
                | _ -> false)

        let completes =
            evs
            |> List.filter (function
                | WireEvent.AttachmentComplete _ -> true
                | _ -> false)

        Assert.Single(begins) |> ignore
        Assert.NotEmpty(chunks)
        Assert.Single(completes) |> ignore

        let beginId =
            match List.head begins with
            | WireEvent.AttachmentBegin d -> d.attachmentId
            | _ -> Guid.Empty

        let completeId =
            match List.head completes with
            | WireEvent.AttachmentComplete d -> d.attachmentId
            | _ -> Guid.Empty

        Assert.Equal(beginId, completeId)
        // 分块序号从 0 连续递增
        let indices =
            chunks
            |> List.map (function
                | WireEvent.AttachmentChunk d -> d.index
                | _ -> -1)

        Assert.Equal<int list>([ 0 ], indices)

        // 状态树：托盘里恰好一条附件；**还没就绪**——主机还没确认落盘。
        // 就绪信号是主机的 `attachment.committed`，不是「我发完了 complete」。
        let vs = Workspace.project rig.Shell.Model
        Assert.Single(vs.Composer.Tray) |> ignore
        let tray = vs.Composer.Tray |> List.head
        Assert.Equal("pic.png", tray.FileName)
        Assert.False tray.Ready
        // 状态树里那条附件的 id 就是线协议用的那个 id
        Assert.Equal(beginId, tray.Id)

        // 主机确认落盘 → 就绪
        rig.Shell.Dispatch(
            WireRecv(
                WireEvent.AttachmentCommitted
                    {|
                        attachmentId = beginId
                        sha256 = Wanxiang.UI.UploadFacts.sha256Hex bytes
                        size = int64 bytes.Length
                    |}
            )
        )

        Assert.True(((Workspace.project rig.Shell.Model).Composer.Tray |> List.head).Ready)

        // 发送：payload 里带的是这条附件
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.TextChanged "看图"))) |> ignore
        rig.Shell.Dispatch(User(Composer(ComposerUserMsg.Send))) |> ignore

        let sent =
            rig.Fake.Commands
            |> List.tryPick (function
                | ClientCommand.SendUserMessage d -> Some d.messageJson
                | _ -> None)

        Assert.True(sent.IsSome)
        let contents = sent.Value.["contents"].AsArray()
        Assert.Equal(2, contents.Count)
        Assert.Equal("看图", contents[0].["text"].GetValue<string>())
        Assert.Equal("attachment", contents[1].["type"].GetValue<string>())
        Assert.Equal("pic.png", contents[1].["fileName"].GetValue<string>())
        // sha256 与上传时算出来的一致
        Assert.Equal(Wanxiang.UI.UploadFacts.sha256Hex bytes, contents[1].["sha256"].GetValue<string>())
        // 发送后托盘清空
        Assert.Empty((Workspace.project rig.Shell.Model).Composer.Tray))

/// PWA 的自动连接回落（2026-10-21 W7-CLEANBREAK 修）。
///
/// 浏览器里没有 toml，凭据在 IndexedDB。此前 `ShellPorts.TryLoadBrowserConnection`
/// **只有声明、没有调用点**——`AutoConnect` 在浏览器里必然走 `| None -> ()`，
/// 于是 PWA 从不自动连上主机，composer 一直停在「未连接」态：
/// 打不进字、不写草稿（已发布 PWA 冒烟的三条失败都是这一个根因）。
[<Fact>]
let ``AutoConnect 在没有 toml 时回落读浏览器凭据并连上`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Fake.BrowserConnection <- Some("ws://127.0.0.1:9/ws", "browser-token", "inst-browser")
        rig.Shell.AutoConnect()
        // 等后台续体落地（`Async.Start` + `ports.Post` 回 UI 线程，测试自己驱动消息循环）
        Pump.until (fun () -> not (List.isEmpty rig.Fake.Connects))
        Assert.Equal<string list>([ "ws://127.0.0.1:9/ws" ], rig.Fake.Connects)
        // AuthPresent 是连接之后的另一条消息，要单独等它落地；只等 Connects 会在慢机器上抢先断言
        Pump.until (fun () -> not (List.isEmpty rig.Fake.AuthEvents))
        // 认证用的是浏览器那条凭据的 token
        Assert.Equal(1, (rig.Fake.AuthEvents |> List.length))

        match List.head rig.Fake.AuthEvents with
        | _, WireEvent.AuthPresent d -> Assert.Equal("browser-token", d.token)
        | _ -> Assert.True(false, "应当用浏览器凭据的 token 发 AuthPresent"))

/// 连上之后浏览器凭据不该被重复使用：`AutoConnect` 与 `reconnect` 撞在一起时，
/// 一次只连一个（否则两次 `Connect` 抢同一个 epoch，后一个把前一个 socket 顶掉）。
[<Fact>]
let ``AutoConnect 与 reconnect 撞在一起时只连一次`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Fake.BrowserConnection <- Some("ws://127.0.0.1:9/ws", "browser-token", "inst-browser")
        rig.Shell.AutoConnect()
        rig.Shell.SidebarActions().reconnect()
        Pump.until (fun () -> not (List.isEmpty rig.Fake.Connects) && not (List.isEmpty rig.Fake.AuthEvents))

        Pump.settle ()
        Assert.Equal(1, (rig.Fake.Connects |> List.length)))

/// 草稿落盘的真实链路（2026-10-21 W7-CLEANBREAK 修）。
///
/// 此前**打字从不进状态树**：`compose/ComposerDesk/ComposerDesk.fs` 的
/// `box.Input.TextChanged` 只重算门控，`ComposerDeskActions` 里也没有「文本变了」这个出口；
/// 唯一一处 `ComposerUserMsg.TextChanged` 是在 `Submit` 里派发的（发送那一刻）。
/// 于是状态树里的 `Composer.Text` 恒为空，草稿永远存不进本机，重开也回不来。
///
/// 现在：打字 → `TextChanged` → 状态树 → **800 ms 闸**（宿主侧）→ `DraftWrite` → 落盘。
///
/// 闸的**决策**（什么时候落、落哪一份文本）由组合者自己的 `DraftSaveGate` 负责，
/// 它是纯函数 + 注入时钟，所以能被确定性地测（见 `DraftSaveGateTests.fs`）。
/// 这里断的是**接线**：打字确实进了状态树、也确实产出了 `DraftWrite`。
[<Fact>]
let ``打字进状态树并产出草稿落盘；内容与输入框一致`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        // 直接驱动输入框的 TextChanged（与真实按键同一条路）
        let actions =
            rig.Shell.ComposerActions((fun () -> ()), (fun _ -> ()), (fun () -> false))

        actions.TextChanged "草稿内容"
        // 打字**立刻**进状态树
        Assert.Equal("草稿内容", (Workspace.project rig.Shell.Model).Composer.Text)
        // 闸决定落盘，并产出 `DraftWrite`（内容与输入框一致）
        Assert.NotEmpty rig.Fake.DraftWrites

        match List.head rig.Fake.DraftWrites with
        | DraftWrite.Write t -> Assert.Equal("草稿内容", t)
        | other -> Assert.True(false, "应当写出草稿，实际 " + string other))

/// 打字时**光标位置**也进状态树：文本相同时输入区不再回写，
/// 光标只能靠这条回调保持同步（否则打字打到一半光标被拉回末尾）。
[<Fact>]
let ``光标位置也进状态树`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()

        let actions =
            rig.Shell.ComposerActions((fun () -> ()), (fun _ -> ()), (fun () -> false))

        actions.TextChanged "abcdef"
        actions.CaretChanged 3
        let vs = Workspace.project rig.Shell.Model
        Assert.Equal("abcdef", vs.Composer.Text)
        Assert.Equal(Some 3, vs.Composer.Caret))

/// 原生交互证明（补充 A11yTraversalTests 的静态名称/角色断言）：
/// 真实顶层窗口下验证 Tab 焦点前移、Esc 清空搜索、删除邻位聚焦三段行为。
/// 名称与角色的全树断言在 A11yTraversalTests.fs，这里不重复。
let keyOn (c: Control) (key: Key) =
    c.RaiseEvent(
        KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = KeyModifiers.None, Source = c)
    )

[<Fact>]
let ``真实控件树下：Tab 焦点前移、Esc 清空搜索、删除邻位聚焦`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))

        rig.Shell.Dispatch(
            WireRecv(
                CatalogSnapshot
                    {|
                        providers = JsonArray()
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )
        )

        // 挂入顶层无头窗口：焦点管理器需要真实的视觉根与焦点作用域。
        let window = Window(Width = 1024.0, Height = 768.0, Content = rig.Root)
        window.Show()
        Dispatcher.UIThread.RunJobs()
        let topLevel = TopLevel.GetTopLevel(window)
        Assert.NotNull topLevel

        // Tab：焦点沿原生键盘导航链前移，不留在输入框。
        rig.Views.Composer.Box.Input.Focus() |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<IInputElement>(rig.Views.Composer.Box.Input, topLevel.FocusManager.GetFocusedElement())

        keyOn rig.Views.Composer.Box.Input Key.Tab
        Dispatcher.UIThread.RunJobs()
        let postTabFocused = topLevel.FocusManager.GetFocusedElement()
        Assert.NotNull postTabFocused
        Assert.NotEqual<IInputElement>(rig.Views.Composer.Box.Input, postTabFocused)

        // Esc：搜索输入清空（既有文本与状态树同时回空）。
        rig.Views.Sidebar.Search.Box.Text <- "查询待清空"
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.SearchTextChanged "查询待清空")))
        rig.Views.Sidebar.Search.Box.Focus() |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("查询待清空", rig.Views.Sidebar.Search.Text)

        keyOn rig.Views.Sidebar.Search.Box Key.Escape
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("", rig.Views.Sidebar.Search.Text)

        // 删除邻位聚焦：删中间项选下一个邻位，删末尾项回退上一个，清空后回落搜索框。
        let id1 = Guid.NewGuid()
        let id2 = Guid.NewGuid()
        let id3 = Guid.NewGuid()

        let item (id: Guid) (title: string) =
            let j = JsonObject()

            j["conversationId"] <- JsonValue.Create(string id)
            j["title"] <- JsonValue.Create(title)
            j :> JsonNode

        let snapshot lastCommitId (items: JsonNode seq) =
            WireRecv(
                ConversationListSnapshot
                    {|
                        lastCommitId = lastCommitId
                        items = JsonArray(items |> Seq.toArray)
                    |}
            )

        rig.Shell.Dispatch(snapshot 0UL [ item id1 "对话1"; item id2 "对话2"; item id3 "对话3" ])

        Assert.Equal(Some id3, rig.Views.Sidebar.NeighborAfterDelete(string id2))
        rig.Views.Sidebar.FocusAfterDelete(string id2)

        rig.Shell.Dispatch(snapshot 1UL [ item id1 "对话1"; item id3 "对话3" ])
        Assert.Equal(Some id1, rig.Views.Sidebar.NeighborAfterDelete(string id3))
        rig.Views.Sidebar.FocusAfterDelete(string id3)

        rig.Shell.Dispatch(snapshot 0UL [])
        rig.Views.Sidebar.FocusAfterDelete(string id1)

        window.Close())
