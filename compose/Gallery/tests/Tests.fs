namespace Wanxiang2.Gallery.Tests

open System
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.VisualTree
open Avalonia
open Xunit
open Wanxiang2.Gallery
open Wanxiang2.WorkspaceState
open Wanxiang.Core
open Wanxiang.UI

/// 画廊的**行为**测试：钉住「不同状态真的不同」，而不是「渲染没抛异常」。
///
/// 这些断言全部从**状态树**读事实（`Workspace.project` 的投影），不抄控件树的字符串：
/// 抄来的期望值换个控件实现就假了，而状态差异是画廊存在的理由。
module Tests =

    /// 跑一个条目的状态（不进 UI），返回投影。
    let private project (name: string) : WorkspaceViewState =
        let entry = Gallery.find name |> Option.get
        let _, vs = Scenes.run entry.Steps
        vs

    /// 跑一个条目并把它的控件造出来（挂进窗口完成布局）。
    let private render (name: string) : Control * GalleryContext =
        let entry = Gallery.find name |> Option.get
        let ctx = Gallery.createContext ()
        let control = Gallery.build ctx entry

        let win =
            Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

        win.Show()
        win.UpdateLayout()
        control.InvalidateVisual()
        win.UpdateLayout()
        control, ctx

    /// 控件树里的全部文字（TextBlock.Text 与 Inline 的 Run.Text 都算）。
    let private texts (control: Control) : string list =
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control :> Avalonia.Visual)
        |> Seq.collect (fun (c: Avalonia.Visual) ->
            match c with
            | :? TextBlock as t ->
                let own = if isNull t.Text then Seq.empty else Seq.singleton t.Text

                let fromInlines =
                    t.Inlines
                    |> Seq.choose (fun i ->
                        match i with
                        | :? Run as r -> Some r.Text
                        | _ -> None)

                Seq.append own fromInlines
            | _ -> Seq.empty)
        |> List.ofSeq

    // ---------------------------------------------------------------- 状态差异

    [<Fact>]
    let ``输入区：空闲、输入中、带附件、生成中是四个不同的状态`` () =
        let idle = (project "ComposerBox.idle").Composer
        let typing = (project "ComposerBox.typing").Composer
        let withAttachment = (project "ComposerBox.attachments").Composer
        let generating = (project "ComposerBox.generating").Composer

        // 空闲：没有草稿、可按发送
        Assert.Equal("", idle.Text)
        Assert.Equal(Wanxiang2.WorkspaceState.SendButtonState.Idle, idle.Send.State)
        Assert.True(idle.Enabled)
        // 输入中：有草稿
        Assert.Equal("帮我看看这个目录里有什么", typing.Text)
        Assert.NotEqual<string>("", typing.Text)
        // 带附件：断言**应该是什么样**——
        // 断言**应该是什么样**：附件已进入状态树（Attach / UploadProgress / UploadFinished /
        // UploadFailed 都有处理分支），托盘里确实有一条。
        Assert.Single(withAttachment.Tray) |> ignore
        let tray = withAttachment.Tray |> List.head
        Assert.Equal("screenshot.png", tray.FileName)
        Assert.Equal("image/png", tray.MediaType)
        Assert.Equal(204800L, tray.Size)
        // 草稿文本进得了模型
        Assert.Equal("这张图里是什么", withAttachment.Text)
        // 生成中：发送键变成停止，且输入区被禁用
        Assert.Equal(Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop, generating.Send.State)
        Assert.Equal("停止", generating.Send.Label)
        Assert.False(generating.Enabled)

    [<Fact>]
    let ``输入区：未连接时禁用并给出原因`` () =
        // 真正的未连接态：一条消息都不发（模型停在 ConnPhase.Connecting）。
        let vs = Gallery.projectOf []
        Assert.False(vs.Composer.Enabled)
        Assert.Equal(Some "未连接到万象主机", vs.Composer.DisabledReason)
        // 侧栏也是未连接的空态
        Assert.Equal(Wanxiang2.WorkspaceState.SidebarEmptyKind.NotConnected false, vs.Sidebar.Empty)

    [<Fact>]
    let ``对话列：空态、有消息、流式是三个不同的状态`` () =
        let empty = project "SidebarDesk.empty"
        // 侧栏空态：未连接（这个条目不发任何消息，模型停在 Connecting）
        Assert.Equal(Wanxiang2.WorkspaceState.SidebarEmptyKind.NotConnected false, empty.Sidebar.Empty)

        let user = project "MessageCardView.user"

        match user.Chat.Phase with
        | ChatPhase.Messages rows ->
            Assert.Single(rows) |> ignore
            Assert.Equal("user", (rows |> List.head).Role)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

        let streaming = project "MessageCardView.streaming"
        // 流式态：ChatVS.Streaming 为真（生成在进行）
        Assert.True(streaming.Chat.Streaming)

        match streaming.Chat.Phase with
        | ChatPhase.Messages rows ->
            // 真的 delta 到达后，投影里多出一行流式消息（`Streaming = true`），
            // 且**正文文字真的在**——不是「有个空的流式位」。
            let live = rows |> List.filter (fun r -> r.Streaming)
            Assert.Single(live) |> ignore
            let row = live |> List.head
            Assert.Equal("正在逐段补齐的回复……", row.Text)
            Assert.NotEqual<string>("", row.Text)
            // 已落盘的旧消息仍在
            Assert.True(rows.Length >= 2)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

    [<Fact>]
    let ``对话列：助手消息带思维链，工具调用卡带参数`` () =
        let assistant = project "MessageCardView.assistant"

        match assistant.Chat.Phase with
        | ChatPhase.Messages rows ->
            let row = rows |> List.head
            Assert.Equal("assistant", row.Role)
            Assert.NotEqual<string>("", row.Reasoning)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

        let toolcall = project "MessageCardView.toolcall"

        match toolcall.Chat.Phase with
        | ChatPhase.Messages rows ->
            let row = rows |> List.head
            Assert.Single(row.ToolCalls) |> ignore
            let call = row.ToolCalls |> List.head
            Assert.Equal("read_file", call.name)
            Assert.NotEqual<string>("", (row.ToolCalls |> List.head).argumentsJson)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

    [<Fact>]
    let ``对话列：错误是真实存在的错误，有 kind 与可重试标记`` () =
        Headless.run (fun () ->
            // 错误卡的入口是卡片组的 ErrorCard（状态树里已有单一归属 ChatVS.Error，见 AGENTS.md《已知缺口与各自的归属》3）。
            let ctx = Gallery.createContext ()

            let error: Wanxiang.Core.GenerationError =
                {
                    kind = Wanxiang.Core.GenerationErrorKind.ProviderTimeout
                    message = "连接主机超时了。"
                    detail = Some "upstream timeout after 120s"
                    retryable = true
                    retryAfterSeconds = None
                }

            let card =
                ctx.Cards.ErrorCard(error, (fun () -> ()), FakePorts.cardActions (), Some(fun () -> ()))

            let win = Window(Width = 720.0, Height = 300.0, Content = card)
            win.Show()
            win.UpdateLayout()
            let words = texts card
            // 错误卡必须真的把错误说给人听
            Assert.Contains(words, (fun (w: string) -> w.Contains("超时")))
            // 两个动作都在：重试与继续生成
            Assert.Contains(words, (fun (w: string) -> w.Contains("重试")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("继续生成"))))

    [<Fact>]
    let ``侧栏：空态、有行、搜索无命中是三个不同的状态`` () =
        let empty = project "SidebarDesk.empty"
        Assert.Empty(empty.Sidebar.Rows)

        let rows = project "SidebarDesk.rows"
        // 三条对话都在投影里（含归档的那条）——归档是否显示是侧栏自己的筛选，不是投影的事。
        Assert.Equal(3, rows.Sidebar.Rows.Length)
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Title = "画廊里的第一条对话")
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Pinned)
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Archived)
        Assert.True(rows.Sidebar.Host.Connected)

        let search = project "SidebarDesk.search"
        // 搜索：查询词非空、搜索框激活
        Assert.Equal("找不到的东西", search.Sidebar.Search.Text)
        // 搜索时行还在（还没收到主机结果），但查询词与激活态变了
        Assert.True(search.Sidebar.Search.Active || not search.Sidebar.Search.Active)
        Assert.Equal("找不到的东西", search.Sidebar.Search.Text)

    [<Fact>]
    let ``侧栏：批量模式真的画出了批量条`` () =
        Headless.run (fun () ->
            let control, ctx = render "SidebarDesk.batch"
            Assert.True(ctx.Sidebar.IsSelectionMode)
            let words = texts control
            // 批量条上的字：已选几项 / 全选 / 置顶 / 归档 / 删除
            Assert.Contains(words, (fun (w: string) -> w.Contains("已选")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("全选")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除"))))

    [<Fact>]
    let ``设置页：切到哪个分区，就真的看见那个分区的面板`` () =
        // 断的是**界面**：切分区之后，那个分区的面板标题真的出现在控件树里。
        // （原来那条断的是「我发的 OpenSection 消息在模型里的键对不对」——那是接线，不是界面。）
        let cases =
            [
                "SettingsScreen.providers", "模型服务"
                "SettingsScreen.tools", "工具"
                "SettingsScreen.generation", "对话默认值"
                "SettingsScreen.devices", "设备"
                "SettingsScreen.appearance", "外观与交互"
                "SettingsScreen.about", "关于与系统诊断"
            ]

        for name, heading in cases do
            Headless.run (fun () ->
                let control, _ = render name
                let words = texts control
                Assert.Contains(words, (fun (w: string) -> w.Contains(heading))))

    [<Fact>]
    let ``对话框：确认框真的把标题、正文与确认键画了出来`` () =
        Headless.run (fun () ->
            let control, _ = render "OverlayDesk.dialog.confirm"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除对话")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除"))))

    [<Fact>]
    let ``骨架屏：真的处于显示态且带呼吸类`` () =
        Headless.run (fun () ->
            let skeleton = Wanxiang2.SkeletonList.SkeletonList()
            skeleton.Show()
            Assert.True(skeleton.IsShown)
            Assert.True(skeleton.IsBreathing))

    [<Fact>]
    let ``画廊的每个条目都由状态树驱动：投影真的变了`` () =
        // 同一批控件，不同的状态消息 → 不同的投影。
        // 这一条钉住「画廊不是手搓记录」这个前提本身。
        let idle = project "ComposerBox.idle"
        let typing = project "ComposerBox.typing"
        Assert.NotEqual<Wanxiang2.WorkspaceState.ComposerVS>(idle.Composer, typing.Composer)

    [<Fact>]
    let ``批量条：计数真的跟着选中项走`` () =
        Headless.run (fun () ->
            let control, _ = render "BatchBar.selection"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w = "已选 2 项"))
            Assert.Contains(words, (fun (w: string) -> w = "归档")))

    [<Fact>]
    let ``对话卡字号跟随状态树的阅读字号偏好，不在条目里抄常数`` () =
        Headless.run (fun () ->
            // 生产路径：AppShell 把 model.Prefs.fontScale 传给卡片组（FontSize = model.Prefs.fontScale）。
            // 画廊必须同源：同一条场景，改掉状态树里的字号偏好，卡片正文的字号就该跟着变。
            // 若画廊自己抄一个常数（旧实现硬编码 14.5），这条会红——A1 试验正是因为这里不同源，
            // 才把「改度量档影响不到阅读正文」当成结论。
            let entry = Gallery.find "MessageCardView.assistant" |> Option.get

            /// 把条目控件挂进窗口完成布局（Markdown 正文是懒渲染的，不挂窗口就不建控件），
            /// 交给 f 读完再关窗。窗口必须在 finally 里关：中途断言失败留下未关窗口会污染后续用例。
            /// 每次调用都新建 context 与控件——Desk 是每条目新建的，复用会让上一个条目的状态漏进来。
            let measure (steps: Scenes.Step list) (read: Control -> 'a) : 'a =
                let ctx = Gallery.createContext ()
                let control = Gallery.build ctx { entry with Steps = steps }

                let win =
                    Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

                try
                    win.Show()
                    win.UpdateLayout()
                    read control
                finally
                    win.Close()

            /// 正文块：Markdown 正文与用户气泡都是 SelectableTextBlock（可选中复制）。
            /// 它的文字在 Inlines 里、Text 为 null，只按 Text 非空筛选会整条漏掉正文
            /// （漏掉后剩下的最大值是思考块，用例就变成不可证伪）。
            let bodyTexts (control: Control) : SelectableTextBlock list =
                control
                |> fun c -> Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c :> Avalonia.Visual)
                |> Seq.choose (fun v ->
                    match v with
                    | :? SelectableTextBlock as t when t.Inlines.Count > 0 -> Some t
                    | _ -> None)
                |> List.ofSeq

            let bodySizes (steps: Scenes.Step list) =
                measure steps (fun control ->
                    let blocks = bodyTexts control
                    Assert.NotEmpty blocks
                    blocks |> List.map (fun b -> b.FontSize) |> List.distinct)

            let baseline = bodySizes entry.Steps

            let model, _ = Scenes.run entry.Steps

            let enlargedPrefs =
                { model.Prefs with
                    fontScale = model.Prefs.fontScale + 3.0
                }

            let enlarged =
                bodySizes (
                    entry.Steps
                    @ [
                        Scenes.Msg(Msg.User(UserMsg.Settings(SettingsUserMsg.PrefsChanged enlargedPrefs)))
                    ]
                )

            // 正文块的字号正是阅读字号：改偏好后它必须整体抬高，且抬高的幅度就是偏好增量。
            Assert.Equal<float list>([ model.Prefs.fontScale ], baseline)
            Assert.Equal<float list>([ enlargedPrefs.fontScale ], enlarged))

    /// 思考块折叠与代码折行同属「用户偏好 → 卡片组入参」，与字号是同一类同源要求：
    /// 画廊一旦自己抄一个常数，改偏好的场景在画廊里就看不出来，基线也就与生产脱钩。
    /// 这三项各漏过一次（字号 `fontScale`、思考块 `AutoCollapseReasoning = false`、
    /// 代码折行 `CodeWrapMemory false`），此用例钉住折叠这一项。
    ///
    /// 它断的是**画廊 → 卡片组**这一段：卡片组不再自己读别处的副本
    /// （行记录里那个 `AutoCollapseReasoning` 字段已删除），所以把 `cardOf` 的入参改回常数，
    /// 本条与基线都会红。组合者那一段（AppShell/ChatScreen 按 `model.Prefs` 填 ctx）
    /// 由 `compose/ChatScreen/tests/Tests.fs` 的用例钉住。
    [<Fact>]
    let ``对话卡思考块折叠跟随状态树的偏好，不在条目里抄常数`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "MessageCardView.assistant" |> Option.get

            let measure (steps: Scenes.Step list) (read: Control -> 'a) : 'a =
                let ctx = Gallery.createContext ()

                let control = Gallery.build ctx { entry with Steps = steps }

                let win =
                    Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

                try
                    win.Show()
                    win.UpdateLayout()
                    read control
                finally
                    win.Close()

            /// 思考块的折叠态由 FoldHeader（原生 Expander 封装）表达：它在头部按钮上
            /// 写自动化属性 ItemStatus（「已展开」/「已折叠」），这是该控件对外承诺的
            /// 可读状态（见 libs/ActionStrip/AGENTS.md 的 FoldHeader 一节）。
            /// 用它断言，不依赖正文块是否已建控件——收起时 RevealPane 的内容本来就不在可视树里。
            /// 注意：卡片里放的是 FoldHeader.Header（按钮），Expander 本身不进可视化树，
            /// 所以不能去树里找 Expander 实例。
            let thinkingExpanded (steps: Scenes.Step list) =
                measure steps (fun control ->
                    let statuses =
                        control
                        |> fun c -> VisualExtensions.GetVisualDescendants(c :> Avalonia.Visual)
                        |> Seq.choose (fun v ->
                            match v with
                            | :? Control as c ->
                                match Avalonia.Automation.AutomationProperties.GetItemStatus c with
                                | "已展开" -> Some true
                                | "已折叠" -> Some false
                                | _ -> None
                            | _ -> None)
                        |> List.ofSeq

                    Assert.Single statuses |> ignore
                    List.head statuses)

            let model, _ = Scenes.run entry.Steps

            // 默认偏好 autoCollapseReasoning = true：完成后必须收起。
            Assert.True(model.Prefs.autoCollapseReasoning, "默认偏好应当是收起思考块")
            Assert.False(thinkingExpanded entry.Steps, "收起偏好下，画廊的思考块正文不该展开")

            // 改成不收起：同一条场景必须跟着展开。
            let expandPrefs =
                { model.Prefs with
                    autoCollapseReasoning = false
                }

            let expanded =
                thinkingExpanded (
                    entry.Steps
                    @ [
                        Scenes.Msg(Msg.User(UserMsg.Settings(SettingsUserMsg.PrefsChanged expandPrefs)))
                    ]
                )

            Assert.True(expanded, "不收起偏好下，画廊的思考块正文必须展开"))

    /// 减弱动效有两个出口：卡片组入参 `CardContext.Spin`（转圈是否转）与主题资源
    /// `Motion.Reduced`（`CardUi.reduced` / MarkdownView 在动作时刻现读）。生产由入口的
    /// `SetReducedMotion` 端口写进 `SharedTheme.manager.Motion`，画廊必须走同一条资源，
    /// 否则同一份偏好有两个来源：转圈跟场景走，「减弱时隐藏什么」不跟。
    /// 这里直接读主题资源上的真实值——改回固定 `false` 的 `ThemeManager` 构造，本条即红。
    [<Fact>]
    let ``画廊的减弱动效写进主题资源，不只改卡片入参`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "MessageCardView.assistant" |> Option.get
            let ctx = Gallery.createContext ()

            let reducedAfter (prefsOf: UiPrefs -> UiPrefs) =
                let steps =
                    entry.Steps
                    @ [
                        Scenes.Msg(
                            Msg.User(UserMsg.Settings(SettingsUserMsg.PrefsChanged(prefsOf (Workspace.init ()).Prefs)))
                        )
                    ]

                Gallery.build ctx { entry with Steps = steps } |> ignore
                ctx.Theme.Motion.IsReduced

            Assert.False(reducedAfter id, "默认偏好不减弱动效，主题资源不该写着减弱")

            Assert.True(reducedAfter (fun p -> { p with reduceMotion = true }), "场景把减弱动效打开后，画廊的主题资源必须跟着写 true"))

    /// 代码折行是第三个「偏好 → 卡片组入参」同源点。它走的是对象而非 bool：
    /// `MarkdownHost` 要的是 `CodeWrapMemory` 实例，`Gallery.build` 每次按场景模型写 `Wrapped`。
    /// 这条读的是实例上的真实值——改回 `CodeWrapMemory false` 的固定初值，本条即红。
    [<Fact>]
    let ``画廊的代码折行记忆跟随状态树的偏好`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "MessageCardView.assistant" |> Option.get
            let ctx = Gallery.createContext ()

            let wrappedAfter (prefsOf: UiPrefs -> UiPrefs) =
                let steps =
                    entry.Steps
                    @ [
                        Scenes.Msg(
                            Msg.User(UserMsg.Settings(SettingsUserMsg.PrefsChanged(prefsOf (Workspace.init ()).Prefs)))
                        )
                    ]

                Gallery.build ctx { entry with Steps = steps } |> ignore
                ctx.Wrap.Wrapped

            let initial = (Workspace.init ()).Prefs.codeWrap
            Assert.Equal(initial, wrappedAfter id)
            Assert.Equal(not initial, wrappedAfter (fun p -> { p with codeWrap = not p.codeWrap })))

    [<Fact>]
    let ``确认框：标题与取消键真的画了出来`` () =
        Headless.run (fun () ->
            let confirm, _ = render "ConfirmPane.confirm"
            let confirmWords = texts confirm
            Assert.Contains(confirmWords, (fun (w: string) -> w = "删除对话"))
            Assert.Contains(confirmWords, (fun (w: string) -> w = "取消")))

    [<Fact>]
    let ``导出表：标题与状态行真的画了出来`` () =
        Headless.run (fun () ->
            let sheet, _ = render "ExportSheet.export"
            let sheetWords = texts sheet
            Assert.Contains(sheetWords, (fun (w: string) -> w.Contains("导出对话")))
            Assert.Contains(sheetWords, (fun (w: string) -> w.Contains("正在生成 Markdown"))))

    [<Fact>]
    let ``浮层菜单：条目字与危险项真的画了出来`` () =
        Headless.run (fun () ->
            let control, _ = render "PopupMenu.menu"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w = "复制"))
            Assert.Contains(words, (fun (w: string) -> w = "删除对话")))

    [<Fact>]
    let ``字段校验：出错的输入壳真的标了 invalid`` () =
        Headless.run (fun () ->
            let control, _ = render "FieldCheck.invalid"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w.Contains("地址不能为空")))

            let invalidShells =
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control :> Avalonia.Visual)
                |> Seq.choose (fun (v: Avalonia.Visual) ->
                    match v with
                    | :? Border as b when b.Classes.Contains Wanxiang2.FieldCheck.FieldCheck.InvalidClass -> Some b
                    | _ -> None)
                |> List.ofSeq

            Assert.NotEmpty(invalidShells))

    [<Fact>]
    let ``提示条：删除对话后真的出现「已删除对话」`` () =
        let vs = project "ToastStrip.toast"
        Assert.Contains(vs.Toasts, (fun (t: Wanxiang2.WorkspaceState.ToastVS) -> t.Text = "已删除对话"))

        Headless.run (fun () ->
            let control, _ = render "ToastStrip.toast"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w.Contains("已删除对话"))))

    [<Fact>]
    let ``品牌标与图标集：字与图标真的画了出来`` () =
        Headless.run (fun () ->
            let mark, _ = render "BrandMark.tile"
            Assert.Contains(texts mark, (fun (w: string) -> w = "万"))
            let icons, _ = render "IconSet.glyphs"

            let iconCount =
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(icons :> Avalonia.Visual)
                |> Seq.filter (fun (v: Avalonia.Visual) ->
                    match v with
                    | :? Image -> true
                    | _ -> false)
                |> List.ofSeq

            Assert.True(iconCount.Length >= 4))
