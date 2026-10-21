namespace Wanxiang2.MarkdownView

open System
open System.Globalization
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions
open Wanxiang.UI
open Wanxiang2.MathFigure
open Wanxiang2.CodeTint
open Wanxiang2.CopyButton

/// 动态资源绑定辅助：外观值全部经主题资源键声明式送达（Metric.* / Space.* /
/// CornerRadius.* / Thickness.* / Padding.* / Margin.* / Tracking.* / FontFamily.* / Motion.Reduced），
/// 主题切换即时生效由资源系统保证，本文件不再挂活值、不再手工订阅。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

    /// 动作时刻读减弱动效资源（无订阅）；缺省按不减弱。
    let reduced (c: Control) : bool =
        match c.TryFindResource(box "Motion.Reduced") with
        | true, (:? bool as r) -> r
        | _ -> false

/// 代码块折行的当前默认值（旧 MarkdownRenderer.DefaultCodeWrap 静态量改为宿主持有的对象）：
/// 初值来自 UI 偏好，块内切换也会更新它，同一屏后续渲染的代码块跟着用户刚做的选择。
type CodeWrapMemory(initial: bool) =
    member val Wrapped = initial with get, set

/// 正文所需的宿主能力：复制、打开链接、公式 HTML（宿主 RichBackend 的 MathHtml）、着色实例、折行记忆。
type MarkdownHost =
    {
        CopyText: string -> unit
        OpenLink: string -> unit
        MathHtml: string * bool -> string option
        Tint: CodeTint
        Wrap: CodeWrapMemory
    }

/// Markdown 块模型 → Avalonia 控件（旧 Controls/MarkdownRenderer.fs）。
/// 标题有真实字号、引用有左缘、列表有悬挂缩进、表格是真表格、代码块是带语言标签和复制按钮的卡片。
/// allowHighlight = false 用于流式进行中的消息：未写完的代码着不准，每个 token 重新着色一次会把机器点着。
type MarkdownView(look: MarkdownMetrics, host: MarkdownHost, fontSize: float, allowHighlight: bool) =

    let handCursor = new Cursor(StandardCursorType.Hand)
    let ink = look.Ink

    let headingSize (level: int) =
        ReadingRhythm.headingFontSize fontSize level

    /// 外边距只有底边一段（段落节奏）：经 Margin 读写，末块清零规则照旧
    let bottomGap (c: Control) (gap: float) =
        c.Margin <- Thickness(0.0, 0.0, 0.0, gap)

    /// 等宽字体走资源键：Run 不是 Control，走它自己的绑定重载
    let monoText (owner: TextBlock) =
        owner.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono")

    let monoRun (run: Run) =
        run.Bind(Run.FontFamilyProperty, Dyn.res "FontFamily.Mono")

    let setIcon (button: Button) (glyph: MarkdownGlyph) (brush: IBrush) =
        let g = look.Glyph glyph brush
        g.HorizontalAlignment <- HorizontalAlignment.Center
        g.VerticalAlignment <- VerticalAlignment.Center
        button.Content <- g

    /// 图标按钮（旧 Ui.iconButton + setSquareTarget）：外观类由 Dress 挂，命中区按布局规则取方形
    let iconButton (glyph: MarkdownGlyph) (tip: string) =
        let button =
            ActionSpot(
                Focusable = true,
                Cursor = handCursor,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            )

        look.Dress MarkdownButton.Icon button
        let target = LayoutPolicy.inlineActionTarget
        button.Width <- target
        button.Height <- target
        button.MinWidth <- target
        button.MinHeight <- target
        setIcon button glyph (ink MarkdownInk.CodeMuted)
        ToolTip.SetTip(button, tip)
        AutomationProperties.SetName(button, tip)
        button

    /// 行内片段渲染成一个可选中的文本块。
    member private this.RenderInlines
        (
            items: MdInline list,
            size: float,
            weight: FontWeight,
            brush: IBrush,
            ?lineHeight: float,
            ?monoFamily: bool,
            ?textAlignment: TextAlignment,
            ?noBoldAccent: bool
        ) : Control =
        let lh = defaultArg lineHeight (ReadingRhythm.proseLineHeight size)

        let block =
            SelectableTextBlock(
                // 主题自带的英文「Copy」浮层会吞掉右键，消息菜单出不来
                ContextFlyout = null,
                TextWrapping = TextWrapping.Wrap,
                FontSize = size,
                FontWeight = weight,
                Foreground = brush,
                LineHeight = lh,
                SelectionBrush = ink MarkdownInk.AccentSoft
            )

        if defaultArg monoFamily false then
            monoText block |> ignore

        match textAlignment with
        | Some align -> block.TextAlignment <- align
        | None -> ()

        let codeRun (text: string) (italic: bool) =
            let run = Run text
            monoRun run |> ignore

            if italic then
                run.FontStyle <- FontStyle.Italic

            run.Foreground <- ink MarkdownInk.Text
            run.Background <- ink MarkdownInk.InlineCode
            run.FontSize <- size - 0.5
            run.BaselineAlignment <- BaselineAlignment.Center
            run

        for item in items do
            match item with
            | MdText(text, bold, italic, strike, code) ->
                if code then
                    // 行内代码靠「等宽 + 底色」区分；颜色留给链接
                    let run = codeRun text italic

                    if bold && not (defaultArg noBoldAccent false) then
                        run.Foreground <- ink MarkdownInk.Accent

                    if strike then
                        run.TextDecorations <- TextDecorations.Strikethrough

                    block.Inlines.Add run
                else
                    let run = Run text
                    // 不用合成加粗：内嵌字体只有 Regular，Skia 合成时 CJK 前进宽度会算错。强调改由强调色承担；
                    // 表格内禁用（表头常整行加粗，全转强调色会变成一整条蓝块）
                    if bold && not (defaultArg noBoldAccent false) then
                        run.Foreground <- ink MarkdownInk.Accent

                    if italic then
                        run.FontStyle <- FontStyle.Italic

                    if strike then
                        run.TextDecorations <- TextDecorations.Strikethrough

                    block.Inlines.Add run
            | MdLink(text, _url) ->
                // 只留视觉；可点激活归 RenderInlineRow 的逐段路径，整段正文不能变成一个链接热区
                let link = Run text
                link.Foreground <- ink MarkdownInk.Accent
                link.TextDecorations <- TextDecorations.Underline
                block.Inlines.Add link
            | MdMath(tex, display) ->
                let mathSize = if display then size + 1.0 else size

                match MathFigures.tryInline look.Math (host.MathHtml(tex, false)) mathSize tex with
                | Some figure ->
                    figure.Bind(Layoutable.MarginProperty, Dyn.res "Margin.MathInline") |> ignore
                    figure.VerticalAlignment <- VerticalAlignment.Center
                    let container = MathFigures.inlineContainer figure
                    container.BaselineAlignment <- BaselineAlignment.Center
                    block.Inlines.Add container
                | None ->
                    // 没有脚本后端或 TeX 有语法错：按行内代码呈现原式
                    block.Inlines.Add(codeRun tex true)
            | MdImage(alt, url) ->
                // 默认不请求远程图片（避免泄露网络信息、tracking pixel）；来源域名写在正文里
                let text, notice = Markdown.imagePlaceholder alt url
                let run = Run text
                run.Foreground <- ink MarkdownInk.TextFaint
                block.Inlines.Add run
                ToolTip.SetTip(block, notice)
                AutomationProperties.SetName(block, notice)
            | MdBreak -> block.Inlines.Add(LineBreak())

        block :> Control

    /// 链接要能点：段落里有链接时拆成「文本 + 可点链接」的 WrapPanel。
    member private this.RenderInlineRow
        (
            items: MdInline list,
            size: float,
            weight: FontWeight,
            brush: IBrush,
            ?lineHeight: float,
            ?monoFamily: bool,
            ?textAlignment: TextAlignment,
            ?noBoldAccent: bool
        ) : Control =
        let hasLink =
            items
            |> List.exists (function
                | MdLink _ -> true
                | _ -> false)

        let inlines items =
            this.RenderInlines(
                items,
                size,
                weight,
                brush,
                ?lineHeight = lineHeight,
                ?monoFamily = monoFamily,
                ?textAlignment = textAlignment,
                ?noBoldAccent = noBoldAccent
            )

        if not hasLink then
            inlines items
        else
            let wrap = WrapPanel(Orientation = Orientation.Horizontal)
            let mutable buffer: MdInline list = []

            let flush () =
                if not (List.isEmpty buffer) then
                    wrap.Children.Add(inlines (List.rev buffer))
                    buffer <- []

            let addLinkChunk (fullText: string) (chunk: string) (url: string) (focusable: bool) =
                let linkText =
                    TextBlock(
                        Text = chunk,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = size,
                        FontWeight = weight,
                        LineHeight = defaultArg lineHeight (ReadingRhythm.proseLineHeight size),
                        Foreground = ink MarkdownInk.Accent,
                        TextDecorations = TextDecorations.Underline,
                        VerticalAlignment = VerticalAlignment.Center
                    )

                if defaultArg monoFamily false then
                    monoText linkText |> ignore

                let link =
                    ActionSpot(
                        Background = Brushes.Transparent,
                        Padding = Thickness 0.0,
                        Cursor = handCursor,
                        Focusable = focusable,
                        VerticalAlignment = VerticalAlignment.Center,
                        Content = linkText
                    )

                if focusable then
                    AutomationProperties.SetName(link, fullText)
                else
                    // 非首段切片从读屏树摘除，否则一个链接被念成碎句
                    AutomationProperties.SetAccessibilityView(link, AccessibilityView.Raw)

                AutomationProperties.SetHelpText(link, url)
                AutomationProperties.SetControlTypeOverride(link, Nullable Peers.AutomationControlType.Hyperlink)
                ToolTip.SetTip(link, url)
                link.OnInvoke(fun () -> host.OpenLink url)
                wrap.Children.Add link

            for item in items do
                match item with
                | MdLink(text, url) ->
                    flush ()
                    // WrapPanel 只能在 child 之间换行：超长链接拆成同 URL 的多段，只有首段占 Tab 停靠
                    if text.Length <= 40 then
                        addLinkChunk text text url true
                    else
                        let chunks = LinkChunks.rebalance (LinkChunks.safeChunk 40 text)
                        chunks |> List.iteri (fun i chunk -> addLinkChunk text chunk url (i = 0))
                | other -> buffer <- other :: buffer

            flush ()
            wrap :> Control

    member private _.RenderCode(language: string, code: string) : Control =
        let block =
            SelectableTextBlock(
                ContextFlyout = null,
                FontSize = fontSize - 1.5,
                Foreground = ink MarkdownInk.CodeText,
                LineHeight = ReadingRhythm.technicalLineHeight (fontSize - 1.5),
                SelectionBrush = ink MarkdownInk.AccentSoft
            )

        monoText block |> ignore
        block.Bind(Layoutable.MarginProperty, Dyn.res "Margin.Block") |> ignore

        let brushOf kind : IBrush =
            match kind with
            | CodeKeyword -> ink MarkdownInk.CodeKeyword
            | CodeString -> ink MarkdownInk.CodeString
            | CodeComment -> ink MarkdownInk.CodeComment
            | CodeNumber -> ink MarkdownInk.CodeNumber
            | CodeType -> ink MarkdownInk.CodeType
            | CodeFunction -> ink MarkdownInk.CodeFunction
            | CodeMeta -> ink MarkdownInk.CodeMeta
            | CodeVariable -> ink MarkdownInk.CodeVariable
            | CodeOperator -> ink MarkdownInk.CodeOperator
            | CodeAddition -> ink MarkdownInk.CodeAddition
            | CodeDeletion -> ink MarkdownInk.CodeDeletion
            | CodePlain -> ink MarkdownInk.CodeText
        // 着色输入截到常量预算，超出部分按纯文本追加（复制仍拿完整原文）；按字素簇安全截断
        let highlightBudget = 20000

        let head, rest =
            if String.IsNullOrEmpty code || code.Length <= highlightBudget then
                code, ""
            else
                match LinkChunks.safeChunk highlightBudget code with
                | first :: _ when first.Length < code.Length -> first, code.Substring(first.Length)
                | _ -> code.Substring(0, highlightBudget), code.Substring(highlightBudget)

        for token in host.Tint.Tokenize(allowHighlight, language, head) do
            let run = Run token.text
            run.Foreground <- brushOf token.kind

            if token.kind = CodeComment then
                run.FontStyle <- FontStyle.Italic

            block.Inlines.Add run

        if not (String.IsNullOrEmpty rest) then
            let r = Run rest
            r.Foreground <- ink MarkdownInk.CodeText
            block.Inlines.Add r

        let scroll =
            ScrollViewer(Content = block, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled)
        // 超长单围栏先限高在 expandedDetailMaxHeight，需要全文再展开（120 行在任何字号下都远超 360px）
        let lineCount = 1 + (code |> Seq.filter ((=) '\n') |> Seq.length)
        let huge = lineCount > 120

        if huge then
            scroll.MaxHeight <- LayoutPolicy.expandedDetailMaxHeight
            scroll.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto

        let langName =
            if String.IsNullOrWhiteSpace language then
                "text"
            else
                language.ToLowerInvariant()

        let langLabel =
            TextBlock(
                Text = langName,
                FontWeight = FontWeight.Medium,
                Foreground = ink MarkdownInk.CodeMuted,
                VerticalAlignment = VerticalAlignment.Center
            )

        langLabel.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontMicro") |> ignore

        langLabel.Bind(TextBlock.LetterSpacingProperty, Dyn.res "Tracking.Emphasis")
        |> ignore

        let copyButton = iconButton MarkdownGlyph.Copy "复制代码"
        let copyName = sprintf "复制%s代码" langName
        AutomationProperties.SetName(copyButton, copyName)
        AutomationProperties.SetHelpText(copyButton, copyName)
        let confirm = CopyButton(copyButton, look.Copy)

        confirm.StateChanged.Add(fun state ->
            match state with
            | CopyState.Default ->
                setIcon copyButton MarkdownGlyph.Copy (ink MarkdownInk.CodeMuted)
                ToolTip.SetTip(copyButton, "复制代码")
                AutomationProperties.SetName(copyButton, copyName)
                AutomationProperties.SetHelpText(copyButton, copyName)
            | CopyState.Confirmed
            | CopyState.Fading ->
                setIcon copyButton MarkdownGlyph.Check (ink MarkdownInk.Success)
                ToolTip.SetTip(copyButton, "已复制")
                AutomationProperties.SetName(copyButton, "已复制"))
        // 确认淡出：往既有过渡集合追加不透明度补间，不改几何；时长随减弱动效
        let fade = Avalonia.Animation.DoubleTransition(Property = Visual.OpacityProperty)

        if isNull copyButton.Transitions then
            copyButton.Transitions <- Avalonia.Animation.Transitions()

        copyButton.Transitions.Add fade
        // 淡出时长是主题的静态时长档；减弱动效在激活时刻读资源键（无订阅）
        fade.Duration <- look.Copy.Fade

        copyButton.OnInvoke(fun () ->
            host.CopyText code
            confirm.Copied(Dyn.reduced copyButton))

        // 长行折行是逐块开关，初值取自折行记忆
        let wrapButton = iconButton MarkdownGlyph.TextWrap "长行折行 / 横向滚动"
        let mutable wrapped = host.Wrap.Wrapped

        let applyWrap () =
            block.TextWrapping <- (if wrapped then TextWrapping.Wrap else TextWrapping.NoWrap)

            scroll.HorizontalScrollBarVisibility <-
                (if wrapped then
                     ScrollBarVisibility.Disabled
                 else
                     ScrollBarVisibility.Auto)

            setIcon
                wrapButton
                MarkdownGlyph.TextWrap
                (if wrapped then
                     ink MarkdownInk.Accent
                 else
                     ink MarkdownInk.CodeMuted)
            // 读屏名称跟随当前状态，按下去会发生什么一听便知
            AutomationProperties.SetName(wrapButton, if wrapped then "取消折行" else "长行折行")
            AutomationProperties.SetHelpText(wrapButton, if wrapped then "取消折行，恢复横向滚动" else "长行折行，在块内自动换行")

        wrapButton.OnInvoke(fun () ->
            wrapped <- not wrapped
            host.Wrap.Wrapped <- wrapped
            applyWrap ())

        applyWrap ()

        let header =
            let actions =
                StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)

            actions.Bind(StackPanel.SpacingProperty, Dyn.res "Space.XXs") |> ignore
            actions.Children.Add wrapButton
            actions.Children.Add copyButton
            let dock = DockPanel(LastChildFill = false)
            DockPanel.SetDock(langLabel, Dock.Left)
            DockPanel.SetDock(actions, Dock.Right)
            dock.Children.Add langLabel
            dock.Children.Add actions

            let b =
                Border(
                    Background = ink MarkdownInk.CodeHeaderBg,
                    BorderBrush = ink MarkdownInk.CodeBorder,
                    Child = dock
                )

            b.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.BorderBottom")
            |> ignore

            b.Bind(Border.PaddingProperty, Dyn.res "Padding.CodeHeader") |> ignore
            b

        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        stack.Children.Add header
        stack.Children.Add scroll

        if huge then
            let expand =
                ActionSpot(
                    Focusable = true,
                    Cursor = handCursor,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                )

            let label =
                TextBlock(
                    Text = "展开全部",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                )

            expand.Content <- label
            look.Dress MarkdownButton.Ghost expand
            expand.Bind(Layoutable.MarginProperty, Dyn.res "Margin.CodeExpand") |> ignore
            AutomationProperties.SetName(expand, "展开全部")
            // 展开态报成标准 expander 语义（旧 SetExpanded：ItemStatus 已展开 / 已折叠）
            AutomationProperties.SetItemStatus(expand, "已折叠")
            let mutable full = false

            expand.OnInvoke(fun () ->
                full <- not full

                scroll.MaxHeight <-
                    if full then
                        Double.PositiveInfinity
                    else
                        LayoutPolicy.expandedDetailMaxHeight

                let text = if full then "收起" else "展开全部"
                label.Text <- text
                AutomationProperties.SetName(expand, text)
                AutomationProperties.SetItemStatus(expand, if full then "已展开" else "已折叠"))

            stack.Children.Add expand

        let root =
            Border(
                Background = ink MarkdownInk.CodeBg,
                BorderBrush = ink MarkdownInk.CodeBorder,
                ClipToBounds = true,
                Child = stack
            )

        root.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.BorderThin")
        |> ignore

        root.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusMd")
        |> ignore

        bottomGap root ReadingRhythm.paragraphGap
        root :> Control

    member private this.RenderTable(header: MdInline list list, rows: MdInline list list list) : Control =
        let columnCount = (header :: rows) |> List.map List.length |> List.fold max 0

        if columnCount = 0 then
            Border() :> Control
        else
            let grid = Grid()
            // 列宽最小阶梯是内容策略（防窄表被压扁的排版下限），不是外观值
            let minColumnWidth =
                match columnCount with
                | 1
                | 2 -> 100.0
                | 3 -> 90.0
                | 4 -> 80.0
                | _ -> 120.0

            for _ in 1..columnCount do
                grid.ColumnDefinitions.Add(
                    ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star), MinWidth = minColumnWidth)
                )

            let isNumericCell (items: MdInline list) =
                match items with
                | [ MdText(text, _, _, _, false) ] ->
                    let s = text.Trim()

                    if String.IsNullOrEmpty s then
                        false
                    else
                        let clean =
                            s.TrimStart('$', '¥', '€', '£', '+', '-').TrimEnd('%').Replace(",", "").Trim()

                        fst (Double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture))
                | _ -> false

            let isNumericColumn =
                Array.init columnCount (fun c ->
                    not (List.isEmpty rows)
                    && rows
                       |> List.forall (fun row ->
                           c >= List.length row
                           || (match row[c] with
                               | [] -> true
                               | cells -> isNumericCell cells)))

            let mutable gridRow = 0
            let mutable dataRow = 0
            let total = (if List.isEmpty header then 0 else 1) + List.length rows

            let addRow (cells: MdInline list list) (isHeader: bool) =
                grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))

                for col in 0 .. columnCount - 1 do
                    let content = if col < List.length cells then cells[col] else []

                    let isNumeric =
                        if isHeader then
                            isNumericColumn[col] && (isNumericCell content || List.length content <= 1)
                        else
                            isNumericCell content

                    let cell =
                        this.RenderInlineRow(
                            content,
                            fontSize - 0.5,
                            (if isHeader then FontWeight.Medium else FontWeight.Normal),
                            ink MarkdownInk.TextMuted,
                            monoFamily = (isNumeric && not isHeader),
                            lineHeight = ReadingRhythm.proseLineHeight (fontSize - 0.5),
                            ?textAlignment = (if isNumeric then Some TextAlignment.Right else None),
                            noBoldAccent = true
                        )

                    let hasBreak =
                        content
                        |> List.exists (function
                            | MdBreak -> true
                            | _ -> false)

                    cell.VerticalAlignment <-
                        if not isHeader && hasBreak then
                            VerticalAlignment.Top
                        else
                            VerticalAlignment.Center

                    cell.HorizontalAlignment <-
                        if isNumeric then
                            HorizontalAlignment.Right
                        else
                            HorizontalAlignment.Left
                    // 超长无断点 token 的格子套一层横向滚动
                    let cellContent: Control =
                        if columnCount <= 4 && Markdown.hasLongToken content then
                            ScrollViewer(
                                Content = cell,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                            )
                        else
                            cell
                    // 隔行微底色 + 行间单 hairline，不画逐格纵线
                    let background: IBrush =
                        if isHeader then ink MarkdownInk.TableHeader
                        elif dataRow % 2 = 1 then ink MarkdownInk.TableStripe
                        else Brushes.Transparent

                    let notLast = gridRow < total - 1

                    let hostCell =
                        Border(
                            BorderBrush = ink MarkdownInk.Hairline,
                            Background = background,
                            ClipToBounds = true,
                            UseLayoutRounding = true,
                            Child = cellContent
                        )

                    hostCell.Bind(Border.PaddingProperty, Dyn.res "Padding.Block") |> ignore

                    hostCell.Bind(
                        Border.BorderThicknessProperty,
                        Dyn.res (
                            if notLast then
                                "Thickness.BorderBottom"
                            else
                                "Thickness.None"
                        )
                    )
                    |> ignore

                    Grid.SetRow(hostCell, gridRow)
                    Grid.SetColumn(hostCell, col)
                    grid.Children.Add hostCell

                gridRow <- gridRow + 1

                if not isHeader then
                    dataRow <- dataRow + 1

            if not (List.isEmpty header) then
                addRow header true

            for row in rows do
                addRow row false

            let content: Control =
                if columnCount > 4 then
                    ScrollViewer(
                        Content = grid,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                    )
                else
                    grid

            let frame =
                Border(
                    BorderBrush = ink MarkdownInk.HairlineStrong,
                    ClipToBounds = true,
                    UseLayoutRounding = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = content
                )

            frame.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.BorderThin")
            |> ignore

            frame.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusMd")
            |> ignore

            bottomGap frame ReadingRhythm.paragraphGap
            frame :> Control

    member private this.RenderMath(tex: string) : Control list =
        match MathFigures.tryBlock look.Math (host.MathHtml(tex, true)) (fontSize + 1.0) tex with
        | Some control ->
            control.HorizontalAlignment <- HorizontalAlignment.Center
            bottomGap control ReadingRhythm.paragraphGap
            [ control ]
        | None -> [ this.RenderCode("tex", tex) ]

    /// 一个块渲染成若干控件（流式面板逐块挂载）。
    member this.RenderBlock(block: MdBlock, ?inQuote: bool) : Control list =
        let insideQuote = defaultArg inQuote false

        let textColor =
            if insideQuote then
                ink MarkdownInk.TextMuted
            else
                ink MarkdownInk.Text

        let textLineHeight =
            if insideQuote then
                ReadingRhythm.secondaryLineHeight fontSize
            else
                ReadingRhythm.proseLineHeight fontSize

        let listStack () =
            let s =
                StackPanel(Orientation = Orientation.Vertical, Spacing = ReadingRhythm.listItemGap)

            bottomGap s ReadingRhythm.listBlockGap
            s

        match block with
        | MdMathBlock tex -> this.RenderMath tex
        // Markdig 把独立成段的 $$…$$ 也解析成行内公式；这里还原成块，否则不会居中
        | MdParagraph [ MdMath(tex, true) ] -> this.RenderMath tex
        | MdHeading(level, items) ->
            let control =
                this.RenderInlineRow(items, headingSize level, FontWeight.Medium, textColor)

            control.Margin <- Thickness(0.0, ReadingRhythm.headingBefore level, 0.0, ReadingRhythm.headingAfter level)
            [ control ]
        | MdParagraph items ->
            let control =
                this.RenderInlineRow(items, fontSize, FontWeight.Normal, textColor, lineHeight = textLineHeight)

            bottomGap control ReadingRhythm.paragraphGap
            [ control ]
        | MdList(ordered, items) ->
            let stack = listStack ()

            for (depth, bullet, content) in items do
                // 有序号用文字；无序用绘制圆点（字形圆点在内嵌字体里又小又偏上）
                let marker: Control =
                    if ordered then
                        let t =
                            TextBlock(
                                Text = bullet,
                                FontSize = fontSize - 0.5,
                                Foreground = ink MarkdownInk.TextMuted,
                                MinWidth = ReadingRhythm.listMarkerWidth,
                                TextAlignment = TextAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top
                            )

                        let markerBody: Control =
                            t.Bind(Layoutable.MarginProperty, Dyn.res "Margin.GapRightMd") |> ignore

                            if depth = 0 then
                                t :> Control
                            // 层级缩进是每行的静态量：套一层左边距壳，右间距仍走资源键
                            else
                                Border(
                                    Margin = Thickness(float depth * ReadingRhythm.listIndentStep, 0.0, 0.0, 0.0),
                                    Child = t
                                )
                                :> Control

                        markerBody
                    else
                        let dot =
                            Border(Background = ink MarkdownInk.TextMuted, VerticalAlignment = VerticalAlignment.Center)

                        dot.Bind(Layoutable.WidthProperty, Dyn.res "Metric.ListMarkerDotSize") |> ignore

                        dot.Bind(Layoutable.HeightProperty, Dyn.res "Metric.ListMarkerDotSize")
                        |> ignore

                        dot.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.ListMarkerDot")
                        |> ignore

                        let slot =
                            Border(
                                Width = ReadingRhythm.listMarkerWidth + float depth * ReadingRhythm.listIndentStep,
                                Margin = Thickness(0.0, fontSize * 0.55, 0.0, 0.0),
                                VerticalAlignment = VerticalAlignment.Top,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Child = dot
                            )

                        slot.Bind(Border.PaddingProperty, Dyn.res "Thickness.GapRightMd") |> ignore
                        slot

                let body =
                    this.RenderInlineRow(content, fontSize, FontWeight.Normal, textColor, lineHeight = textLineHeight)

                let row = DockPanel()
                DockPanel.SetDock(marker, Dock.Left)
                row.Children.Add marker
                row.Children.Add body
                stack.Children.Add row

            [ stack :> Control ]
        | MdTask items ->
            let stack = listStack ()

            for (isChecked, content) in items do
                let box =
                    Border(
                        BorderBrush =
                            (if isChecked then
                                 ink MarkdownInk.Accent
                             else
                                 ink MarkdownInk.Line),
                        Background =
                            (if isChecked then
                                 ink MarkdownInk.Accent
                             else
                                 Brushes.Transparent :> IBrush),
                        VerticalAlignment = VerticalAlignment.Top
                    )

                box.Bind(Layoutable.WidthProperty, Dyn.res "Metric.TaskBoxSize") |> ignore
                box.Bind(Layoutable.HeightProperty, Dyn.res "Metric.TaskBoxSize") |> ignore
                box.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusXs") |> ignore

                box.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.TaskBoxEdge")
                |> ignore

                box.Bind(Layoutable.MarginProperty, Dyn.res "Margin.GapRightMd") |> ignore
                // 自绘方框补 CheckBox 语义与状态名，读屏能听到任务是否完成
                AutomationProperties.SetControlTypeOverride(box, Nullable Peers.AutomationControlType.CheckBox)
                AutomationProperties.SetName(box, if isChecked then "已完成任务" else "未完成任务")

                if isChecked then
                    let mark = look.Glyph MarkdownGlyph.Check (ink MarkdownInk.TextOnAccent)

                    mark.Bind(Layoutable.WidthProperty, Dyn.res "Metric.TaskCheckGlyphSize")
                    |> ignore

                    mark.Bind(Layoutable.HeightProperty, Dyn.res "Metric.TaskCheckGlyphSize")
                    |> ignore

                    box.Child <- mark

                let body =
                    this.RenderInlineRow(
                        content,
                        fontSize,
                        FontWeight.Normal,
                        (if isChecked then ink MarkdownInk.TextMuted else textColor),
                        lineHeight = textLineHeight
                    )

                let row = DockPanel()
                DockPanel.SetDock(box, Dock.Left)
                row.Children.Add box
                row.Children.Add body
                stack.Children.Add row

            [ stack :> Control ]
        | MdQuote inner ->
            let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)

            for child in inner do
                for control in this.RenderBlock(child, inQuote = true) do
                    stack.Children.Add control
            // 首子项清顶外边距、末子项清底外边距，内部留白紧凑一致
            if stack.Children.Count > 0 then
                let first = stack.Children[0]
                let fm = first.Margin
                first.Margin <- Thickness(fm.Left, 0.0, fm.Right, fm.Bottom)
                let last = stack.Children[stack.Children.Count - 1]
                let lm = last.Margin
                last.Margin <- Thickness(lm.Left, lm.Top, lm.Right, 0.0)
            // 单层容器：左侧强调条 + 微底色；嵌套引用向左退一步
            let card =
                Border(
                    Background = ink MarkdownInk.SurfaceContainer,
                    BorderBrush = ink MarkdownInk.Accent,
                    ClipToBounds = true,
                    Child = stack
                )

            card.Bind(Border.BorderThicknessProperty, Dyn.res "Thickness.QuoteEdge")
            |> ignore

            card.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.QuoteRight")
            |> ignore

            card.Bind(Border.PaddingProperty, Dyn.res "Padding.Quote") |> ignore
            // 外边距只在构造时读一次资源：父级（引用首末项、末块清零）会改写它，活绑定会把改写冲掉
            card.Margin <-
                if insideQuote then
                    match card.TryFindResource(box "Margin.QuoteNested") with
                    | true, (:? Thickness as m) -> m
                    | _ -> Thickness(ReadingRhythm.paragraphGap, 0.0, 0.0, ReadingRhythm.paragraphGap)
                else
                    Thickness(0.0, 0.0, 0.0, ReadingRhythm.paragraphGap)

            [ card :> Control ]
        | MdCode(language, code) -> [ this.RenderCode(language, code) ]
        | MdRule ->
            let rule =
                Border(
                    Background = ink MarkdownInk.BorderSoft,
                    Margin = Thickness(0.0, ReadingRhythm.headingBefore 3, 0.0, ReadingRhythm.headingBefore 3),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                )

            rule.Bind(Layoutable.HeightProperty, Dyn.res "Metric.BorderWidth") |> ignore
            [ rule :> Control ]
        | MdTable(header, rows) -> [ this.RenderTable(header, rows) ]

    /// 渲染整段 Markdown 到一个纵向面板；末块的下边距清零（否则气泡里留空隙）。
    member this.Render(blocks: MdBlock list) : Control =
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)

        for block in blocks do
            for control in this.RenderBlock block do
                stack.Children.Add control

        if stack.Children.Count > 0 then
            let last = stack.Children[stack.Children.Count - 1]
            let m = last.Margin
            last.Margin <- Thickness(m.Left, m.Top, m.Right, 0.0)

        stack :> Control

    member this.RenderText(raw: string) : Control = this.Render(Markdown.parse raw)
