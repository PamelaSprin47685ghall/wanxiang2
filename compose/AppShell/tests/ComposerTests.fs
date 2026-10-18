module Wanxiang2.AppShell.Tests.ComposerTests

// 组合者的可观察后果：旧 AppShell 的行为在新分体里做了什么（发了哪条事件/命令、说了哪句话、
// 偏好扇出到哪些端口、能力表闸门拦在哪）。全部经假 ShellPorts 观察，计时器与 UI 投递是确定性的。

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI
open Wanxiang2.AppShell.Tests

let private toasts (rig: Rig) = rig.Fake.Toasts |> List.map fst

/// 只取附件上传事件（认证等其它事件已经发过，不该混进来）。
let private attachmentEvents (rig: Rig) =
    rig.Fake.Events
    |> List.filter (function
        | AttachmentBegin _ | AttachmentChunk _ | AttachmentComplete _ -> true
        | _ -> false)

/// 连上主机（AuthAccepted）+ 可选能力表，参数取自旧 ApplyHostEvent 的同名状态。
let private authenticate (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "inst-1" |})

let private capabilities (rig: Rig) (features: string list) =
    rig.Fake.Deliver(HostCapabilities {| hostVersion = "1.0"; features = features |})

[<Fact>]
let ``未连接时导入导出只说连接提示，不开对话框`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.ImportConversation()
        rig.Shell.ExportConversation rig.Summary
        Assert.Equal<string list>(
            [ "连接主机后才能导入对话。"; "连接主机后才能读取完整历史。" ],
            toasts rig)
        Assert.Empty rig.ImportDesks
        Assert.Empty rig.ExportDesks)

[<Fact>]
let ``主机能力表声明不含导入导出时 RequireFeature 拦下并逐字提示`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        capabilities rig [ "presets" ]
        rig.Shell.ImportConversation()
        rig.Shell.ExportConversation rig.Summary
        rig.Shell.RequestOlderHistory()
        Assert.Equal<string list>(
            [ "主机版本较旧，不支持导入对话。请更新主机后再试。"
              "主机版本较旧，不支持导出对话。请更新主机后再试。"
              "主机版本较旧，不支持加载更早的消息。请更新主机后再试。" ],
            toasts rig)
        Assert.Empty rig.ImportDesks
        Assert.Empty rig.ExportDesks)

[<Fact>]
let ``能力表未声明（旧主机）时照常放行，靠请求超时兜底`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        // 没有 HostCapabilities：hostFeatures = None，一律视为可用（不会出现「不支持」提示）
        rig.Shell.ImportConversation()
        Assert.Single rig.ImportDesks |> ignore
        Assert.DoesNotContain(toasts rig, fun text -> text.Contains "主机版本较旧"))

[<Fact>]
let ``附件归属过期时只说一次断开提示，不发任何协议事件`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Fake.Generation <- 8
        let before = rig.Fake.Events.Length
        rig.Shell.AttachFiles ("inst-1", 7) [ "a.png", [| 1uy; 2uy; 3uy |] ]
        Assert.Equal<string list>([ UploadFacts.disconnectedText ], toasts rig)
        Assert.Equal(before, rig.Fake.Events.Length)
        Assert.Empty rig.Fake.Thumbnails)

[<Fact>]
let ``附件上传按开始-分块-完成发事件，图片另走缩略图端口`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let owner = rig.Shell.UploadOwner()
        Assert.Equal(("inst-1", 7), owner)
        let bytes = [| for i in 1..3 -> byte i |]
        rig.Shell.AttachFiles owner [ "a.png", bytes ]
        let shas = UploadFacts.sha256Hex bytes
        match attachmentEvents rig with
        | [ AttachmentBegin begin_
            AttachmentChunk chunk
            AttachmentComplete complete ] ->
            Assert.Equal("a.png", begin_.fileName)
            Assert.Equal("image/png", begin_.mediaType)
            Assert.Equal(int64 bytes.Length, begin_.totalBytes)
            Assert.Equal(shas, begin_.sha256)
            Assert.Equal(0, chunk.index)
            Assert.Equal(Convert.ToBase64String bytes, chunk.dataBase64)
            Assert.Equal(shas, complete.sha256)
            Assert.Equal(begin_.attachmentId, chunk.attachmentId)
            Assert.Equal(begin_.attachmentId, complete.attachmentId)
        | other -> Assert.Fail(sprintf "事件序列不对：%A" other)
        match rig.Fake.Thumbnails with
        | [ (id, thumbBytes) ] ->
            Assert.Equal(bytes.Length, thumbBytes.Length)
            match attachmentEvents rig with
            | AttachmentBegin begin_ :: _ -> Assert.Equal(begin_.attachmentId, id)
            | _ -> ()
        | other -> Assert.Fail(sprintf "缩略图调用不对：%A" other))

[<Fact>]
let ``空文件与超大文件被校验挡下，不发上传事件`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let owner = rig.Shell.UploadOwner()
        let before = rig.Fake.Events.Length
        rig.Shell.AttachFiles owner [ "b.txt", Array.empty<byte> ]
        Assert.Equal<string list>([ "没有添加附件：文件是空的。请换一个文件。" ], toasts rig)
        Assert.Equal(before, rig.Fake.Events.Length))

[<Fact>]
let ``SavePrefs 扇出：主题、动效、折行、归档、回车与草稿开关各归各的端口`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let themes0 = rig.Fake.AppliedThemes.Length
        let wraps0 = rig.Fake.CodeWraps.Length
        let motions0 = rig.Fake.ReducedMotion.Length
        rig.Shell.SavePrefs
            { UiPrefsFile.defaults with
                theme = ThemePreference.AlwaysDark
                reduceMotion = true
                codeWrap = true
                showArchived = true
                enterSends = false
                persistDrafts = false
                sidebarWidth = 300.0 }
        // 主题落到 ApplyTheme 端口（AlwaysDark 与系统深浅无关，恒 true）
        Assert.Equal<bool list>([ true ], rig.Fake.AppliedThemes |> List.skip themes0)
        Assert.Equal<bool list>([ true ], rig.Fake.ReducedMotion |> List.skip motions0)
        Assert.Equal<bool list>([ true ], rig.Fake.CodeWraps |> List.skip wraps0)
        // 关闭保存：本机内容立刻删除
        Assert.Contains(DraftWrite.Delete, rig.Fake.DraftWrites)
        Assert.Equal(300.0, (List.last rig.Fake.SavedPrefs).sidebarWidth)
        Assert.Equal(ThemePreference.AlwaysDark, (List.last rig.Fake.SavedPrefs).theme))

[<Fact>]
let ``系统深色变化只在跟随系统时重应用主题`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Fake.SystemDark <- true
        let apply0 = rig.Fake.AppliedThemes.Length
        // 默认偏好是跟随系统：会重应用，且拿到端口的深色判定
        Assert.Equal(ThemePreference.FollowSystem, UiPrefsFile.defaults.theme)
        rig.Shell.OnSystemThemeChanged()
        Assert.Equal<bool list>([ true ], rig.Fake.AppliedThemes |> List.skip apply0)
        // 固定深色之后系统变化不再重应用
        rig.Shell.SavePrefs { UiPrefsFile.defaults with theme = ThemePreference.AlwaysDark }
        let apply1 = rig.Fake.AppliedThemes.Length
        rig.Shell.OnSystemThemeChanged()
        Assert.Equal(apply1, rig.Fake.AppliedThemes.Length))

[<Fact>]
let ``侧栏搜索接线：普通查询发 SearchQuery，消息链接走应用内打开`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        match rig.Views.Sidebar.SearchHandler with
        | None -> Assert.Fail "SearchHandler 未接"
        | Some handler ->
            handler "关键词" true 20
            match rig.Fake.Events |> List.filter (function SearchQuery _ -> true | _ -> false) with
            | [ SearchQuery q ] ->
                Assert.Equal("关键词", q.query)
                Assert.True q.includeArchived
                Assert.Equal(20, q.offset)
                Assert.Equal(SearchLimits.defaultPageSize, q.limit)
                Assert.NotEqual(Guid.Empty, q.queryId)
            | other -> Assert.Fail(sprintf "事件不对：%A" other)
            let conversationId = Guid "22222222-2222-2222-2222-222222222222"
            handler (sprintf "wanxiang://open?c=%s&m=42" (conversationId.ToString "D")) false 0
            // 消息链接不当搜索词：没有新的 SearchQuery
            Assert.Equal(1, rig.Fake.Events |> List.filter (function SearchQuery _ -> true | _ -> false) |> List.length))

[<Fact>]
let ``命令回执在手：提交后到 CommandCommitted 才 toast 成功文案`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Shell.SetPinned(rig.Summary, true)
        match rig.Fake.Commands with
        | [ SetConversationFlags flags ] ->
            Assert.True flags.pinned
            Assert.False flags.archived
            rig.Fake.Deliver(CommandCommitted {| invocationId = flags.invocationId; commandId = "c1"; commitId = 1UL |})
            Assert.Equal<string list>([ "已置顶「一段对话」" ], toasts rig)
        | other -> Assert.Fail(sprintf "命令不对：%A" other))

[<Fact>]
let ``命令没发出去：拒绝回执等待，之后到达的提交不再报成功`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        rig.Fake.SendsSucceed <- false
        rig.Shell.SetPinned(rig.Summary, true)
        let invocationId =
            match rig.Fake.Commands with
            | [ SetConversationFlags flags ] -> flags.invocationId
            | other -> failwithf "命令不对：%A" other
        // 旧 sendCommand 的失败路径在 UI 线程上跑（后台续体经 Post 回来）：等它落地
        Pump.until (fun () -> rig.Fake.Posts > 0)
        rig.Fake.Deliver(CommandCommitted {| invocationId = invocationId; commandId = "c1"; commitId = 1UL |})
        Assert.DoesNotContain(toasts rig, fun text -> text.Contains "已置顶"))

[<Fact>]
let ``打开设置记下焦点归宿，关闭时把焦点还回去`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.ShowSettings(Some "providers")
        Assert.True rig.Views.Settings.View.IsVisible
        Assert.False rig.Views.Workspace.Host.IsVisible
        Assert.Equal("providers", rig.Views.Settings.Frame.Current)
        Assert.Contains(CatalogRequest, rig.Fake.Events)
        Assert.Equal(0, rig.Fake.RestoreFocusCalls)
        rig.Shell.CloseSettings()
        Assert.False rig.Views.Settings.View.IsVisible
        Assert.True rig.Views.Workspace.Host.IsVisible
        Pump.until (fun () -> rig.Fake.RestoreFocusCalls > 0)
        Assert.Equal(1, rig.Fake.RestoreFocusCalls))

[<Fact>]
let ``会话设置与设备重命名都经配置回执端口，成功后提示一次`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let completed = ref None
        (rig.Shell.SettingsHooks()).UpdateGeneration (JsonObject()) (fun ok -> completed.Value <- Some ok)
        match rig.Fake.Events |> List.filter (function ConfigUpdateGeneration _ -> true | _ -> false) with
        | [ ConfigUpdateGeneration d ] ->
            rig.Fake.Deliver(ConfigApplied {| requestId = d.requestId; ok = true; errors = [] |})
            Pump.settle ()
            Assert.Equal(Some true, completed.Value)
            Assert.Contains(toasts rig, fun text -> text = "生成设置已保存")
        | other -> Assert.Fail(sprintf "事件不对：%A" other))

[<Fact>]
let ``切换侧栏折叠立刻落盘`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        Assert.False UiPrefsFile.defaults.sidebarCollapsed
        rig.Shell.ToggleSidebar()
        Assert.Equal<bool list>([ true ], rig.Fake.SavedPrefs |> List.map _.sidebarCollapsed))

[<Fact>]
let ``断开连接：清空审批、状态行说断开、并按退避调度重连`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig
        let before = rig.Fake.Toasts.Length
        rig.Fake.Close(Some "网络断了")
        let fresh = rig.Fake.Toasts |> List.skip before |> List.map fst
        Assert.Equal<string list>([ "连接已断开：网络断了" ], fresh)
        Assert.Equal("连接已断开", rig.Views.Sidebar.ConnectionText))
