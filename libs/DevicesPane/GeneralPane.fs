namespace Wanxiang2.GeneralPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 生成默认值表单的文字（组合者由 GenerationForm.floatText 等得出）
type GenerationText =
    { Temperature: string
      TopP: string
      MaxTokens: string
      ContextMessages: string
      ToolRounds: string
      Instructions: string
      AutoTitle: bool }

/// 一条对话预设
type PresetRow =
    { Id: string
      Name: string
      /// Presets.describe 的一行说明
      Detail: string }

module internal Layout =
    /// 两列表单：宽时并排，窄于断点退化为单列（旧 Ui.twoColumnForm）
    let twoColumns (kit: Kit) (look: GeneralPaneLook) (fields: Control list) : Grid =
        let grid = Grid()
        kit.Live(look.Space2xl, fun v -> grid.ColumnSpacing <- v)
        kit.Live(look.SpaceXl, fun v -> grid.RowSpacing <- v)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        for _ in fields do grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
        for f in fields do grid.Children.Add f
        let mutable breakpoint = 0.0
        let apply () =
            let width = grid.Bounds.Width
            let single = width > 0.0 && width < breakpoint
            grid.ColumnDefinitions[1].Width <- (if single then GridLength(0.0) else GridLength(1.0, GridUnitType.Star))
            fields
            |> List.iteri (fun i c ->
                Grid.SetRow(c, (if single then i else i / 2))
                Grid.SetColumn(c, (if single then 0 else i % 2)))
        kit.Live(look.FormSingleColumnBreakpoint, fun v -> breakpoint <- v; apply ())
        grid.PropertyChanged.Add(fun a -> if a.Property = Visual.BoundsProperty then apply ())
        grid

/// 设置 → 对话默认值（旧 SettingsGeneral.BuildGeneration 与预设列表、导入入口的界面部分）。
/// 草稿状态（GenerationDraft）、校验（GenerationForm + FieldCheck）、写回、预设对话框由组合者接。
type GenerationSection(look: GeneralPaneLook, validationFor: TextBox -> Control option) =
    let kit = Kit(look)
    let edited = Event<unit>()
    let save = Event<unit>()
    let discard = Event<unit>()
    let rename = Event<string>()
    let delete = Event<string>()
    let import = Event<unit>()
    let field label hint placeholder =
        let shell, box = kit.TextField placeholder
        box, kit.FieldGroup(label, hint, shell, validationFor box)
    let temperatureBox, temperatureField = field "Temperature" "越高越发散。留空则用模型服务默认值。" "留空 = 使用模型服务默认"
    let topPBox, topPField = field "Top P" "核采样阈值，0–1。" "0–1，留空不设"
    let maxTokensBox, maxTokensField = field "最大输出 token" "限制单次回复长度。" "留空不限制"
    let contextBox, contextField = field "上下文消息上限" "每次请求最多携带多少条历史，防止长对话撞上模型上限。" "200"
    let toolRoundsBox, toolRoundsField = field "工具调用轮数上限" "模型连续调用工具超过这个轮数就中止本次生成。" "12"
    let instructionsShell, instructionsBox = kit.TextArea("新对话的默认系统指令（可留空）", look.TextAreaMinHeight)
    let summary = kit.Caption ""
    let status = kit.Caption ""
    let autoTitleRow, readAutoTitle, writeAutoTitle =
        kit.SwitchRow("自动生成对话标题", "首轮对话后用同一个模型起一个短标题；失败时回落到你的第一句话。", true, (fun _ -> edited.Trigger()))
    let saveButton = kit.Button(Tone.Primary, "保存默认值", save.Trigger)
    let discardButton = kit.Button(Tone.Ghost, "放弃修改", discard.Trigger)
    let presetsPanel = StackPanel(Orientation = Orientation.Vertical, Name = "settings-presets")
    let importBlock = StackPanel(Orientation = Orientation.Vertical, IsVisible = false)
    let numberBoxes = [ temperatureBox; topPBox; maxTokensBox; contextBox; toolRoundsBox ]

    let view =
        summary.Foreground <- look.Danger
        summary.IsVisible <- false
        summary.Focusable <- true
        AutomationProperties.SetName(summary, "表单错误摘要")
        AutomationProperties.SetLiveSetting(summary, AutomationLiveSetting.Assertive)
        AutomationProperties.SetName(status, "生成设置保存状态")
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        for box in numberBoxes @ [ instructionsBox ] do
            box.PropertyChanged.Add(fun e -> if e.Property = TextBox.TextProperty then edited.Trigger())
        AutomationProperties.SetHelpText(saveButton, "保存默认值（Ctrl / ⌘ + Enter）")
        ToolTip.SetTip(saveButton, "保存默认值（Ctrl / ⌘ + Enter）")
        kit.Live(look.PendingMinWidth, fun v -> saveButton.MinWidth <- v)
        saveButton.HorizontalAlignment <- HorizontalAlignment.Left
        kit.Live(look.SpaceMd, fun v -> presetsPanel.Spacing <- v; importBlock.Spacing <- v)
        let grid = Layout.twoColumns kit look [ temperatureField; topPField; maxTokensField; contextField; toolRoundsField ]
        let importButton = kit.Button(Tone.Secondary, "导入对话…", import.Trigger)
        importButton.Name <- "settings-import"
        importButton.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetName(importButton, "导入对话")
        AutomationProperties.SetHelpText(importButton, "从导出的 JSON 文件新建一个对话")
        for c in [ kit.Hairline() :> Control; kit.Label "导入对话"; kit.Caption "从导出的 JSON 文件在这台主机上新建一个对话，先预览再确认。"; importButton ] do
            importBlock.Children.Add c
        let form =
            kit.VStack(
                look.Space4xl,
                [ kit.VStack(look.SpaceXs, [ kit.Heading "对话默认值" :> Control; kit.Caption "保存到当前主机，供所有客户端新建对话时使用。不会改动已有对话。" ]) :> Control
                  summary
                  kit.Card(
                      look.Space2xl,
                      look.Space2xl,
                      look.RadiusLg,
                      kit.VStack(
                          look.SpaceXs,
                          [ grid :> Control
                            kit.Hairline()
                            kit.FieldGroup("默认系统指令", "会作为 system 消息随每次请求发送。", instructionsShell, None)
                            kit.Hairline()
                            autoTitleRow ]))
                  kit.Hairline()
                  status
                  kit.HStack(look.SpaceMd, [ saveButton :> Control; discardButton ])
                  kit.VStack(look.SpaceMd, [ kit.Hairline() :> Control; presetsPanel ])
                  importBlock ])
        let ctrl (e: KeyEventArgs) = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
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
        { Temperature = t temperatureBox
          TopP = t topPBox
          MaxTokens = t maxTokensBox
          ContextMessages = t contextBox
          ToolRounds = t toolRoundsBox
          Instructions = t instructionsBox
          AutoTitle = readAutoTitle () }

    /// 填表（会发 Edited，与旧实现相同；GenerationDraft 在回填回调里忽略它）
    member _.Fill(v: GenerationText) =
        temperatureBox.Text <- v.Temperature
        topPBox.Text <- v.TopP
        maxTokensBox.Text <- v.MaxTokens
        contextBox.Text <- v.ContextMessages
        toolRoundsBox.Text <- v.ToolRounds
        instructionsBox.Text <- v.Instructions
        writeAutoTitle v.AutoTitle

    member _.SetStatus(text: string) = status.Text <- text

    /// 保存往返中：保存键换「正在保存…」并禁用；结束后按 canSave 决定可用；放弃键往返中禁用
    member _.SetPending(pending: bool, canSave: bool) =
        let text = if pending then "正在保存…" else "保存默认值"
        saveButton.SetText text
        saveButton.SetEnabled(not pending && canSave)
        AutomationProperties.SetHelpText(saveButton, (if pending then text else "保存默认值（Ctrl / ⌘ + Enter）"))
        discardButton.SetEnabled(not pending)

    member _.SetPresets(presets: PresetRow list) =
        presetsPanel.Children.Clear()
        presetsPanel.Children.Add(kit.Label "对话预设")
        if presets.IsEmpty then
            presetsPanel.Children.Add(kit.Caption "预设记下一段对话的模型、系统指令、生成参数和工具。在对话顶部的「⋯」里选「存为预设」，以后新建对话时一键套用。")
        else
            presetsPanel.Children.Add(kit.Caption "保存在当前主机上，所有设备都能用。改动预设不会影响已经建好的对话。")
            for p in presets do
                let name = kit.Text(p.Name, look.BodyFontSize, look.Text)
                name.TextTrimming <- TextTrimming.CharacterEllipsis
                let detail = kit.Caption p.Detail
                detail.TextTrimming <- TextTrimming.CharacterEllipsis
                detail.TextWrapping <- TextWrapping.NoWrap
                let text = kit.VStack(look.SpaceXXs, [ name :> Control; detail ])
                let r = kit.Button(Tone.Ghost, "重命名", (fun () -> rename.Trigger p.Id))
                let d = kit.Button(Tone.Ghost, "删除", (fun () -> delete.Trigger p.Id))
                AutomationProperties.SetName(r, sprintf "重命名预设 %s" p.Name)
                AutomationProperties.SetName(d, sprintf "删除预设 %s" p.Name)
                let buttons = kit.HStack(look.SpaceXs, [ r :> Control; d ])
                let row = DockPanel(LastChildFill = true, Name = "preset-row")
                DockPanel.SetDock(buttons, Dock.Right)
                row.Children.Add buttons
                row.Children.Add text
                presetsPanel.Children.Add row

    /// 宿主接了导入才显示「导入对话…」
    member _.SetImportVisible(visible: bool) = importBlock.IsVisible <- visible

    member _.PresetsPanel = presetsPanel
    /// 任一字段或开关被改
    member _.Edited = edited.Publish
    /// 保存键、Ctrl/⌘+Enter、数值框回车（往返中也会发，组合者按 GenerationDraft.CanSave 拦）
    member _.SaveRequested = save.Publish
    member _.DiscardRequested = discard.Publish
    member _.PresetRenameClicked = rename.Publish
    member _.PresetDeleteClicked = delete.Publish
    member _.ImportClicked = import.Publish

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
    { ThemeLabel: string
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
      DraftSummary: string }

/// 设置 → 外观与交互、本机草稿（旧 SettingsGeneral.BuildAppearance / BuildLocalDrafts 的界面部分）
type AppearanceSection(look: GeneralPaneLook) =
    let kit = Kit(look)
    let themeClicked = Event<Control>()
    let fontStep = Event<float>()
    let toggled = Event<AppearanceSwitch * bool>()
    let clearDrafts = Event<unit>()
    let row which title hint =
        let r, _, write = kit.SwitchRow(title, hint, false, (fun v -> toggled.Trigger((which, v))))
        r, write
    let enterRow, setEnter = row AppearanceSwitch.EnterSends "Enter 直接发送" "关闭后用 Ctrl / ⌘ + Enter 发送，Enter 换行。"
    let collapseRow, setCollapse = row AppearanceSwitch.CollapseReasoning "完成后收起思考过程" "生成结束时自动折叠模型的思维链。"
    let wrapRow, setWrap = row AppearanceSwitch.CodeWrap "代码块长行折行" "关闭时长行横向滚动；开启后折行显示，一屏读完。"
    let archivedRow, setArchived = row AppearanceSwitch.ShowArchived "在侧栏显示已归档对话" "归档对话默认收起，避免长列表。"
    let motionRow, setMotion = row AppearanceSwitch.ReduceMotion "减少动态效果" "停止加载指示器等持续旋转；状态变化仍会即时显示。"
    let draftsRow, setDrafts =
        row AppearanceSwitch.PersistDrafts "保存未发送的草稿" "在这台设备上保存输入框里没发出的文字、附件名称和未确认的消息，重新打开后恢复。只存在本机，不会上传或同步。关闭后会立即删除已保存的内容。"
    let themeCaption = kit.Text("", look.SmallFontSize, look.Text)
    let fontSize = kit.Text("", look.SmallFontSize, look.Muted)
    let draftSummary = kit.Caption ""
    let mutable themeButton: Tap = Unchecked.defaultof<_>
    let smaller = kit.IconButton("minus", "更小", (fun () -> fontStep.Trigger -0.5))
    let larger = kit.IconButton("plus", "更大", (fun () -> fontStep.Trigger 0.5))

    let view =
        themeCaption.VerticalAlignment <- VerticalAlignment.Center
        themeCaption.TextTrimming <- TextTrimming.CharacterEllipsis
        kit.Live(look.SelectCaptionMaxWidth, fun v -> themeCaption.MaxWidth <- v)
        let chevron = look.Glyph "chevron-down" look.Faint
        chevron.VerticalAlignment <- VerticalAlignment.Center
        themeButton <- Tap(kit.HStack(look.SpaceMd, [ themeCaption :> Control; chevron ]), "主题", [ "surface"; "select-button"; "hover-bordered" ], (fun () -> themeClicked.Trigger(themeButton :> Control)))
        AutomationProperties.SetControlTypeOverride(themeButton, Nullable AutomationControlType.ComboBox)
        kit.Live(look.BorderWidth, fun v -> themeButton.BorderThickness <- Thickness v)
        kit.Live(look.RadiusLg, fun v -> themeButton.CornerRadius <- CornerRadius v)
        kit.Live(Kit.Pair(look.SpaceXl, look.SelectPaddingY), fun (x, y) -> themeButton.Padding <- Thickness(x, y))
        themeButton.HorizontalAlignment <- HorizontalAlignment.Left
        fontSize.VerticalAlignment <- VerticalAlignment.Center
        kit.Live(look.FontSizeValueMinWidth, fun v -> fontSize.MinWidth <- v)
        let fontRow = kit.RowShell("消息字号", "影响对话正文与代码块。", kit.HStack(look.SpaceMd, [ smaller :> Control; fontSize; larger ]))
        AutomationProperties.SetAutomationId(draftSummary, "local-drafts-summary")
        let clear = kit.Button(Tone.Secondary, "清除本地草稿", clearDrafts.Trigger)
        clear.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(clear, "clear-local-drafts")
        let card children = kit.Card(look.Space2xl, look.Space2xl, look.RadiusLg, kit.VStack(look.SpaceXs, children)) :> Control
        kit.VStack(
            look.Space4xl,
            [ kit.VStack(look.SpaceXs, [ kit.Heading "外观与交互" :> Control; kit.Caption "这些偏好只保存在本机，不会同步到主机。" ]) :> Control
              card [ kit.FieldGroup("主题", "深色主题是同一套纸感在低光下的版本。", themeButton, None); kit.Hairline(); fontRow ]
              card [ enterRow; kit.Hairline(); collapseRow; kit.Hairline(); wrapRow; kit.Hairline(); archivedRow; kit.Hairline(); motionRow ]
              kit.VStack(look.SpaceXs, [ kit.Heading "本机草稿" :> Control; kit.Caption "草稿只保存在这台设备上，不会上传，也不会出现在对话记录里。" ])
              kit.Card(look.Space2xl, look.Space2xl, look.RadiusLg, kit.VStack(look.SpaceMd, [ draftsRow; kit.Hairline(); draftSummary; clear ])) ])

    member _.View = view :> Control

    member _.Render(s: AppearanceState) =
        themeCaption.Text <- s.ThemeLabel
        fontSize.Text <- s.FontSizeText
        // 边界即禁用：到上下限时对应方向的键不可点
        smaller.SetEnabled s.CanShrink
        larger.SetEnabled s.CanGrow
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

/// 一项主机检查
type AboutCheck =
    { Name: string
      Label: string
      Ok: bool
      Detail: string }

/// 「检查主机」的状态
[<RequireQualifiedAccess>]
type AboutDoctor =
    | NotRun
    | Running
    | Unavailable of reason: string
    | Report of ok: bool * checks: AboutCheck list * timeText: string

/// 设置 → 关于与系统诊断（旧 SettingsGeneral.BuildAbout / BuildHostStatus 的界面部分）
type AboutSection(look: GeneralPaneLook) =
    let kit = Kit(look)
    let copy = Event<unit>()
    let check = Event<unit>()
    let rows = StackPanel(Orientation = Orientation.Vertical)
    let body = StackPanel(Orientation = Orientation.Vertical)

    let view =
        kit.Live(look.SpaceMd, fun v -> rows.Spacing <- v; body.Spacing <- v)
        let copyButton = kit.Button(Tone.Secondary, "复制诊断报告", copy.Trigger)
        copyButton.HorizontalAlignment <- HorizontalAlignment.Left
        let card children = kit.Card(look.Space2xl, look.Space2xl, look.RadiusLg, kit.VStack(look.SpaceMd, children)) :> Control
        kit.VStack(
            look.Space4xl,
            [ kit.VStack(look.SpaceXs, [ kit.Heading "关于与系统诊断" :> Control ]) :> Control
              card [ rows; kit.Hairline(); copyButton ]
              card [ kit.Label "主机状态"; body ]
              card [ kit.Label "设计与实现"
                     kit.Caption "万象是独立实现的对等 C/S 聊天客户端：每个实例既是主机也是客户端，"
                     kit.Caption "对话以 NDJSON 事件日志为唯一权威，界面只是投影的一个视图。" ]
              card [ kit.Label "致谢"; kit.Caption "界面思路受 Kelivo 启发，代码与数据模型均为独立实现。"; kit.Caption "内嵌字体 Sarasa Term SC 采用 OFL-1.1 许可。" ] ])

    member _.View = view :> Control

    /// 信息行（键, 值）：协议版本、日志格式版本、客户端平台、操作系统、主机实例、连接地址
    member _.SetRows(items: (string * string) list) =
        rows.Children.Clear()
        for key, value in items do
            let k = kit.Text(key, look.SmallFontSize, look.Muted)
            kit.Live(look.AboutKeyMinWidth, fun v -> k.MinWidth <- v)
            let v = SelectableTextBlock(Text = value, Foreground = look.Text, TextWrapping = TextWrapping.Wrap, SelectionBrush = look.Selection)
            kit.Live(look.SmallFontSize, fun s -> v.FontSize <- s)
            kit.Live(look.MonoFont, fun f -> v.FontFamily <- f)
            rows.Children.Add(kit.HStack(look.SpaceXl, [ k :> Control; v ]))

    /// 主机状态：配置问题（摘要, 明细）与检查结果
    member _.SetHealth(configIssue: (string * string list) option, doctor: AboutDoctor) =
        body.Children.Clear()
        match configIssue with
        | Some(summary, errors) ->
            let s = kit.Caption summary
            s.Foreground <- look.Danger
            AutomationProperties.SetAutomationId(s, "host-config-issue")
            let d = kit.Caption(String.concat "\n" errors)
            AutomationProperties.SetAutomationId(d, "host-config-issue-detail")
            body.Children.Add(kit.VStack(look.SpaceXs, [ kit.Label "配置有误" :> Control; s; d; kit.Caption "修好配置文件后，这条提示会自动消失。" ]))
        | None ->
            let t = kit.Caption "配置正常，主机正在使用最新一份配置。"
            AutomationProperties.SetAutomationId(t, "host-config-ok")
            body.Children.Add t
        body.Children.Add(kit.Hairline())
        let running = (doctor = AboutDoctor.Running)
        let button = kit.Button(Tone.Secondary, (if running then "正在检查…" else "检查主机"), (fun () -> if not running then check.Trigger()))
        button.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(button, "check-host")
        if running then button.SetEnabled false
        body.Children.Add button
        match doctor with
        | AboutDoctor.NotRun -> body.Children.Add(kit.Caption "只读检查：配置、磁盘、数据目录、事件日志与附件。不会修改任何内容。")
        | AboutDoctor.Running -> body.Children.Add(kit.Caption "正在检查，可能需要几秒…")
        | AboutDoctor.Unavailable reason -> body.Children.Add(kit.Caption reason)
        | AboutDoctor.Report(ok, checks, at) ->
            let headline = kit.Label(if ok then "全部通过" else "有项目需要处理")
            AutomationProperties.SetAutomationId(headline, "doctor-headline")
            body.Children.Add headline
            for c in checks do
                let icon = if c.Ok then look.Glyph "check-circle" look.Success else look.Glyph "alert" look.Danger
                let name = kit.Label c.Label
                kit.Live(look.AboutKeyMinWidth, fun v -> name.MinWidth <- v)
                let detail = SelectableTextBlock(Text = c.Detail, Foreground = (if c.Ok then look.Muted else look.Danger), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center)
                kit.Live(look.CaptionFontSize, fun v -> detail.FontSize <- v)
                let line = kit.HStack(look.SpaceMd, [ icon; name :> Control; detail ])
                AutomationProperties.SetAutomationId(line, sprintf "doctor-check-%s" c.Name)
                body.Children.Add line
            body.Children.Add(kit.Caption(sprintf "检查时间 %s" at))

    /// 「复制诊断报告」（剪贴板与提示由组合者做）
    member _.CopyDiagnosticsClicked = copy.Publish
    /// 「检查主机」（检查中不发）
    member _.CheckHostClicked = check.Publish
