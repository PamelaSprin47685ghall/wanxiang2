namespace Wanxiang2.GeneralPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState
open Wanxiang2.DevicesPane
open Wanxiang2.DevicesPane.ButtonExtensions

/// 生成默认值表单的文字（组合者由 GenerationForm.floatText 等得出）
type GenerationText =
    {
        Temperature: string
        TopP: string
        MaxTokens: string
        ContextMessages: string
        ToolRounds: string
        Instructions: string
        AutoTitle: bool
    }

/// 一条对话预设
type PresetRow =
    {
        Id: string
        Name: string
        /// Presets.describe 的一行说明
        Detail: string
    }

/// 一项主机检查
type AboutCheck =
    {
        Name: string
        Label: string
        Ok: bool
        Detail: string
    }

/// 「检查主机」的状态
[<RequireQualifiedAccess>]
type AboutDoctor =
    | NotRun
    | Running
    | Unavailable of reason: string
    | Report of ok: bool * checks: AboutCheck list * timeText: string

/// 「对话默认值 + 关于与系统诊断」两块分区的一帧（单入口 Render 的输入）。
/// 分区各取自己关心的字段：GenerationSection 用 Status / Pending / CanSave / Presets，
/// AboutSection 用 AboutRows / ConfigIssue / Doctor。
type GeneralHealth =
    {
        /// 主机配置问题（摘要, 明细）
        ConfigIssue: (string * string list) option
        /// 「检查主机」的状态
        Doctor: AboutDoctor
    }

module GeneralHealth =
    let empty: GeneralHealth =
        {
            ConfigIssue = None
            Doctor = AboutDoctor.NotRun
        }

type GeneralSectionVS =
    {
        /// 生成设置的状态行
        Status: string
        /// 保存往返中
        Pending: bool
        /// 当前是否可以保存
        CanSave: bool
        /// 对话预设行
        Presets: PresetRow list
        /// 关于页信息行（键, 值）
        AboutRows: (string * string) list
        /// 主机状态
        Health: GeneralHealth
    }

module GeneralSectionVS =
    let empty: GeneralSectionVS =
        {
            Status = ""
            Pending = false
            CanSave = false
            Presets = []
            AboutRows = []
            Health = GeneralHealth.empty
        }

module internal Layout =
    /// 两列表单：宽时并排，窄于断点退化为单列（旧 Ui.twoColumnForm）
    let twoColumns (fields: Control list) : Grid =
        let grid = Grid()
        grid.ColumnSpacing <- 20.0
        grid.RowSpacing <- 16.0
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))

        for _ in fields do
            grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))

        for f in fields do
            grid.Children.Add f

        let mutable breakpoint = 0.0

        let apply () =
            let width = grid.Bounds.Width
            let single = width > 0.0 && width < breakpoint

            grid.ColumnDefinitions[1].Width <-
                (if single then
                     GridLength(0.0)
                 else
                     GridLength(1.0, GridUnitType.Star))

            fields
            |> List.iteri (fun i c ->
                Grid.SetRow(c, (if single then i else i / 2))
                Grid.SetColumn(c, (if single then 0 else i % 2)))

        apply ()

        grid.PropertyChanged.Add(fun a ->
            if a.Property = Visual.BoundsProperty then
                apply ())

        grid

/// 设置 → 对话默认值（旧 SettingsGeneral.BuildGeneration 与预设列表、导入入口的界面部分）。
/// 草稿状态（GenerationDraft）、校验（GenerationForm + FieldCheck）、写回、预设对话框由组合者接。
type GenerationSection(validationFor: TextBox -> Control option, ?_glyphOf: string -> IBrush -> Control) =
    let edited = Event<unit>()
    let save = Event<unit>()
    let discard = Event<unit>()
    let rename = Event<string>()
    let delete = Event<string>()
    /// 宿主注入的导入回调：注入即显示「导入对话…」
    let mutable importHandler: (unit -> unit) option = None
    /// 上一帧的预设行：内容没变就不重建（保住用户在预设行上的焦点）
    let mutable lastPresets: PresetRow list = []
    let mutable presetsBuilt = false

    let field label hint placeholder =
        let box = Ui.textField placeholder
        AutomationProperties.SetName(box, label)
        box, Ui.fieldGroup label hint box (validationFor box)

    let temperatureBox, temperatureField =
        field "Temperature" "越高越发散。留空则用模型服务默认值。" "留空 = 使用模型服务默认"

    let topPBox, topPField = field "Top P" "核采样阈值，0–1。" "0–1，留空不设"
    let maxTokensBox, maxTokensField = field "最大输出 token" "限制单次回复长度。" "留空不限制"
    let contextBox, contextField = field "上下文消息上限" "每次请求最多携带多少条历史，防止长对话撞上模型上限。" "200"
    let toolRoundsBox, toolRoundsField = field "工具调用轮数上限" "模型连续调用工具超过这个轮数就中止本次生成。" "12"

    let instructionsBox =
        let b = Ui.textArea "新对话的默认系统指令（可留空）" 72.0
        AutomationProperties.SetName(b, "默认系统指令")
        b

    let summary = Ui.caption ""
    let status = Ui.caption ""

    let autoTitleRow, readAutoTitle, writeAutoTitle =
        Ui.switchRow "自动生成对话标题" "首轮对话后用同一个模型起一个短标题；失败时回落到你的第一句话。" true (fun _ -> edited.Trigger())

    let saveButton = Ui.button "primary" "保存默认值" save.Trigger
    let discardButton = Ui.button "ghost" "放弃修改" discard.Trigger

    let presetsPanel =
        StackPanel(Orientation = Orientation.Vertical, Name = "settings-presets")

    let importBlock = StackPanel(Orientation = Orientation.Vertical, IsVisible = false)

    let numberBoxes =
        [ temperatureBox; topPBox; maxTokensBox; contextBox; toolRoundsBox ]

    let view =
        summary.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        summary.IsVisible <- false
        summary.Focusable <- true
        AutomationProperties.SetName(summary, "表单错误摘要")
        AutomationProperties.SetLiveSetting(summary, AutomationLiveSetting.Assertive)
        AutomationProperties.SetName(status, "生成设置保存状态")
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)

        for box in numberBoxes @ [ instructionsBox ] do
            box.PropertyChanged.Add(fun e ->
                if e.Property = TextBox.TextProperty then
                    edited.Trigger())

        AutomationProperties.SetHelpText(saveButton, "保存默认值（Ctrl / ⌘ + Enter）")
        ToolTip.SetTip(saveButton, "保存默认值（Ctrl / ⌘ + Enter）")
        saveButton.MinWidth <- 96.0
        saveButton.HorizontalAlignment <- HorizontalAlignment.Left
        presetsPanel.Spacing <- 12.0
        importBlock.Spacing <- 12.0

        let grid =
            Layout.twoColumns [ temperatureField; topPField; maxTokensField; contextField; toolRoundsField ]

        let importButton =
            Ui.button "secondary" "导入对话…" (fun () -> importHandler |> Option.iter (fun h -> h ()))

        importButton.Name <- "settings-import"
        importButton.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetName(importButton, "导入对话")
        AutomationProperties.SetHelpText(importButton, "从导出的 JSON 文件新建一个对话")

        for c in
            [
                Ui.hairline () :> Control
                Ui.label "导入对话"
                Ui.caption "从导出的 JSON 文件在这台主机上新建一个对话，先预览再确认。"
                importButton
            ] do
            importBlock.Children.Add c

        let header = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        header.Children.Add(Ui.heading "对话默认值")
        header.Children.Add(Ui.caption "保存到当前主机，供所有客户端新建对话时使用。不会改动已有对话。")
        let cardPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        cardPanel.Children.Add grid
        cardPanel.Children.Add(Ui.hairline ())
        cardPanel.Children.Add(Ui.fieldGroup "默认系统指令" "会作为 system 消息随每次请求发送。" instructionsBox None)
        cardPanel.Children.Add(Ui.hairline ())
        cardPanel.Children.Add autoTitleRow
        let card = Ui.card cardPanel
        let actions = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        actions.Children.Add saveButton
        actions.Children.Add discardButton
        let presetsStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        presetsStack.Children.Add(Ui.hairline ())
        presetsStack.Children.Add presetsPanel
        let form = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        form.Children.Add header
        form.Children.Add summary
        form.Children.Add card
        form.Children.Add(Ui.hairline ())
        form.Children.Add status
        form.Children.Add actions
        form.Children.Add presetsStack
        form.Children.Add importBlock

        let ctrl (e: KeyEventArgs) =
            e.KeyModifiers.HasFlag KeyModifiers.Control
            || e.KeyModifiers.HasFlag KeyModifiers.Meta

        form.KeyDown.Add(fun e ->
            if e.Key = Key.Enter && ctrl e then
                e.Handled <- true
                save.Trigger())
        // 五个数值框回车即保存；指令框的回车是换行
        for box in numberBoxes do
            box.KeyDown.Add(fun e ->
                if e.Key = Key.Enter && not (ctrl e) then
                    e.Handled <- true
                    save.Trigger())

        form

    member _.View = view :> Control
    member _.TemperatureBox = temperatureBox
    member _.TopPBox = topPBox
    member _.MaxTokensBox = maxTokensBox
    member _.ContextBox = contextBox
    member _.ToolRoundsBox = toolRoundsBox
    member _.InstructionsBox = instructionsBox
    /// 五个数值框（校验顺序）
    member _.NumberBoxes = numberBoxes
    member _.Summary = summary
    member _.AutoTitle = readAutoTitle ()

    /// 当前文字
    member _.Read() : GenerationText =
        let t (b: TextBox) = if isNull b.Text then "" else b.Text

        {
            Temperature = t temperatureBox
            TopP = t topPBox
            MaxTokens = t maxTokensBox
            ContextMessages = t contextBox
            ToolRounds = t toolRoundsBox
            Instructions = t instructionsBox
            AutoTitle = readAutoTitle ()
        }

    /// 填表（会发 Edited，与旧实现相同；GenerationDraft 在回填回调里忽略它）
    member _.Fill(v: GenerationText) =
        temperatureBox.Text <- v.Temperature
        topPBox.Text <- v.TopP
        maxTokensBox.Text <- v.MaxTokens
        contextBox.Text <- v.ContextMessages
        toolRoundsBox.Text <- v.ToolRounds
        instructionsBox.Text <- v.Instructions
        writeAutoTitle v.AutoTitle

    /// 单入口幂等渲染：状态行、保存往返、对话预设一次性投影
    member _.Render(vs: GeneralSectionVS) =
        status.Text <- vs.Status
        // 保存往返中：保存键换「正在保存…」并禁用；结束后按 canSave 决定可用；放弃键往返中禁用
        let pending = vs.Pending
        let text = if pending then "正在保存…" else "保存默认值"
        saveButton.ApplyText text
        saveButton.ApplyEnabled(not pending && vs.CanSave)
        AutomationProperties.SetHelpText(saveButton, (if pending then text else "保存默认值（Ctrl / ⌘ + Enter）"))
        discardButton.ApplyEnabled(not pending)
        let presets = vs.Presets

        if not presetsBuilt || presets <> lastPresets then
            (presetsBuilt <- true
             lastPresets <- presets
             presetsPanel.Children.Clear()
             presetsPanel.Children.Add(Ui.label "对话预设")

             if presets.IsEmpty then
                 presetsPanel.Children.Add(Ui.caption "预设记下一段对话的模型、系统指令、生成参数和工具。在对话顶部的「⋯」里选「存为预设」，以后新建对话时一键套用。")
             else
                 presetsPanel.Children.Add(Ui.caption "保存在当前主机上，所有设备都能用。改动预设不会影响已经建好的对话。")

                 for p in presets do
                     let name = TextBlock(Text = p.Name, FontSize = 14.0)
                     name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
                     name.TextTrimming <- TextTrimming.CharacterEllipsis
                     let detail = Ui.caption p.Detail
                     detail.TextTrimming <- TextTrimming.CharacterEllipsis
                     detail.TextWrapping <- TextWrapping.NoWrap
                     let textStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
                     textStack.Children.Add name
                     textStack.Children.Add detail
                     let r = Ui.button "ghost" "重命名" (fun () -> rename.Trigger p.Id)
                     let d = Ui.button "ghost" "删除" (fun () -> delete.Trigger p.Id)
                     AutomationProperties.SetName(r, sprintf "重命名预设 %s" p.Name)
                     AutomationProperties.SetName(d, sprintf "删除预设 %s" p.Name)
                     let buttons = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0)
                     buttons.Children.Add r
                     buttons.Children.Add d
                     let row = DockPanel(LastChildFill = true, Name = "preset-row")
                     DockPanel.SetDock(buttons, Dock.Right)
                     row.Children.Add buttons
                     row.Children.Add textStack
                     presetsPanel.Children.Add row)

    /// 宿主接了导入才显示「导入对话…」：注入即显示，可见性归这里独占
    member _.WireImportHandler(handler: unit -> unit) =
        importHandler <- Some handler
        importBlock.IsVisible <- true

    member _.PresetsPanel = presetsPanel
    /// 任一字段或开关被改
    member _.Edited = edited.Publish
    /// 保存键、Ctrl/⌘+Enter、数值框回车（往返中也会发，组合者按 GenerationDraft.CanSave 拦）
    member _.SaveRequested = save.Publish
    member _.DiscardRequested = discard.Publish
    member _.PresetRenameClicked = rename.Publish
    member _.PresetDeleteClicked = delete.Publish

/// 外观页的开关
[<RequireQualifiedAccess>]
type AppearanceSwitch =
    | EnterSends
    | CollapseReasoning
    | CodeWrap
    | ShowArchived
    | ReduceMotion
    | PersistDrafts

/// 外观页的一帧（组合者由 UiPrefs 得出）
type AppearanceState =
    {
        ThemeLabel: string
        /// 「14.0 pt」
        FontSizeText: string
        CanShrink: bool
        CanGrow: bool
        EnterSends: bool
        CollapseReasoning: bool
        CodeWrap: bool
        ShowArchived: bool
        ReduceMotion: bool
        PersistDrafts: bool
        /// 本机草稿概况
        DraftSummary: string
    }

/// 设置 → 外观与交互、本机草稿（旧 SettingsGeneral.BuildAppearance / BuildLocalDrafts 的界面部分）
type AppearanceSection(?_glyphOf: string -> IBrush -> Control) =
    let themeClicked = Event<Control>()
    let fontStep = Event<float>()
    let toggled = Event<AppearanceSwitch * bool>()
    let clearDrafts = Event<unit>()

    let row which title hint =
        let r, _, write =
            Ui.switchRow title hint false (fun v -> toggled.Trigger((which, v)))

        r, write

    let enterRow, setEnter =
        row AppearanceSwitch.EnterSends "Enter 直接发送" "关闭后用 Ctrl / ⌘ + Enter 发送，Enter 换行。"

    let collapseRow, setCollapse =
        row AppearanceSwitch.CollapseReasoning "完成后收起思考过程" "生成结束时自动折叠模型的思维链。"

    let wrapRow, setWrap =
        row AppearanceSwitch.CodeWrap "代码块长行折行" "关闭时长行横向滚动；开启后折行显示，一屏读完。"

    let archivedRow, setArchived =
        row AppearanceSwitch.ShowArchived "在侧栏显示已归档对话" "归档对话默认收起，避免长列表。"

    let motionRow, setMotion =
        row AppearanceSwitch.ReduceMotion "减少动态效果" "停止加载指示器等持续旋转；状态变化仍会即时显示。"

    let draftsRow, setDrafts =
        row
            AppearanceSwitch.PersistDrafts
            "保存未发送的草稿"
            "在这台设备上保存输入框里没发出的文字、附件名称和未确认的消息，重新打开后恢复。只存在本机，不会上传或同步。关闭后会立即删除已保存的内容。"

    let themeCaption = Ui.smallText ""
    let fontSize = TextBlock(Text = "", FontSize = 13.0)

    do
        fontSize.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

    let draftSummary = Ui.caption ""
    let smaller = Ui.iconButton "-" "更小" (fun () -> fontStep.Trigger -0.5)
    let larger = Ui.iconButton "+" "更大" (fun () -> fontStep.Trigger 0.5)
    let themeButton = Button()

    let view =
        themeCaption.VerticalAlignment <- VerticalAlignment.Center
        themeCaption.TextTrimming <- TextTrimming.CharacterEllipsis
        themeCaption.MaxWidth <- 260.0

        let chevron =
            TextBlock(Text = "▼", FontSize = 10.0, VerticalAlignment = VerticalAlignment.Center)

        chevron.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint")
        |> ignore

        let btnStack = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        btnStack.Children.Add themeCaption
        btnStack.Children.Add chevron
        themeButton.Content <- btnStack
        themeButton.Classes.Add "surface"
        themeButton.Classes.Add "select-button"
        themeButton.Classes.Add "hover-bordered"
        themeButton.Click.Add(fun _ -> themeClicked.Trigger(themeButton :> Control))
        AutomationProperties.SetName(themeButton, "主题")
        AutomationProperties.SetControlTypeOverride(themeButton, Nullable AutomationControlType.ComboBox)
        themeButton.HorizontalAlignment <- HorizontalAlignment.Left
        fontSize.VerticalAlignment <- VerticalAlignment.Center
        fontSize.MinWidth <- 56.0

        let fontButtons =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 12.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        fontButtons.Children.Add smaller
        fontButtons.Children.Add fontSize
        fontButtons.Children.Add larger
        let fontDock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(fontButtons, Dock.Right)
        fontDock.Children.Add fontButtons
        let fontTextStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        fontTextStack.Children.Add(Ui.label "消息字号")
        fontTextStack.Children.Add(Ui.caption "影响对话正文与代码块。")
        fontDock.Children.Add fontTextStack

        let fontRow =
            Border(Child = fontDock, Padding = Thickness(12.0, 8.0), MinHeight = 52.0)

        AutomationProperties.SetAutomationId(draftSummary, "local-drafts-summary")
        let clear = Ui.button "secondary" "清除本地草稿" clearDrafts.Trigger
        clear.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(clear, "clear-local-drafts")

        let makeCard (children: Control list) =
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)

            for c in children do
                p.Children.Add c

            Ui.card p :> Control

        let p1 = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        p1.Children.Add(Ui.heading "外观与交互")
        p1.Children.Add(Ui.caption "这些偏好只保存在本机，不会同步到主机。")
        let p2 = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        p2.Children.Add(Ui.heading "本机草稿")
        p2.Children.Add(Ui.caption "草稿只保存在这台设备上，不会上传，也不会出现在对话记录里。")
        let draftsCardPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        draftsCardPanel.Children.Add draftsRow
        draftsCardPanel.Children.Add(Ui.hairline ())
        draftsCardPanel.Children.Add draftSummary
        draftsCardPanel.Children.Add clear
        let main = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        main.Children.Add p1

        main.Children.Add(
            makeCard
                [
                    Ui.fieldGroup "主题" "深色主题是同一套纸感在低光下的版本。" themeButton None
                    Ui.hairline ()
                    fontRow
                ]
        )

        main.Children.Add(
            makeCard
                [
                    enterRow
                    Ui.hairline ()
                    collapseRow
                    Ui.hairline ()
                    wrapRow
                    Ui.hairline ()
                    archivedRow
                    Ui.hairline ()
                    motionRow
                ]
        )

        main.Children.Add p2
        main.Children.Add(Ui.card draftsCardPanel)
        main

    member _.View = view :> Control

    member _.Render(s: AppearanceState) =
        themeCaption.Text <- s.ThemeLabel
        fontSize.Text <- s.FontSizeText
        // 边界即禁用：到上下限时对应方向的键不可点
        smaller.ApplyEnabled s.CanShrink
        larger.ApplyEnabled s.CanGrow
        setEnter s.EnterSends
        setCollapse s.CollapseReasoning
        setWrap s.CodeWrap
        setArchived s.ShowArchived
        setMotion s.ReduceMotion
        setDrafts s.PersistDrafts
        draftSummary.Text <- s.DraftSummary

    /// 主题选择器被点（带锚点，组合者弹「主题」分组菜单）
    member _.ThemeClicked = themeClicked.Publish
    /// 字号步进（-0.5 / +0.5）
    member _.FontStepped = fontStep.Publish
    /// 用户拨了某个开关（新值）
    member _.Toggled = toggled.Publish
    /// 「清除本地草稿」（确认框由组合者弹）
    member _.ClearDraftsClicked = clearDrafts.Publish

/// 设置 → 关于与系统诊断（旧 SettingsGeneral.BuildAbout / BuildHostStatus 的界面部分）
type AboutSection(?glyphOf: string -> IBrush -> Control) =
    let copy = Event<unit>()
    let check = Event<unit>()
    let rows = StackPanel(Orientation = Orientation.Vertical)
    let body = StackPanel(Orientation = Orientation.Vertical)
    /// 上一帧的信息行与主机状态：内容没变就不重建
    let mutable lastRows: (string * string) list = []
    let mutable lastConfigIssue: (string * string list) option = None
    let mutable lastDoctor: AboutDoctor = AboutDoctor.NotRun
    let mutable rowsBuilt = false
    let mutable healthBuilt = false

    let view =
        rows.Spacing <- 12.0
        body.Spacing <- 12.0
        let copyButton = Ui.button "secondary" "复制诊断报告" copy.Trigger
        copyButton.HorizontalAlignment <- HorizontalAlignment.Left

        let makeCard (children: Control list) =
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)

            for c in children do
                p.Children.Add c

            Ui.card p :> Control

        let header = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        header.Children.Add(Ui.heading "关于与系统诊断")
        let main = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        main.Children.Add header
        main.Children.Add(makeCard [ rows; Ui.hairline (); copyButton ])
        main.Children.Add(makeCard [ Ui.label "主机状态"; body ])

        main.Children.Add(
            makeCard
                [
                    Ui.label "设计与实现"
                    Ui.caption "万象是独立实现的对等 C/S 聊天客户端：每个实例既是主机也是客户端，"
                    Ui.caption "对话以 NDJSON 事件日志为唯一权威，界面只是投影的一个视图。"
                ]
        )

        main.Children.Add(
            makeCard
                [
                    Ui.label "致谢"
                    Ui.caption "界面思路受 Kelivo 启发，代码与数据模型均为独立实现。"
                    Ui.caption "内嵌字体 Sarasa Term SC 采用 OFL-1.1 许可。"
                ]
        )

        main

    /// 主机状态：配置问题（摘要, 明细）与检查结果
    let renderHealth (configIssue: (string * string list) option) (doctor: AboutDoctor) =
        body.Children.Clear()

        match configIssue with
        | Some(summary, errors) ->
            let s = Ui.caption summary
            s.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
            AutomationProperties.SetAutomationId(s, "host-config-issue")
            let d = Ui.caption (String.concat "\n" errors)
            AutomationProperties.SetAutomationId(d, "host-config-issue-detail")
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
            p.Children.Add(Ui.label "配置有误")
            p.Children.Add s
            p.Children.Add d
            p.Children.Add(Ui.caption "修好配置文件后，这条提示会自动消失。")
            body.Children.Add p
        | None ->
            let t = Ui.caption "配置正常，主机正在使用最新一份配置。"
            AutomationProperties.SetAutomationId(t, "host-config-ok")
            body.Children.Add t

        body.Children.Add(Ui.hairline ())
        let running = (doctor = AboutDoctor.Running)

        let button =
            Ui.button "secondary" (if running then "正在检查…" else "检查主机") (fun () ->
                if not running then
                    check.Trigger())

        button.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(button, "check-host")

        if running then
            button.ApplyEnabled false

        body.Children.Add button

        match doctor with
        | AboutDoctor.NotRun -> body.Children.Add(Ui.caption "只读检查：配置、磁盘、数据目录、事件日志与附件。不会修改任何内容。")
        | AboutDoctor.Running -> body.Children.Add(Ui.caption "正在检查，可能需要几秒…")
        | AboutDoctor.Unavailable reason -> body.Children.Add(Ui.caption reason)
        | AboutDoctor.Report(ok, checks, at) ->
            let headline = Ui.label (if ok then "全部通过" else "有项目需要处理")
            AutomationProperties.SetAutomationId(headline, "doctor-headline")
            body.Children.Add headline

            for c in checks do
                let icon =
                    match glyphOf with
                    | Some g ->
                        let b = SolidColorBrush(if c.Ok then Colors.Green else Colors.Red) :> IBrush
                        g (if c.Ok then "check-circle" else "alert") b
                    | None ->
                        let t =
                            TextBlock(Text = (if c.Ok then "✓" else "⚠"), VerticalAlignment = VerticalAlignment.Center)

                        if c.Ok then
                            t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Success") |> ignore
                        else
                            t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore

                        t :> Control

                let name = Ui.label c.Label
                name.MinWidth <- 120.0

                let detail =
                    SelectableTextBlock(
                        Text = c.Detail,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 12.0
                    )

                if c.Ok then
                    detail.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                else
                    detail.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore

                let line = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
                line.Children.Add icon
                line.Children.Add name
                line.Children.Add detail
                AutomationProperties.SetAutomationId(line, sprintf "doctor-check-%s" c.Name)
                body.Children.Add line

            body.Children.Add(Ui.caption (sprintf "检查时间 %s" at))

    member _.View = view :> Control

    /// 单入口幂等渲染：信息行（键, 值）与主机状态（配置问题、检查结果）一次性投影
    member _.Render(vs: GeneralSectionVS) =
        let items = vs.AboutRows

        if not rowsBuilt || items <> lastRows then
            (rowsBuilt <- true
             lastRows <- items
             rows.Children.Clear()

             for key, value in items do
                 let k = TextBlock(Text = key, FontSize = 13.0, MinWidth = 120.0)
                 k.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore

                 let v =
                     SelectableTextBlock(Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 13.0)

                 v.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
                 v.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

                 v.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft")
                 |> ignore

                 let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 16.0)
                 row.Children.Add k
                 row.Children.Add v
                 rows.Children.Add row)

        let configIssue = vs.Health.ConfigIssue
        let doctor = vs.Health.Doctor

        if not healthBuilt || configIssue <> lastConfigIssue || doctor <> lastDoctor then
            (healthBuilt <- true
             lastConfigIssue <- configIssue
             lastDoctor <- doctor
             renderHealth configIssue doctor)

    /// 「复制诊断报告」（剪贴板与提示由组合者做）
    member _.CopyDiagnosticsClicked = copy.Publish
    /// 「检查主机」（检查中不发）
    member _.CheckHostClicked = check.Publish
