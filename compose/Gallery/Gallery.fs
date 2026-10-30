namespace Wanxiang2.Gallery

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.ComposerDesk
open Wanxiang2.ChatScreen
open Wanxiang2.MessageCardView
open Wanxiang2.SettingsScreen
open Wanxiang2.DialogsDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView

/// 画廊的构造上下文：主题、浮层根与假端口。
type GalleryContext =
    {
        Theme: ThemeManager
        Styles: ThemeStyles
        /// 浮层根（对话框与浮层挂这里）
        OverlayRoot: Grid
        /// 浮层桌（对话框、浮层、提示条都挂它；提示条带就是真实的 ToastStrip 控件）
        Overlay: OverlayDesk
        /// 侧栏
        Sidebar: SidebarDesk
        /// 输入区
        Composer: ComposerDesk
        /// 对话屏
        Chat: ChatScreen
        /// 设置页
        Settings: SettingsScreen
        /// 卡片组
        Cards: MessageCards
        /// 对话框
        Dialogs: DialogsDesk
        /// 审批面板
        Approval: ApprovalDesk
        /// 品牌标工厂（画廊用占位方块，与 AppShell 测试台同口径）
        BrandLogo: float -> Control
    }


/// 画廊：一个状态 = 一条可命名的可视条目。
///
/// 状态来源（见 AGENTS.md《状态来源》）：每个条目的视图状态都由
/// `Workspace.init ()` + `Workspace.update` 的消息序列跑出来，再经 `Workspace.project` 投影。
/// 没有任何一个条目手搓 WorkspaceViewState 记录——那样画廊会和真实界面漂移。
///
/// 需要宿主端口的条目（对话框、审批、模型切换）由 `FakePorts` 那一小组显式假端口驱动，
/// 假端口逐条列在 AGENTS.md《假端口》一节。
type Entry =
    {
        /// 稳定的名字（CLI 用）
        Name: string
        /// 这个状态属于哪个控件库
        Widget: string
        /// 一句话说明这个状态长什么样
        Note: string
        /// 造出这个状态的步骤
        Steps: Scenes.Step list
        /// 额外的界面构造（把投影喂给真实的控件库）
        Build: GalleryContext -> WorkspaceViewState -> Control
    }

module Gallery =

    // ---------------------------------------------------------------- 外观

    /// 浮层外观：**与 app/Desktop / app/Pwa 的 overlayLook 同一份口径**——
    /// 颜色全部取主题的活画笔（PaletteKey），尺寸取主题的活值（Metric / Timing）。
    /// 这里不硬编码白 / 灰 / 黑：画廊要能证明「主题换色后界面跟得上」，
    /// 硬编码颜色会把主题这一层从证明里抹掉。
    let overlayLook (theme: ThemeManager) : OverlayDeskMetrics =
        let brush k = theme.Brush k :> IBrush
        let metric m = theme.Metric m :> IObservable<float>

        {
            Layer =
                {
                    Scrim = brush Wanxiang2.ThemePalette.PaletteKey.Scrim
                    Surface = brush Wanxiang2.ThemePalette.PaletteKey.Surface
                    Edge = brush Wanxiang2.ThemePalette.PaletteKey.Border
                    DialogShadow = BoxShadows()
                    PopupShadow = BoxShadows()
                    PopupGap = 4.0
                    PopupMinWidth = 180.0
                    ScrimFade = TimeSpan.Zero
                    ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut()
                }
            Toasts =
                {
                    Surface = brush Wanxiang2.ThemePalette.PaletteKey.SurfaceRaised
                    Edge = brush Wanxiang2.ThemePalette.PaletteKey.Border
                    Text = brush Wanxiang2.ThemePalette.PaletteKey.Text
                    Accent = (fun _ -> brush Wanxiang2.ThemePalette.PaletteKey.Accent)
                    AccentOpacity = 0.6
                    Padding = Thickness(16.0, 12.0)
                    Shadow = BoxShadows()
                    LineHeight = 18.0
                    ColumnSpacing = 12.0
                    Spacing = 8.0
                    Margin = Thickness(24.0, 0.0, 24.0, 32.0)
                    MaxWidth = 480.0
                    BodyMaxHeight = 160.0
                    Fade = TimeSpan.Zero
                    FadeEasing = Avalonia.Animation.Easings.CubicEaseOut()
                }
            Menu =
                {
                    Text = brush Wanxiang2.ThemePalette.PaletteKey.Text
                    Muted = brush Wanxiang2.ThemePalette.PaletteKey.TextMuted
                    Faint = brush Wanxiang2.ThemePalette.PaletteKey.TextFaint
                    Danger = brush Wanxiang2.ThemePalette.PaletteKey.Danger
                    Accent = brush Wanxiang2.ThemePalette.PaletteKey.Accent
                    Check =
                        (fun b ->
                            Wanxiang2.IconSet.IconSet.image
                                Wanxiang2.IconSet.Glyph.Check
                                b
                                (metric Wanxiang2.ThemeMetrics.Metric.IconGlyph))
                    Chevron =
                        (fun b ->
                            Wanxiang2.IconSet.IconSet.image
                                Wanxiang2.IconSet.Glyph.ChevronRight
                                b
                                (metric Wanxiang2.ThemeMetrics.Metric.IconGlyph))
                    IconSlotWidth = 16.0
                    ItemMinHeight = 32.0
                    MaxHeight = 200.0
                }
            MenuMinWidth = 200.0
            GroupedMenuMinWidth = 240.0
            DwellFailureMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellFailure).Value
            DwellWarningMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellWarning).Value
            DwellDefaultMs = theme.Timing(Wanxiang2.MotionSwitch.Timing.ToastDwellDefault).Value
            DismissTick = TimeSpan.FromMilliseconds 100.0
        }

    // ---------------------------------------------------------------- 上下文

    /// 主题是**进程级单例**（真实宿主里就是 `SharedTheme.manager` 这么一件事）：
    /// Avalonia 的样式与资源字典挂在 Application 上，一个 ResourceDictionary 只能有一个 owner。
    /// 装两次会让控件的 DynamicResource 解析到错误的那份——所以这里只装一次。
    let private themeSingleton =
        lazy
            (let theme = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
             let styles = new ThemeStyles(theme)
             styles.Install Application.Current.Styles

             if
                 not (Application.Current.Resources.MergedDictionaries.Contains(theme.Resources :> IResourceDictionary))
             then
                 Application.Current.Resources.MergedDictionaries.Add(theme.Resources :> IResourceDictionary)

             theme, styles)

    /// 设置画廊的主题模式（Light 或 Dark）
    let setMode (mode: Wanxiang2.ThemePalette.PaletteMode) =
        let theme, _ = themeSingleton.Value

        if theme.Mode <> mode then
            theme.Apply mode
            theme.CompleteTransition()

        if not (isNull Avalonia.Application.Current) then
            Avalonia.Application.Current.RequestedThemeVariant <-
                (if mode = Wanxiang2.ThemePalette.PaletteMode.Dark then
                     Avalonia.Styling.ThemeVariant.Dark
                 else
                     Avalonia.Styling.ThemeVariant.Light)

    /// 获取当前画廊的主题模式
    let currentMode () : Wanxiang2.ThemePalette.PaletteMode =
        let theme, _ = themeSingleton.Value
        theme.Mode

    /// 造一套画廊上下文（真实控件库 + 假端口）。
    /// 主题是单例；各个 Desk 是**每次新建**的——Desk 持有桌内瞬态（上一帧投影、滚动位置、选中项），
    /// 复用会让上一个条目的状态漏进下一个条目。
    /// 造一套画廊上下文（真实控件库 + 假端口）。
    /// 主题是单例；各个 Desk 是**每次新建**的——Desk 持有桌内瞬态（上一帧投影、滚动位置、选中项），
    /// 复用会让上一个条目的状态漏进下一个条目。
    let createContextMode (mode: Wanxiang2.ThemePalette.PaletteMode) : GalleryContext =
        setMode mode

        let prefLimits: UiPrefLimits =
            {
                sidebarWidth = 284.0
                sidebarMinWidth = 232.0
                sidebarMaxWidth = 420.0
                windowMinWidth = 700.0
            }

        let theme, styles = themeSingleton.Value

        let root = Grid()
        let overlay = OverlayDesk(root, overlayLook theme)
        let brandMark = Wanxiang2.ShellDesk.ShellBrand(theme)
        let brandLogo (size: float) = brandMark.Logo size

        let composer = ComposerDesk(FakePorts.composerActions ())
        AutomationProperties.SetName(composer.Box.Input, "输入消息")
        let approval = ApprovalDesk(theme, overlay, ignore)

        let cards =
            MessageCards(
                CardLooks.ofTheme theme styles,
                {
                    Tint =
                        CodeTint
                            {
                                HighlightHtml = (fun _ -> None)
                                Background = (fun work -> work ())
                                After = (fun _ work -> work ())
                                Synchronous = true
                            }
                    MathHtml = (fun _ -> None)
                    Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
                    Clock = CaretClock()
                    Touch = CardTouch()
                    Now = fun () -> DateTimeOffset.UnixEpoch
                }
            )

        let chat =
            ChatScreen(
                ChatScreenMetrics.ofTheme theme styles,
                cards,
                composer,
                approval,
                overlay,
                brandLogo,
                FakePorts.chatActions ()
            )

        let sidebar =
            SidebarDesk(theme, overlay, FakePorts.sidebarActions (), brandLogo, now = fun () -> Scenes.fixedNow)

        let settings =
            SettingsScreen(
                theme,
                overlay,
                FakePorts.settingsHooks (),
                UiPrefs.defaults prefLimits,
                environmentInfo = ("Desktop (.NET gallery)", "Gallery OS")
            )

        let dialogs = DialogsDesk(theme, overlay)

        {
            Theme = theme
            Styles = styles
            OverlayRoot = root
            Overlay = overlay
            Sidebar = sidebar
            Composer = composer
            Chat = chat
            Settings = settings
            Cards = cards
            Dialogs = dialogs
            Approval = approval
            BrandLogo = brandLogo
        }

    let createContext () : GalleryContext = createContextMode (currentMode ())

    // ---------------------------------------------------------------- 条目

    /// 一张卡：把投影里的消息行喂给真实的卡片组。
    let private cardOf (ctx: GalleryContext) (row: MessageRowVS) : Control =
        let card, _ =
            ctx.Cards.Render(
                row,
                {
                    FontSize = 14.5
                    AutoCollapseReasoning = false
                    Spin = false
                    Streaming = row.Streaming
                    IsLastAssistant = row.IsLastAssistant
                    Usage = None
                    MissingAttachments = Set.empty
                    AwaitingApproval = Set.empty
                    BrandAvatar = fun () -> ctx.BrandLogo 28.0
                },
                FakePorts.cardActions ()
            )

        card

    /// 把 ChatPhase 里的消息行逐条渲染成卡片。
    let private cardsOf (ctx: GalleryContext) (phase: ChatPhase) : Control =
        match phase with
        | ChatPhase.Messages rows ->
            let panel = StackPanel(Spacing = 12.0, Width = 520.0)

            for row in rows do
                panel.Children.Add(cardOf ctx row)

            panel :> Control
        | _ -> Border() :> Control

    /// 画廊条目的表。
    let entries: Entry list =
        let composerEntry name note steps =
            {
                Name = name
                Widget = "ComposerBox"
                Note = note
                Steps = steps
                Build =
                    fun ctx vs ->
                        ctx.Composer.Render vs.Composer
                        ctx.Composer.Host
            }

        let chatEntry name note steps =
            {
                Name = name
                Widget = "MessageCardView"
                Note = note
                Steps = steps
                Build =
                    fun ctx vs ->
                        match vs.Chat.Phase with
                        | ChatPhase.Messages rows ->
                            let panel = StackPanel(Spacing = 12.0, Width = 520.0)

                            for row in rows do
                                panel.Children.Add(cardOf ctx row)

                            panel :> Control
                        | _ -> Border() :> Control
            }

        let sidebarEntry name note steps =
            {
                Name = name
                Widget = "SidebarDesk"
                Note = note
                Steps = steps
                Build =
                    fun ctx vs ->
                        ctx.Sidebar.Render vs.Sidebar
                        ctx.Sidebar.View
            }

        let settingsEntry name note steps =
            {
                Name = name
                Widget = "SettingsScreen"
                Note = note
                Steps = steps
                Build =
                    fun ctx vs ->
                        match vs.Settings with
                        | Some sv -> ctx.Settings.Render sv
                        | None -> ()

                        ctx.Settings.View
            }

        /// 输入区的一条消息（文本变化）。
        let composerText text : Scenes.Step =
            Scenes.Msg(Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged text)))

        /// 输入区的一条消息（挂一个附件）。
        let composerAttach fileName mediaType size sha : Scenes.Step =
            Scenes.Msg(
                Msg.User(UserMsg.Composer(ComposerUserMsg.Attach(Guid.NewGuid(), fileName, mediaType, size, sha)))
            )

        /// 侧栏的一条消息（搜索词变化）。
        let sidebarSearch query : Scenes.Step =
            Scenes.Msg(Msg.User(UserMsg.Sidebar(SidebarUserMsg.SearchTextChanged query)))

        /// 设置页的一条消息（直达分区）。
        let settingsSection part : Scenes.Step =
            Scenes.Msg(Msg.User(UserMsg.Settings(SettingsUserMsg.OpenSection(Some(SettingsPart.key part)))))

        /// 生成开始的线协议事件（当前对话）。
        let generationStarted (generationId: string) : Scenes.Step =
            Scenes.Wire(Scenes.generationStarted generationId)

        /// 一条流式增量（真的 generation.delta，带真实正文）。
        let generationDelta (generationId: string) (text: string) : Scenes.Step =
            Scenes.Wire(Scenes.generationDelta generationId text)

        [
            // ---- ComposerBox ----
            composerEntry "ComposerBox.idle" "空闲：未连接主机，输入区禁用并给出原因" (Scenes.authed)

            composerEntry "ComposerBox.typing" "输入中：草稿有字，发送键可按" (Scenes.authed @ [ composerText "帮我看看这个目录里有什么" ])

            composerEntry
                "ComposerBox.attachments"
                "带附件：托盘里有一张图片"
                (Scenes.authed
                 @ [
                     composerAttach "screenshot.png" "image/png" 204800L "sha-1"
                     composerText "这张图里是什么"
                 ])

            composerEntry
                "ComposerBox.generating"
                "生成中：发送键变成停止，输入区禁用"
                (Scenes.authed
                 @ [
                     Scenes.Wire(Scenes.conversationSnapshot (JsonArray [| Scenes.userText 1 "你好" |]))
                     generationStarted "44444444-4444-4444-4444-444444444444"
                 ])

            // ---- MessageCardView ----
            chatEntry
                "MessageCardView.user"
                "用户消息：右对齐气泡"
                (Scenes.withConversation (JsonArray [| Scenes.userText 1 "帮我读一下这个文件" |]))

            chatEntry
                "MessageCardView.assistant"
                "助手回复：Markdown 正文 + 思考折叠块"
                (Scenes.withConversation (JsonArray [| Scenes.assistantReasoning 1 |]))

            chatEntry
                "MessageCardView.toolcall"
                "工具调用卡：参数摘要可展开"
                (Scenes.withConversation (JsonArray [| Scenes.assistantToolCall 1 |]))

            // 错误卡：状态树里已有单一归属（ChatVS.Error，由 Workspace.project 从 RunState.Idle 投影）；
            // 这里仍走卡片组的真实控件入口渲染，见 AGENTS.md《已知缺口与各自的归属》3。
            // 所以直接用卡片组的 ErrorCard 入口渲染真实的错误卡。
            {
                Name = "MessageCardView.error"
                Widget = "MessageCardView"
                Note = "错误卡：重试与继续生成两个动作"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        let error: GenerationError =
                            {
                                kind = GenerationErrorKind.ProviderTimeout
                                message = "连接主机超时了。"
                                detail = Some "upstream timeout after 120s"
                                retryable = true
                                retryAfterSeconds = None
                            }

                        ctx.Cards.ErrorCard(error, (fun () -> ()), FakePorts.cardActions (), Some(fun () -> ()))
            }

            chatEntry
                "MessageCardView.streaming"
                "流式中：光标呼吸，正文逐段补齐"
                (Scenes.withConversation (JsonArray [| Scenes.assistantText 1 "开始生成" |])
                 @ [
                     generationStarted "55555555-5555-5555-5555-555555555555"
                     // 真的 delta：RunState.Generating 里才有 partial，投影才会多出流式行
                     generationDelta "55555555-5555-5555-5555-555555555555" "正在逐段补齐的回复……"
                 ])

            // ---- SidebarDesk ----
            sidebarEntry "SidebarDesk.empty" "空态：未连接主机" ([])

            sidebarEntry "SidebarDesk.rows" "有对话：置顶行 + 普通行 + 归档行" (Scenes.authed @ [ Scenes.Wire Scenes.listSnapshot ])

            // 批量模式：多选的事实在状态树（Chrome.BatchMode / Chrome.SelectedRowIds），
            // 侧栏只发意图。这里走**真消息**：SidebarUserMsg.EnterBatch → update → project → 渲染。
            {
                Name = "SidebarDesk.batch"
                Widget = "SidebarDesk"
                Note = "批量模式：选中若干行，批量条出现"
                Steps =
                    Scenes.authed
                    @ [
                        Scenes.Wire Scenes.listSnapshot
                        Scenes.Msg(
                            Msg.User(UserMsg.Sidebar(SidebarUserMsg.EnterBatch(Guid.Parse Scenes.conversationId)))
                        )
                        Scenes.Msg(
                            Msg.User(
                                UserMsg.Sidebar(
                                    SidebarUserMsg.ToggleBatch(Guid.Parse "22222222-2222-2222-2222-222222222222")
                                )
                            )
                        )
                    ]
                Build =
                    fun ctx vs ->
                        ctx.Sidebar.Render vs.Sidebar
                        ctx.Sidebar.View
            }

            sidebarEntry
                "SidebarDesk.search"
                "搜索中：查询词非空但没有命中"
                (Scenes.authed @ [ Scenes.Wire Scenes.listSnapshot; sidebarSearch "找不到的东西" ])

            // ---- SettingsScreen ----
            settingsEntry
                "SettingsScreen.providers"
                "模型服务分区"
                (Scenes.authed @ [ settingsSection SettingsPart.Providers ])

            settingsEntry "SettingsScreen.tools" "工具分区" (Scenes.authed @ [ settingsSection SettingsPart.Tools ])

            settingsEntry
                "SettingsScreen.generation"
                "对话默认值分区"
                (Scenes.authed @ [ settingsSection SettingsPart.Generation ])

            settingsEntry "SettingsScreen.devices" "设备分区" (Scenes.authed @ [ settingsSection SettingsPart.Devices ])

            settingsEntry
                "SettingsScreen.appearance"
                "外观与操作分区"
                (Scenes.authed @ [ settingsSection SettingsPart.Appearance ])

            settingsEntry "SettingsScreen.about" "关于分区" (Scenes.authed @ [ settingsSection SettingsPart.About ])

            // ---- OverlayDesk 对话框 ----
            {
                Name = "OverlayDesk.dialog.confirm"
                Widget = "OverlayDesk"
                Note = "确认对话框"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        ctx.Dialogs.Confirm("删除对话", "「画廊里的第一条对话」将被删除，8 秒内可撤销。", "删除", ignore) |> ignore
                        ctx.OverlayRoot
            }

            {
                Name = "OverlayDesk.dialog.prompt"
                Widget = "OverlayDesk"
                Note = "单行输入对话框"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        ctx.Dialogs.Prompt("重命名对话", "给这条对话起个新名字", "画廊里的第一条对话", "保存", fun _ _ -> ())
                        |> ignore

                        ctx.OverlayRoot
            }

            {
                Name = "OverlayDesk.dialog.connect"
                Widget = "OverlayDesk"
                Note = "连接对话框（地址 + 令牌）"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        ctx.Dialogs.Connect("ws://127.0.0.1:8080/ws", (fun _ -> ()), (fun _ -> ()))
                        |> ignore

                        ctx.OverlayRoot
            }

            {
                Name = "OverlayDesk.dialog.shortcuts"
                Widget = "OverlayDesk"
                Note = "快捷键帮助"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        ctx.Dialogs.Shortcuts() |> ignore
                        ctx.OverlayRoot
            }

            // ---- ChatEmpty 骨架 ----
            {
                Name = "ChatEmpty.skeleton"
                Widget = "ChatEmpty"
                Note = "对话列骨架屏（目录加载中）"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let skeleton = Wanxiang2.SkeletonList.SkeletonList()
                        skeleton.Show()
                        let panel = StackPanel(Spacing = 8.0, Width = 520.0)
                        panel.Children.Add(skeleton.Host)
                        panel :> Control
            }

            // ---- ChatEmpty 空态（真实 ShowPrompt 入口）----
            {
                Name = "ChatEmpty.empty"
                Widget = "ChatEmpty"
                Note = "对话区空态：从一句开始，三行起手式"
                Steps = Scenes.withConversation (JsonArray())
                Build =
                    fun ctx _ ->
                        let empty =
                            Wanxiang2.ChatEmpty.ChatEmpty(
                                [
                                    "用三句话说明这台主机现在能做什么", "用三句话说明这台主机现在能做什么"
                                    "指出一份 TOML 配置里的错误", "指出一份 TOML 配置里的错误"
                                    "把工具调用说成人话", "把工具调用说成人话"
                                ],
                                Some ignore,
                                ctx.BrandLogo
                            )

                        empty.ShowPrompt("从一句开始", "模型、附件和发送都在下面。要找回以前的对话，用侧栏。", None)
                        empty.Host
            }


            // ---- ActionStrip（消息脚注的操作条）----
            {
                Name = "ActionStrip.actions"
                Widget = "ActionStrip"
                Note = "操作条：复制与删除两个图标键"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let strip =
                            Wanxiang2.ActionStrip.ActionStrip(Wanxiang2.ActionStrip.StripRest.Subtle)

                        strip.Render(
                            {
                                Items =
                                    [
                                        {
                                            Id = "copy"
                                            Glyph = "copy"
                                            Tip = "复制"
                                            Name = "复制助手消息"
                                            Help = "把这条回复复制到剪贴板"
                                            Enabled = true
                                        }
                                        {
                                            Id = "delete"
                                            Glyph = "trash"
                                            Tip = "删除"
                                            Name = "删除助手消息"
                                            Help = "删掉这条回复"
                                            Enabled = true
                                        }
                                    ]
                                Subtle = false
                                Visible = true
                            },
                            ignore
                        )

                        let panel = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0)
                        panel.Children.Add(strip.Host)
                        panel :> Control
            }

            // ---- ApprovalPanel（工具审批卡）----
            {
                Name = "ApprovalPanel.approval"
                Widget = "ApprovalPanel"
                Note = "审批卡：模型想用工具，等用户批准"
                Steps =
                    Scenes.withConversation (JsonArray())
                    @ [
                        Scenes.Wire(
                            WireEvent.ToolApprovalRequested
                                {|
                                    approvalId = Guid.Parse "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
                                    conversationId = Guid.Parse Scenes.conversationId
                                    generationId = Guid.Parse "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
                                    callId = "call-1"
                                    toolName = "read_file"
                                    summary = "读取 /tmp/example.txt"
                                    arguments = JsonNode.Parse """{"path":"/tmp/example.txt"}"""
                                    policy = "first"
                                    expiresInSeconds = 120
                                |}
                        )
                    ]
                Build =
                    fun ctx vs ->
                        // 上下文中的审批面板已归 ChatScreen 所有，独立条目创建自己的真实组合者。
                        let approval = ApprovalDesk(ctx.Theme, ctx.Overlay, ignore)
                        approval.Render(vs.Approval, Some(Guid.Parse Scenes.conversationId))
                        approval.View
            }

            // ---- AttachmentTray（输入区附件托盘）----
            {
                Name = "AttachmentTray.items"
                Widget = "AttachmentTray"
                Note = "托盘：一张已就绪的图片与一个上传中的文件"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let tray = Wanxiang2.AttachmentTray.AttachmentTray()

                        let items: Wanxiang2.AttachmentTray.TrayItem list =
                            [
                                {
                                    Id = Guid.Parse "cccccccc-cccc-cccc-cccc-cccccccccccc"
                                    FileName = "screenshot.png"
                                    SizeText = "200 KB"
                                    StateText = "已就绪"
                                    Tip = "200 KB · image/png"
                                    Name = "screenshot.png"
                                    Failed = false
                                    Ready = true
                                    IsImage = true
                                    ShrinkNote = None
                                    Thumbnail = None
                                }
                                {
                                    Id = Guid.Parse "dddddddd-dddd-dddd-dddd-dddddddddddd"
                                    FileName = "notes.txt"
                                    SizeText = "12 KB"
                                    StateText = "上传中 40%"
                                    Tip = "12 KB · text/plain"
                                    Name = "notes.txt"
                                    Failed = false
                                    Ready = false
                                    IsImage = false
                                    ShrinkNote = None
                                    Thumbnail = None
                                }
                            ]

                        tray.Render(items, false)
                        let panel = StackPanel(Spacing = 4.0)
                        panel.Children.Add(tray.Host)
                        panel :> Control
            }

            // ---- BatchBar（多选批量条）----
            {
                Name = "BatchBar.selection"
                Widget = "BatchBar"
                Note = "批量条：已选 2 项，全选与置顶 / 归档 / 删除"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let bar = Wanxiang2.BatchBar.BatchBar()

                        bar.Render(
                            Some
                                {
                                    SelectedCount = 2
                                    AllSelected = false
                                    Busy = false
                                    AllPinned = false
                                }
                        )

                        let panel = StackPanel(Spacing = 8.0)
                        panel.Children.Add(bar.Header)
                        panel.Children.Add(bar.ActionBar)
                        panel :> Control
            }

            // ---- BrandMark（品牌标）----
            {
                Name = "BrandMark.tile"
                Widget = "BrandMark"
                Note = "品牌标：没有图时退化成「万」字"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let panel = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
                        panel.Children.Add(Wanxiang2.BrandMark.BrandMark.tile None 48.0)
                        panel :> Control
            }

            // ---- ChatHeader（对话顶栏）----
            {
                Name = "ChatHeader.header"
                Widget = "ChatHeader"
                Note = "顶栏：对话标题、侧栏键与更多键"
                Steps = Scenes.withConversation (JsonArray())
                Build =
                    fun _ vs ->
                        let header = Wanxiang2.ChatHeader.ChatHeader()
                        header.Render vs.Header
                        header.Host
            }

            // ---- ConfirmPane（破坏性操作确认）----
            {
                Name = "ConfirmPane.confirm"
                Widget = "ConfirmPane"
                Note = "确认框：标题、正文与取消 / 删除两键"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let pane =
                            Wanxiang2.ConfirmPane.ConfirmPane("删除对话", "「画廊里的第一条对话」将被删除，8 秒内可撤销。", "删除", false)

                        pane.View
            }

            // ---- DevicesPane（设置里的设备分区）----
            {
                Name = "DevicesPane.devices"
                Widget = "DevicesPane"
                Note = "设备分区：配对码、地址与已授权设备"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let pane = Wanxiang2.DevicesPane.DevicesPane()

                        let qr =
                            [| [| true; false; true |]; [| false; true; false |]; [| true; true; false |] |]

                        let state: Wanxiang2.DevicesPane.DevicesPaneState =
                            {
                                Invite =
                                    Wanxiang2.DevicesPane.DevicesPaneInvite.Code(
                                        "482913",
                                        "四八二九一三",
                                        "4 分 58 秒后失效",
                                        false,
                                        Wanxiang2.DevicesPane.DevicesPaneAddress.Reachable("ws://192.168.1.10:8080", qr)
                                    )
                                List =
                                    Wanxiang2.DevicesPane.DevicesPaneList.Rows
                                        [
                                            {
                                                Id = "dev-1"
                                                Name = "phone"
                                                Title = "我的手机"
                                                Meta = "这台设备 · 手机 · 在线 · 创建于 2026-10-20"
                                                Revoked = false
                                                CanRename = true
                                                CanRevoke = true
                                            }
                                        ]
                                Editing = None
                            }

                        pane.Render state
                        pane.View
            }

            // ---- ExportSheet（导出对话框）----
            {
                Name = "ExportSheet.export"
                Widget = "ExportSheet"
                Note = "导出对话框：状态行、进度条与保存两键"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        let brush k = ctx.Theme.Brush k :> IBrush

                        let look: Wanxiang2.ExportSheet.ExportSheetAppearance =
                            {
                                Text = brush Wanxiang2.ThemePalette.PaletteKey.Text
                                Muted = brush Wanxiang2.ThemePalette.PaletteKey.TextMuted
                                Success = brush Wanxiang2.ThemePalette.PaletteKey.Success
                                Danger = brush Wanxiang2.ThemePalette.PaletteKey.Danger
                            }

                        let sheet = Wanxiang2.ExportSheet.ExportSheet(look, "画廊里的第一条对话", "导出的文件只写在本机。")

                        sheet.Render(
                            {
                                StatusText = "正在生成 Markdown"
                                Tone = Wanxiang2.ExportSheet.ExportSheetTone.Text
                                Progress = Wanxiang2.ExportSheet.ExportSheetProgress.Determinate 40.0
                                Action =
                                    Wanxiang2.ExportSheet.ExportSheetAction.Save(
                                        Wanxiang2.ExportSheet.ExportSaveGate.Enabled,
                                        true
                                    )
                                CloseText = "取消导出"
                            }
                        )

                        sheet.View
            }

            // ---- FieldCheck（字段级校验反馈）----
            {
                Name = "FieldCheck.invalid"
                Widget = "FieldCheck"
                Note = "字段校验：出错时输入壳标红并给出错误行"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let check = Wanxiang2.FieldCheck.FieldCheck()
                        let box = TextBox(PlaceholderText = "主机地址")
                        AutomationProperties.SetName(box, "主机地址")
                        let shell = Border(Child = box, Padding = Thickness(8.0))
                        let message = check.MessageFor(box)
                        check.ApplyError(box, "地址不能为空")
                        let panel = StackPanel(Spacing = 4.0)
                        panel.Children.Add(shell)
                        panel.Children.Add(message)
                        panel :> Control
            }

            // ---- FocusOrigin（聚焦来源标记）----
            {
                Name = "FocusOrigin.programmatic"
                Widget = "FocusOrigin"
                Note = "聚焦来源：接上报告器后，程序化聚焦会给控件挂 focus-by-program 类"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let button = Button(Content = "程序化聚焦我")
                        Wanxiang2.FocusOrigin.FocusOrigin.report button
                        let panel = StackPanel(Spacing = 8.0)
                        panel.Children.Add(button)
                        panel :> Control
            }

            // ---- IconSet（Lucide 图标集）----
            {
                Name = "IconSet.glyphs"
                Widget = "IconSet"
                Note = "图标集：搜索、齿轮、扳手与数据库"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        let brush = ctx.Theme.Brush Wanxiang2.ThemePalette.PaletteKey.Text :> IBrush

                        let size =
                            ctx.Theme.Metric Wanxiang2.ThemeMetrics.Metric.IconGlyph :> IObservable<float>

                        let panel = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)

                        for glyph in
                            [
                                Wanxiang2.IconSet.Glyph.Search
                                Wanxiang2.IconSet.Glyph.Gear
                                Wanxiang2.IconSet.Glyph.Wrench
                                Wanxiang2.IconSet.Glyph.Database
                            ] do
                            panel.Children.Add(Wanxiang2.IconSet.IconSet.image glyph brush size)

                        panel :> Control
            }

            // ---- ModelSwitchPanel（模型切换浮层）----
            {
                Name = "ModelSwitchPanel.models"
                Widget = "ModelSwitchPanel"
                Note = "模型切换：搜索框、模型行与默认 / 当前标记"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let slots: Wanxiang2.ModelSwitchPanel.ModelSwitchSlots =
                            {
                                Groups = (fun _ opts -> [ None, opts ])
                                SearchHelp = "输入筛选，↑↓ 选择，回车切换"
                                EmptyText = "没有匹配的模型"
                                AppliesNote = "切换只影响当前对话，新对话仍用默认模型。"
                            }

                        let options: Wanxiang2.ModelSwitchPanel.ModelChoice list =
                            [
                                {
                                    ProviderId = "openai"
                                    ProviderLabel = "OpenAI"
                                    Model = "gpt-5"
                                    IsCurrent = true
                                    IsDefault = true
                                    Probe = Wanxiang2.ModelSwitchPanel.ProbeState.Probed 2
                                }
                                {
                                    ProviderId = "anthropic"
                                    ProviderLabel = "Anthropic"
                                    Model = "claude-opus-4"
                                    IsCurrent = false
                                    IsDefault = false
                                    Probe = Wanxiang2.ModelSwitchPanel.ProbeState.Unknown
                                }
                            ]

                        Wanxiang2.ModelSwitchPanel.ModelSwitchPanel.build None slots options "切换模型" ignore
            }

            // ---- PalettePanel（命令面板）----
            {
                Name = "PalettePanel.palette"
                Widget = "PalettePanel"
                Note = "命令面板：搜索框、最近使用与结果计数"
                Steps = Scenes.authed
                Build =
                    fun _ _ ->
                        let slots: Wanxiang2.PalettePanel.PaletteSlots =
                            {
                                Query =
                                    (fun _ ->
                                        {
                                            Rows =
                                                [
                                                    {
                                                        Id = "conv-1"
                                                        Title = "画廊里的第一条对话"
                                                        Subtitle = "上一条回复的摘要"
                                                        KindLabel = "对话"
                                                        Shortcut = Some "Ctrl+1"
                                                        Recent = true
                                                    }
                                                    {
                                                        Id = "act-1"
                                                        Title = "新建对话"
                                                        Subtitle = "开始一条新对话"
                                                        KindLabel = "操作"
                                                        Shortcut = None
                                                        Recent = false
                                                    }
                                                ]
                                            MatchCount = 2
                                            HasRecent = true
                                        })
                                CompactKeys = (fun keys -> keys)
                                PrefersTouch = (fun () -> false)
                            }

                        let content, _search =
                            Wanxiang2.PalettePanel.PalettePanel.build slots (fun _ _ -> ())

                        content
            }

            // ---- PopupMenu（浮层菜单）----
            {
                Name = "PopupMenu.menu"
                Widget = "PopupMenu"
                Note = "浮层菜单：复制、重命名与危险项删除"
                Steps = Scenes.authed
                Build =
                    fun ctx _ ->
                        let look = (overlayLook ctx.Theme).Menu

                        let entries: Wanxiang2.PopupMenu.PopupEntry list =
                            [
                                {
                                    Label = "复制"
                                    Hint = "Ctrl+C"
                                    Icon = None
                                    Selected = false
                                    Danger = false
                                    Action = ignore
                                }
                                {
                                    Label = "重命名"
                                    Hint = ""
                                    Icon = None
                                    Selected = true
                                    Danger = false
                                    Action = ignore
                                }
                                {
                                    Label = "删除对话"
                                    Hint = ""
                                    Icon = None
                                    Selected = false
                                    Danger = true
                                    Action = ignore
                                }
                            ]

                        match Wanxiang2.PopupMenu.PopupMenu.build look ignore entries with
                        | Some content -> content
                        | None -> Border() :> Control
            }

            // ---- ToastStrip（提示条带）----
            {
                Name = "ToastStrip.toast"
                Widget = "ToastStrip"
                Note = "提示条：删除对话后的成功提示"
                Steps =
                    Scenes.withConversation (JsonArray())
                    @ [
                        Scenes.Msg(
                            Msg.User(
                                UserMsg.Sidebar(SidebarUserMsg.DeleteConversation(Guid.Parse Scenes.conversationId))
                            )
                        )
                    ]
                Build =
                    fun ctx vs ->
                        // 提示条带是**真的 ToastStrip 控件**（外观与 OverlayDesk 同一份口径），
                        // 但自己带一条：OverlayDesk 那条已经挂在浮层根上，
                        // 拿它当画布内容会二次挂父（控件不能有两个父）。
                        let strip = Wanxiang2.ToastStrip.ToastStrip((overlayLook ctx.Theme).Toasts)
                        strip.Render vs.Toasts
                        strip.View :> Control
            }
        ]

    // ---------------------------------------------------------------- 渲染

    /// 画廊画布的尺寸（固定的，渲染与像素基线都以它为准）。
    let canvasWidth = 720.0
    let canvasHeight = 900.0

    /// 造一个条目的控件（把状态跑出来、投影出来、再喂给真实控件库）。
    let build (ctx: GalleryContext) (entry: Entry) : Control =
        let _, vs = Scenes.run entry.Steps
        let control = entry.Build ctx vs
        // 画布尺寸固定：渲染才有确定的像素尺寸，条目之间也可比。
        Border(
            Width = canvasWidth,
            Height = canvasHeight,
            Padding = Thickness(16.0),
            Background = ctx.Theme.Brush Wanxiang2.ThemePalette.PaletteKey.Canvas,
            Child = control
        )

    /// 只跑状态，不造界面：给「某个状态长什么样」的问题一个纯投影的答案。
    let projectOf (steps: Scenes.Step list) : WorkspaceViewState =
        let _, vs = Scenes.run steps
        vs

    /// 按名字找条目。
    let find (name: string) : Entry option =
        entries |> List.tryFind (fun e -> e.Name = name)

    /// 清单文本（--list 的输出；一条一行：名字 / 控件库 / 说明）。
    let listText () : string =
        entries
        |> List.map (fun e -> sprintf "%s\t%s\t%s" e.Name e.Widget e.Note)
        |> String.concat "\n"
