// 照抄旧 Design/Tokens.fs 数值段与 Design/Metrics.fs（extract-ledger@0c0b2a6）；Spacing 照抄 SpacingRamp，ReadingRhythm 只留用到的一项。
namespace OldMetrics

/// Fluent 2 global spacing ramp —— 全应用唯一的间距与槽位刻度。
///
/// 官方 ramp（fluent2.microsoft.design/layout）：4px 基准，明确包含 2 / 6 / 10
/// 三档例外——它们用于图标内衬与像素网格对齐，不是"凑整"。因此本刻度不是
/// 「4 的倍数」，而是**官方 ramp 的完整取值**；任何不在这张表里的数值都是违规。
///
/// 规范权威：`wanxiang-ui` skill §2。
module Spacing =

    // ---- ramp 本体（官方取值，顺序即 ramp 顺序）----

    let spaceNone = 0.0
    /// size20：图标内衬、hairline 内衬、chip / tag 纵向内衬
    let spaceXXs = 2.0
    /// size40：紧凑堆叠间距、单行小控件纵向呼吸
    let spaceXs = 4.0
    /// size60：字段标签 / 校验行 / hint 的贴字段留白
    let spaceSm = 6.0
    /// size80：组件内部主间距、chip 横向内衬
    let spaceMd = 8.0
    /// size100：紧排 chip 横向内衬（需与 8 视觉等重时）
    let spaceLg = 10.0
    /// size120：字段外壳内边距、图标与文字间距
    let spaceXl = 12.0
    /// size160：组件之间、按钮横向内衬
    let space2xl = 16.0
    /// size200：侧栏行内边距、宽档组件间距
    let space3xl = 20.0
    /// size240：页面留白（中小窗档）、分组卡内边距、消息间距
    let space4xl = 24.0
    /// size280：宽屏档留白过渡
    let space5xl = 28.0
    /// size320：区块之间、桌面档页面留白
    let space6xl = 32.0
    /// size360：宽屏留白过渡
    let space7xl = 36.0
    /// size400：主分节之前
    let space8xl = 40.0
    /// size480：宽屏档页面留白
    let space10xl = 48.0
    /// size520：分节最大档
    let space11xl = 52.0
    /// size560：槽位宽度上限档
    let space12xl = 56.0

    /// ramp 全档（收敛校验用：任何间距、槽位、内边距必须 ∈ 本集合）。
    let ramp : float list =
        [ spaceNone; spaceXXs; spaceXs; spaceSm; spaceMd; spaceLg; spaceXl
          space2xl; space3xl; space4xl; space5xl; space6xl; space7xl; space8xl
          space10xl; space11xl; space12xl ]

    /// 槽位宽度档。槽位是**布局列宽**不是间距，但对不齐的根因正是它们各自
    /// 发明的 18 / 19 / 54 / 56 / 110，故与 ramp 同源收进阶梯。
    module Slot =

        /// 行内 / 菜单 / 状态点槽：全应用只此一值。
        /// 此前侧栏状态槽 19、菜单图标槽 18 分叉 1px——现在都读这里。
        let icon = space3xl

        /// 宽槽：附件状态槽与设置页数值槽此前同为 56 但各写一份。
        let wide = space12xl

        /// 更宽一档：关于页键名列（此前 110 → 112，进阶梯）。
        let wide2 = 112.0

        /// 槽位档全表（收敛校验用）。
        let all : float list = [ icon; wide; wide2 ]

    /// 圆角（Fluent 2 corner radius）。控件默认 8，卡片 12，浮层 16。
    module Radius =

        let xs = 4.0
        let sm = 6.0
        /// 控件默认圆角（按钮 / 输入壳 / 图标按钮 / 行）
        let md = 8.0
        /// 卡片 / 分组卡 / 浮层
        let lg = 12.0
        /// 对话框 / Hero 卡片
        let xl = 16.0
        let pill = 999.0

        let all : float list = [ xs; sm; md; lg; xl; pill ]

    /// 描边宽度：全应用只有四档。
    module Stroke =

        /// 1px：tag / 输入壳 / 按钮 / 分组卡 / 分隔线的唯一描边宽
        let thin = 1.0
        /// 2px：左缘强调条、选中行标记、焦点环外扩
        let thick = 2.0
        /// 3px：Markdown 引用块左缘（印刷重量，有意更宽一档）
        let quote = 3.0

        let all : float list = [ thin; thick; quote ]

    /// 字距档：层级靠字号与颜色，字距只做微调。
    module Tracking =

        /// 字段标签、tag
        let label = 0.3
        /// 空态主标题等强调文案
        let emphasis = 0.4
        /// 区块标签
        let section = 0.6
        /// 页面级测量标签（最宽）
        let display = 0.8

        let all : float list = [ label; emphasis; section; display ]

    /// 图标尺寸档：字形与命中区分离，落地经 Ui.setSquareTarget 只调命中区。
    module Glyph =

        /// 行内图标、chip / tag 内图标
        let sm = 14.0
        /// 按钮图标、工具栏、状态点
        let md = 16.0
        /// 空态锚点、功能入口、侧栏品牌区
        let lg = 20.0

        let all : float list = [ sm; md; lg ]

    /// 命中区下限（与字形分离）。依据见 `wanxiang-ui` skill §1.3。
    module Hit =

        /// 行内 / 脚注动作
        let inlineTarget = 20.0
        /// 桌面常规动作
        let regular = 24.0
        /// compact / 触控动作（Fluent 2 移动触控下限）
        let touch = 44.0

        let all : float list = [ inlineTarget; regular; touch ]

    /// 是否落在官方 ramp 上。收敛测试对所有间距 / 槽位断言本函数为真。
    let isRamp (value: float) = List.contains value ramp

    /// 槽位 / 圆角 / 描边 / 字距 / 字形 / 命中区各自是否合规。
    let isSlot (value: float) = List.contains value Slot.all
    let isRadius (value: float) = List.contains value Radius.all
    let isStroke (value: float) = List.contains value Stroke.all
    let isTracking (value: float) = List.contains value Tracking.all
    let isGlyph (value: float) = List.contains value Glyph.all
    let isHit (value: float) = List.contains value Hit.all

    /// 间距 / 槽位 / 圆角 / 描边的并集（几何扫描用）。
    let geometry : float list =
        ramp @ Slot.all @ Radius.all @ Stroke.all

module ReadingRhythm =
    let helperLineHeight = 18.0

module Tokens =

    // ---- 字号阶梯 ----
    /// 元信息、角标
    let fontMicro = 10.5
    /// 辅助说明
    let fontCaption = 11.5
    /// 界面小字（按钮、标签）
    let fontSmall = 12.5
    /// 界面正文
    let fontBody = 13.5
    /// 阅读正文（消息）
    let fontReading = 14.5
    /// 小标题
    let fontTitle = 16.0
    /// 区块标题
    let fontHeading = 19.0
    /// 页面主标题
    let fontDisplay = 25.0

    /// 不透明度阶梯：状态表达只切透明度，不改尺寸，避免布局跳动。
    let opacityDisabled = 0.5
    let opacitySubtle = 0.62
    let opacityComposerDisabled = 0.72
    let opacityPressed = 0.78
    /// 流式光标呼吸的不透明度区间：只改 opacity、不改尺寸；
    /// 节奏由 MotionLedger.caretBreathPhase + caretBreathFrame 驱动（MessageCard 已消费）
    let opacityCaretBreathMin = 0.55
    let opacityCaretBreathMax = 1.0
    /// 复制确认反馈淡出时经过的中间不透明度（随后回到 1.0），
    /// 节奏消费 MotionLedger.copyConfirmationFade（MessageCard 复制确认已消费）
    let opacityCopyConfirmFade = 0.6

    /// hover 微暗：整条可点内容（消息头、操作条）hover 时从 1.0 压到 0.9。
    /// 有意不并入上面的状态阶梯：那几档服务禁用/按压/输入态，这一档服务 hover 提
    /// 示，比 opacityPressed 轻；MessageCard 曾有三处裸 0.9。
    let opacityHoverDim = 0.9

    /// 骨架不透明度组：骨架只切不透明度，不改尺寸（几何契约不变）。
    /// - base：静态骨架条（侧栏任务条等）的基准档；
    /// - breathMin：呼吸区间下限（上限历来是 1.0，见 MotionLedger.skeletonBreathFrame）；
    /// - reduced：减弱动效时的静态整屏值；此前该值只活在 ChatView 的注释里。
    let skeletonOpacityBase = 0.65
    let skeletonOpacityBreathMin = 0.55
    let skeletonOpacityReduced = 0.85

    /// 带框块（代码块、表格）的统一内边距。两者常在同一段回答里前后出现，
    /// 各自取值会让左缘差几个像素，读起来像没对齐的两张卡片。
    let blockPaddingX = Spacing.spaceXl
    let blockPaddingY = Spacing.spaceMd

    // ---- 结构尺寸 ----
    let sidebarWidth = 284.0
    let sidebarMinWidth = 232.0
    let sidebarMaxWidth = 420.0
    /// 消息阅读列宽：CJK 正文每行 40–45 字最舒服
    let readingWidth = 748.0
    let barHeight = 52.0
    let iconButton = 30.0
    let iconGlyph = Spacing.Glyph.md
    let iconStroke = 1.6
    let shellInset = Spacing.space2xl

    // ---- 品牌 ----
    let logoSplash = 76.0
    let logoRadiusRatio = 22.0 / 76.0
    let logoSidebar = 26.0
    let logoEmpty = 68.0
    let logoAvatar = 26.0



module ControlMetrics =

    /// 常规文本按钮用 MinHeight，让 Avalonia 自己处理字体 / scale 后的真实高度。
    let textButtonMinHeight = 34.0
    let textButtonPaddingY = Spacing.spaceSm
    /// “保存”→“正在保存…”时按钮外框不能扩张，避免 footer 横向跳动。
    let pendingActionMinWidth = 96.0

    let textFieldTextMinHeight = 22.0
    let textFieldPaddingY = Spacing.spaceMd
    /// 字段标签/校验行/hint 的左缘内缩：让说明文字与控件内容左缘对齐而不是贴边框。
    let fieldInsetX = Spacing.spaceXXs

    /// 设置页字号值的固定槽宽与「关于」页键名列槽宽：全部读 Spacing.Slot，
    /// 不再各处发明 54 / 56 / 110（见 wanxiang-ui skill §2.2）。
    let fontSizeValueMinWidth = Spacing.Slot.wide
    let aboutKeyMinWidth = Spacing.Slot.wide2

    /// 常规行的最小高度：设置页开关行与侧栏对话行同一行高语义的唯一真源。
    /// 此前 44.0 在 SettingsGeneral 与这里各写一份、同值不同源。
    let rowMinHeight = 44.0

    let sidebarRowMinHeight = rowMinHeight
    /// 设置页开关行的最小高度：与侧栏行同一语义，引用 rowMinHeight。
    let settingsRowMinHeight = rowMinHeight
    let sidebarRowPaddingY = Spacing.spaceSm
    let sidebarStatusLineHeight = ReadingRhythm.helperLineHeight
    /// 行内状态槽宽度：与菜单图标槽同一档（此前 19 与 18 分叉 1px）。
    let sidebarStateSlotWidth = Spacing.Slot.icon
    let sidebarStateGlyphSize = 11.0
    let sidebarRunningDotSize = 6.0
    /// 行内/分节状态点直径：侧栏运行点、分节状态点同一语义的唯一真源
    ///（此前只有侧栏一处命名，其它位置各写裸 6.0）。
    let statusDotSize = 6.0
    /// chip 内部小圆点直径：比状态点小一档，标记 chip 的附属语义。
    let chipDotSize = 5.0
    /// 左缘强调条统一档：侧栏选中行、toast、思考竖线共用同一 2.0。
    /// 引用块用更宽的 quoteEdgeWidth（印刷引用条的视觉重量，两档有意保留）。
    let accentEdgeWidth = Spacing.Stroke.thick
    /// 选中行左缘强调条宽：非色线索，与悬停态区分；统一档见 accentEdgeWidth。
    let sidebarSelectedEdgeWidth = accentEdgeWidth
    /// 引用块左缘强调条：比通用 accentEdgeWidth 宽一档，Markdown 引用的既有意图。
    let quoteEdgeWidth = Spacing.Stroke.quote
    /// 侧栏外框/页脚分隔线宽：唯一来源，避免各处自发明 1.0。
    let sidebarDividerWidth = Spacing.Stroke.thin
    /// 控件/发丝线描边宽的全应用唯一档：tag、输入壳、按钮、chip、分组卡的 1px
    /// 边框都引用它；此前这些位置各自写 Thickness 1.0 / Height = 1.0。
    let borderWidth = Spacing.Stroke.thin
    /// 行内标题+预览垂直间距：紧凑堆叠是语义，不是“顺手写 0”。
    let sidebarRowContentSpacing = 0.0

    let composerInputMinHeight = 30.0
    let composerMaxHeight = 240.0
    let composerInputMaxHeight = 240.0
    let composerShellPaddingX = Spacing.spaceXl
    let composerShellPaddingY = Spacing.spaceMd
    let composerAttachmentNameMaxWidth = 160.0
    let composerAttachmentStateWidth = Spacing.Slot.wide
    let composerAttachmentIconSlot = Tokens.iconGlyph
    let composerModelMaxWidth = 240.0
    let composerModelCompactMaxWidth = 150.0

    /// chip 内边距的唯一真源（横向 8 = space2、纵向 2 = compactRowPaddingY）。
    /// 此前只有 Composer 的 chip 写着 padding、没有语义名；现收敛为一对语义名，
    /// 旧的 compactChipPadding* 降为别名（见下），全应用 chip 只有这一处定义。
    let chipPaddingX = Spacing.spaceMd
    let chipPaddingY = Spacing.spaceXXs

    /// 兼容别名：仍被 Composer 的附件 chip / 模型 chip 引用，引用同源于上面的
    /// chipPadding*——别名不持有自己的值，只是同一个量的旧名字。
    /// 纵向前者是 space1(4)，现统一到 compactRowPaddingY(2)：chip 与标签同为单行
    /// 小控件，纵向密度不应分叉。旧值 4 不由任何测试固定；反过来，
    /// DesignConvergenceTests.ChipDensityHasSingleSourceUnderBothNames 钉住的是
    /// compactChipPaddingX/Y 与 chipPaddingX/Y 的相等关系（别名必须跟真源走）。
    let compactChipPaddingX = chipPaddingX
    let compactChipPaddingY = chipPaddingY

    /// tag（小标签）的横向内边距：7.0 是它的既有外观值，DesignConvergence
    /// 测试钉住 Padding(7.0, tightRowPaddingY)。chip 横向用更宽的 space2，
    /// tag 纵向仍是 Spacing.spaceXXs：tag/chip 横向差一档是有意的两档。
    let tagPaddingX = Spacing.spaceSm

    let attachmentRowPaddingY = Spacing.spaceSm
    let attachmentNameMaxWidth = 260.0

    let menuItemMinHeight = 34.0
    let menuItemPaddingY = Spacing.spaceSm
    let menuIconSlotWidth = Spacing.Slot.icon
    let menuRightSlotMinWidth = Spacing.space2xl

    let toastAccentWidth = accentEdgeWidth

    let settingsNavWidth = 196.0
    let settingsNavMinHeight = 38.0
    let settingsContentMaxWidth = 980.0

    /// Menu.selectButton 的垂直内边距：只读按钮比菜单项（7）再紧一档的手感。
    let selectButtonPaddingY = Spacing.spaceSm

    /// 多行输入最小高度：表单指令类文本框的标准档。
    let textAreaMinHeight = 96.0
    /// 长文本特例（消息内容、大段说明）：160 是"长文本"语义而非 96 的近似值，有意保留。
    let textAreaLongMinHeight = 160.0

    /// 忙指示 spinner：常规 14、紧凑 11，同一组件两档尺寸（非近似值）。
    let spinnerSize = 14.0
    let spinnerCompactSize = 11.0
    /// spinner 的笔画宽与承载弧线的画布尺寸：笔画决定 spinner 的视觉重量，
    /// 画布是弧线几何（半径 6.4 的圆）被缩放前所在的坐标系，不改数值只挪 literals。
    let spinnerStrokeWidth = 1.8
    let spinnerCanvasSize = 16.0

    /// Ui.toggle 组件几何：track 34×20、knob 14、水平内插 3。
    let toggleTrackWidth = 34.0
    let toggleTrackHeight = 20.0
    let toggleKnobSize = 14.0
    let toggleKnobInsetX = 3.0

    /// 空态主操作按钮（136×38）与空态标题最小宽。
    let emptyStateActionMinWidth = 136.0
    let emptyStateActionHeight = 38.0
    let emptyStateTitleMinWidth = 120.0

    /// 「回到底」按钮最小宽：与 pendingActionMinWidth 同为 96 但语义不同
    ///（滚动定位 vs 保存占位），按 MotionLedger 纪律分记，不合档。
    let scrollToBottomMinWidth = 96.0

    /// 错误卡片「重试」按钮最小宽。
    let retryButtonMinWidth = 80.0

    /// 流式光标几何（呼吸区间在 Tokens.opacityCaretBreath*）：宽 7、高 15、圆角 1.5。
    let caretWidth = 7.0
    let caretHeight = 15.0
    let caretRadius = 1.5

    /// Markdown 无序列表圆点：直径 4、圆角 2。
    let listMarkerDotSize = 4.0
    let listMarkerDotRadius = 2.0

    /// Markdown 任务框：14 见方、1.4 描边、勾形 10。
    let taskBoxSize = 14.0
    let taskBoxBorderWidth = 1.4
    let taskCheckGlyphSize = 10.0

    /// 骨架屏头像边长：与真实列表头像同一 28 见方，骨架换成真卡时轮廓不变、
    /// 不产生跳变。此前 ChatView 骨架屏的头像在两处各写裸 28.0，无语义名。
    let skeletonAvatarSize = 28.0

module ContentMetrics =

    let messageGap = Spacing.space4xl
    let messageEndBreathing = 48.0
    let scrollBottomThreshold = 48.0
    let scrollBottomRevealThreshold = 120.0
    let emptyStateMaxWidth = 480.0
    let toastMaxWidth = 560.0


module Adapter =
    let all : (string * float) list =
        [ "FontMicro", Tokens.fontMicro
          "FontCaption", Tokens.fontCaption
          "FontSmall", Tokens.fontSmall
          "FontBody", Tokens.fontBody
          "FontReading", Tokens.fontReading
          "FontTitle", Tokens.fontTitle
          "FontHeading", Tokens.fontHeading
          "FontDisplay", Tokens.fontDisplay
          "OpacityDisabled", Tokens.opacityDisabled
          "OpacitySubtle", Tokens.opacitySubtle
          "OpacityComposerDisabled", Tokens.opacityComposerDisabled
          "OpacityPressed", Tokens.opacityPressed
          "OpacityCaretBreathMin", Tokens.opacityCaretBreathMin
          "OpacityCaretBreathMax", Tokens.opacityCaretBreathMax
          "OpacityCopyConfirmFade", Tokens.opacityCopyConfirmFade
          "OpacityHoverDim", Tokens.opacityHoverDim
          "SkeletonOpacityBase", Tokens.skeletonOpacityBase
          "SkeletonOpacityBreathMin", Tokens.skeletonOpacityBreathMin
          "SkeletonOpacityReduced", Tokens.skeletonOpacityReduced
          "BlockPaddingX", Tokens.blockPaddingX
          "BlockPaddingY", Tokens.blockPaddingY
          "SidebarWidth", Tokens.sidebarWidth
          "SidebarMinWidth", Tokens.sidebarMinWidth
          "SidebarMaxWidth", Tokens.sidebarMaxWidth
          "ReadingWidth", Tokens.readingWidth
          "BarHeight", Tokens.barHeight
          "IconButton", Tokens.iconButton
          "CompactActionTarget", 44.0
          "IconGlyph", Tokens.iconGlyph
          "IconStroke", Tokens.iconStroke
          "ShellInset", Tokens.shellInset
          "LogoSplash", Tokens.logoSplash
          "LogoRadiusRatio", Tokens.logoRadiusRatio
          "LogoSidebar", Tokens.logoSidebar
          "LogoEmpty", Tokens.logoEmpty
          "LogoAvatar", Tokens.logoAvatar
          "TextButtonMinHeight", ControlMetrics.textButtonMinHeight
          "TextButtonPaddingY", ControlMetrics.textButtonPaddingY
          "PendingActionMinWidth", ControlMetrics.pendingActionMinWidth
          "TextFieldTextMinHeight", ControlMetrics.textFieldTextMinHeight
          "TextFieldPaddingY", ControlMetrics.textFieldPaddingY
          "FieldInsetX", ControlMetrics.fieldInsetX
          "FontSizeValueMinWidth", ControlMetrics.fontSizeValueMinWidth
          "AboutKeyMinWidth", ControlMetrics.aboutKeyMinWidth
          "RowMinHeight", ControlMetrics.rowMinHeight
          "SidebarRowMinHeight", ControlMetrics.sidebarRowMinHeight
          "SettingsRowMinHeight", ControlMetrics.settingsRowMinHeight
          "SidebarRowPaddingY", ControlMetrics.sidebarRowPaddingY
          "SidebarStatusLineHeight", ControlMetrics.sidebarStatusLineHeight
          "SidebarStateSlotWidth", ControlMetrics.sidebarStateSlotWidth
          "SidebarStateGlyphSize", ControlMetrics.sidebarStateGlyphSize
          "SidebarRunningDotSize", ControlMetrics.sidebarRunningDotSize
          "StatusDotSize", ControlMetrics.statusDotSize
          "ChipDotSize", ControlMetrics.chipDotSize
          "AccentEdgeWidth", ControlMetrics.accentEdgeWidth
          "SidebarSelectedEdgeWidth", ControlMetrics.sidebarSelectedEdgeWidth
          "QuoteEdgeWidth", ControlMetrics.quoteEdgeWidth
          "SidebarDividerWidth", ControlMetrics.sidebarDividerWidth
          "BorderWidth", ControlMetrics.borderWidth
          "SidebarRowContentSpacing", ControlMetrics.sidebarRowContentSpacing
          "ComposerInputMinHeight", ControlMetrics.composerInputMinHeight
          "ComposerMaxHeight", ControlMetrics.composerMaxHeight
          "ComposerInputMaxHeight", ControlMetrics.composerInputMaxHeight
          "ComposerShellPaddingX", ControlMetrics.composerShellPaddingX
          "ComposerShellPaddingY", ControlMetrics.composerShellPaddingY
          "ComposerAttachmentNameMaxWidth", ControlMetrics.composerAttachmentNameMaxWidth
          "ComposerAttachmentStateWidth", ControlMetrics.composerAttachmentStateWidth
          "ComposerAttachmentIconSlot", ControlMetrics.composerAttachmentIconSlot
          "ComposerModelMaxWidth", ControlMetrics.composerModelMaxWidth
          "ComposerModelCompactMaxWidth", ControlMetrics.composerModelCompactMaxWidth
          "ChipPaddingX", ControlMetrics.chipPaddingX
          "ChipPaddingY", ControlMetrics.chipPaddingY
          "CompactChipPaddingX", ControlMetrics.compactChipPaddingX
          "CompactChipPaddingY", ControlMetrics.compactChipPaddingY
          "TagPaddingX", ControlMetrics.tagPaddingX
          "AttachmentRowPaddingY", ControlMetrics.attachmentRowPaddingY
          "AttachmentNameMaxWidth", ControlMetrics.attachmentNameMaxWidth
          "MenuItemMinHeight", ControlMetrics.menuItemMinHeight
          "MenuItemPaddingY", ControlMetrics.menuItemPaddingY
          "MenuIconSlotWidth", ControlMetrics.menuIconSlotWidth
          "MenuRightSlotMinWidth", ControlMetrics.menuRightSlotMinWidth
          "ToastAccentWidth", ControlMetrics.toastAccentWidth
          "SettingsNavWidth", ControlMetrics.settingsNavWidth
          "SettingsNavMinHeight", ControlMetrics.settingsNavMinHeight
          "SettingsContentMaxWidth", ControlMetrics.settingsContentMaxWidth
          "SelectButtonPaddingY", ControlMetrics.selectButtonPaddingY
          "TextAreaMinHeight", ControlMetrics.textAreaMinHeight
          "TextAreaLongMinHeight", ControlMetrics.textAreaLongMinHeight
          "SpinnerSize", ControlMetrics.spinnerSize
          "SpinnerCompactSize", ControlMetrics.spinnerCompactSize
          "SpinnerStrokeWidth", ControlMetrics.spinnerStrokeWidth
          "SpinnerCanvasSize", ControlMetrics.spinnerCanvasSize
          "ToggleTrackWidth", ControlMetrics.toggleTrackWidth
          "ToggleTrackHeight", ControlMetrics.toggleTrackHeight
          "ToggleKnobSize", ControlMetrics.toggleKnobSize
          "ToggleKnobInsetX", ControlMetrics.toggleKnobInsetX
          "EmptyStateActionMinWidth", ControlMetrics.emptyStateActionMinWidth
          "EmptyStateActionHeight", ControlMetrics.emptyStateActionHeight
          "EmptyStateTitleMinWidth", ControlMetrics.emptyStateTitleMinWidth
          "ScrollToBottomMinWidth", ControlMetrics.scrollToBottomMinWidth
          "RetryButtonMinWidth", ControlMetrics.retryButtonMinWidth
          "CaretWidth", ControlMetrics.caretWidth
          "CaretHeight", ControlMetrics.caretHeight
          "CaretRadius", ControlMetrics.caretRadius
          "ListMarkerDotSize", ControlMetrics.listMarkerDotSize
          "ListMarkerDotRadius", ControlMetrics.listMarkerDotRadius
          "TaskBoxSize", ControlMetrics.taskBoxSize
          "TaskBoxBorderWidth", ControlMetrics.taskBoxBorderWidth
          "TaskCheckGlyphSize", ControlMetrics.taskCheckGlyphSize
          "SkeletonAvatarSize", ControlMetrics.skeletonAvatarSize
          "MessageGap", ContentMetrics.messageGap
          "MessageEndBreathing", ContentMetrics.messageEndBreathing
          "ScrollBottomThreshold", ContentMetrics.scrollBottomThreshold
          "ScrollBottomRevealThreshold", ContentMetrics.scrollBottomRevealThreshold
          "EmptyStateMaxWidth", ContentMetrics.emptyStateMaxWidth
          "ToastMaxWidth", ContentMetrics.toastMaxWidth
          // 旧 ReadingRhythm.captionLineHeight
          "CaptionLineHeight", 17.0
          "UiBodyLineHeight", 20.0
          "ShortcutKeyMinWidth", 136.0
          // 旧 Spacing.Hit.touch
          "TouchTarget", 44.0
          // 旧 ToolApprovalCard.buttonRowMaxWidth
          "ApprovalButtonRowMaxWidth", 360.0
          // 旧 Brand 「万」字比例
          "LogoGlyphRatio", 0.42
          // 旧 LayoutPolicy.menuMaxHeight
          "MenuMaxHeight", 380.0
          // 旧 LayoutPolicy.sidebarSplitterWidth
          "SidebarSplitterWidth", 4.0
          // 旧 LayoutPolicy.startWorkspaceMinHeight
          "StartWorkspaceMinHeight", 600.0
          // 旧 LayoutPolicy.compactBreakpoint
          "CompactBreakpoint", 720.0
          // 旧 ModelSwitcherView 浮层宽
          "ModelSwitcherWidth", 300.0
          // 旧 ShowCommandPalette 对话层宽
          "PaletteDialogWidth", 560.0
          // 旧 Import/ExportDialog 对话层宽
          "TransferDialogWidth", 480.0
          "FormSingleColumnBreakpoint", 440.0
          "DialogContentMaxHeight", 560.0
          "PairingCodeTracking", 6.0
          "QrModuleSize", 6.0
          "SelectCaptionMaxWidth", 260.0
          "SettingsDialogWidth", 520.0
          "SettingsSmallDialogWidth", 480.0
          "ConfirmDialogWidth", 420.0
          "ConnectDialogWidth", 460.0
          "SessionDialogWidth", 520.0 ]
