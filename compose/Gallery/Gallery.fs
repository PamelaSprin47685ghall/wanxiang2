namespace Wanxiang2.Gallery

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open Avalonia
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

    /// 造一套画廊上下文（真实控件库 + 假端口）。
    /// 主题是单例；各个 Desk 是**每次新建**的——Desk 持有桌内瞬态（上一帧投影、滚动位置、选中项），
    /// 复用会让上一个条目的状态漏进下一个条目。
    let createContext () : GalleryContext =
        let prefLimits: UiPrefLimits =
            {
                sidebarWidth = 260.0
                sidebarMinWidth = 200.0
                sidebarMaxWidth = 400.0
                windowMinWidth = 700.0
            }

        let theme, styles = themeSingleton.Value

        let root = Grid()
        let overlay = OverlayDesk(root, overlayLook theme)
        // 品牌标：**真的** ShellBrand（与 app/Desktop 的 SharedTheme.logo 同一个类）。
        // 不用占位方块——画廊是给人看界面真容的，占位方块会把「品牌标长什么样」这件事藏起来。
        let brandMark = Wanxiang2.ShellDesk.ShellBrand(theme)
        let brandLogo (size: float) = brandMark.Logo size

        let composer = ComposerDesk(FakePorts.composerActions ())
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

        let sidebar = SidebarDesk(theme, overlay, FakePorts.sidebarActions (), brandLogo)

        let settings =
            SettingsScreen(theme, overlay, FakePorts.settingsHooks (), UiPrefs.defaults prefLimits)

        let dialogs = DialogsDesk(theme, overlay)

        {
            Theme = theme
            Styles = styles
            OverlayRoot = root
            Sidebar = sidebar
            Composer = composer
            Chat = chat
            Settings = settings
            Cards = cards
            Dialogs = dialogs
            Approval = approval
            BrandLogo = brandLogo
        }

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
