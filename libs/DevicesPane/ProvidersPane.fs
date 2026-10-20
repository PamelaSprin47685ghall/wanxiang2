namespace Wanxiang2.ProvidersPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState
open Wanxiang2.DevicesPane
open Wanxiang2.DevicesPane.ButtonExtensions

/// 行首状态点的语气
[<RequireQualifiedAccess>]
type ProviderDot =
    /// 已停用（最弱）
    | Disabled
    /// 配置完整（强调色）
    | Configured
    /// 缺少密钥（警示色）
    | MissingKey

/// 一个模型服务
type ProviderRow =
    { Id: string
      Label: string
      /// 「地址 · N 个模型 · 未设置密钥」
      Meta: string
      Enabled: bool
      Dot: ProviderDot
      /// 是全局默认
      IsDefault: bool }

/// 设置 → 模型服务列表（旧 SettingsProviders 的列表部分）
type ProvidersPane(?glyphOf: string -> IBrush -> Control) =
    let add = Event<unit>()
    let edit = Event<string>()
    let more = Event<string * Control>()
    let listPanel = StackPanel(Orientation = Orientation.Vertical)
    let topAdd = Ui.button "primary" "添加模型服务" add.Trigger

    let renderRow (p: ProviderRow) : Control =
        let dot = Ui.statusDot ()
        match (p: ProviderRow).Dot with
        | ProviderDot.Disabled -> dot.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, Dyn.res "Palette.TextFaint") |> ignore
        | ProviderDot.Configured -> dot.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, Dyn.res "Palette.Accent") |> ignore
        | ProviderDot.MissingKey -> dot.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, Dyn.res "Palette.Warning") |> ignore
        let name = TextBlock(Text = p.Label, FontSize = 14.0)
        if p.Enabled then
            name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        else
            name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        name.FontWeight <- FontWeight.Medium
        name.VerticalAlignment <- VerticalAlignment.Center
        let meta = TextBlock(Text = p.Meta, FontSize = 11.0)
        meta.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
        meta.TextTrimming <- TextTrimming.CharacterEllipsis
        let stateLabel, tone =
            match p.Dot with
            | ProviderDot.Disabled -> "已停用", TagTone.Neutral
            | ProviderDot.Configured -> "已配置", TagTone.Neutral
            | ProviderDot.MissingKey -> "缺少密钥", TagTone.Warning
        let titleRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0, VerticalAlignment = VerticalAlignment.Center)
        titleRow.Children.Add dot
        titleRow.Children.Add name
        titleRow.Children.Add(Ui.tag tone stateLabel)
        if p.IsDefault then titleRow.Children.Add(Ui.tag TagTone.Neutral "默认")
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        column.Children.Add titleRow
        column.Children.Add meta
        let editButton = Ui.iconButton "✎" "编辑" (fun () -> edit.Trigger p.Id)
        let mutable moreButton: Button = Unchecked.defaultof<_>
        moreButton <- Ui.iconButton "⋯" "更多" (fun () -> more.Trigger((p.Id, moreButton :> Control)))
        let actions = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0, VerticalAlignment = VerticalAlignment.Center)
        actions.Children.Add editButton
        actions.Children.Add moreButton
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(actions, Dock.Right)
        dock.Children.Add actions
        dock.Children.Add column
        Ui.card dock :> Control

    let view =
        listPanel.Spacing <- 12.0
        topAdd.HorizontalAlignment <- HorizontalAlignment.Left
        topAdd.IsVisible <- false
        let header = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        header.Children.Add(Ui.heading "模型服务")
        header.Children.Add(Ui.caption "万象通过模型服务原生协议或 OpenAI 兼容接口访问模型。密钥保存在主机配置里，界面不会回显。")
        let main = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        main.Children.Add header
        main.Children.Add topAdd
        main.Children.Add listPanel
        main

    member _.View = view :> Control

    /// 列表为空时顶部「添加」隐藏，由空态里的同名主按钮接管（同屏不出现两个相同主操作）
    member _.Render(rows: ProviderRow list) =
        listPanel.Children.Clear()
        topAdd.IsVisible <- not rows.IsEmpty
        if rows.IsEmpty then
            let logo =
                match glyphOf with
                | Some g -> g "logo" (SolidColorBrush(Colors.Gray) :> IBrush)
                | None ->
                    let t = TextBlock(Text = "万", FontSize = 32.0, HorizontalAlignment = HorizontalAlignment.Center)
                    t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                    t :> Control
            logo.HorizontalAlignment <- HorizontalAlignment.Center
            let text = Ui.caption "添加一个模型服务后就能开始对话。预设已经填好端点和常见模型，你只需要粘贴密钥。"
            text.TextAlignment <- TextAlignment.Center
            let emptyAdd = Ui.button "primary" "添加模型服务" add.Trigger
            emptyAdd.HorizontalAlignment <- HorizontalAlignment.Center
            let emptyCardPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
            emptyCardPanel.Children.Add logo
            emptyCardPanel.Children.Add text
            emptyCardPanel.Children.Add emptyAdd
            listPanel.Children.Add(Ui.card emptyCardPanel)
        else
            for r in rows do listPanel.Children.Add(renderRow r)

    member _.AddClicked = add.Publish
    member _.EditClicked = edit.Publish
    /// 服务 id 与「更多」键（组合者弹「获取模型列表 / 设为默认 / 停用 / 删除」菜单）
    member _.MoreClicked = more.Publish

/// 模型服务编辑器（旧 SettingsProviders.ShowEditor 的界面部分）：预设选择器、传输说明、预设说明、错误摘要、
/// 快速区（名称、密钥、地址）、高级区（传输切换、标识、模型、默认模型、获取模型列表、看图能力、超时、重试、启用）、取消 / 保存。
/// 预设套用与分歧判定、探活、校验、保存往返由组合者接。
type ProviderEditor(isNew: bool, validationFor: TextBox -> Control option, ?_glyphOf: string -> IBrush -> Control) =
    let presetClicked = Event<Control>()
    let transportClicked = Event<unit>()
    let probeClicked = Event<unit>()
    let save = Event<unit>()
    let cancel = Event<unit>()
    let mutable pending = false
    let mutable advanced = false
    let field label hint placeholder (name: string) =
        let box = Ui.textField placeholder
        AutomationProperties.SetName(box, name)
        box, Ui.fieldGroup label hint box (validationFor box)
    let multiline (box: TextBox) =
        box.AcceptsReturn <- true
        box.TextWrapping <- TextWrapping.Wrap
        box.VerticalContentAlignment <- VerticalAlignment.Top
    let idBox, idField = field "稳定标识" "创建后不可修改；仅限字母、数字、下划线与连字符。" "例如 openai" "稳定标识"
    let labelBox, labelField = field "显示名称" "仅用于界面显示。" "在界面上怎么称呼它" "显示名称"
    let urlBox, urlField = field "端点地址" "模型服务的 API 地址，以 https:// 开头；原生接口与兼容接口均可。" "https://api.openai.com/v1" "端点地址"
    let keyBox, keyField = field "API Key" "只保存在主机，界面不会回显。" "粘贴密钥" "API Key"
    let modelsBox, modelsField = field "模型列表" "每行一个；也可用逗号分隔。" "每行一个模型名" "模型列表"
    let defaultModelBox, defaultModelField = field "默认模型" "必须在模型列表中；留空自动选用第一个。" "留空则自动使用列表中第一个" "默认模型"
    let timeoutBox, timeoutField = field "请求超时（秒）" "单个请求等待的最长时间（秒），默认 120 秒。" "120" "请求超时"
    let retriesBox, retriesField = field "失败重试次数" "网络或偶发错误时的重试次数（0-10），默认 2 次。" "2" "失败重试"
    let visionYesBox, visionYesField = field "指定能看图的模型" "每行一个，必须在模型列表中。自动判断错了才需要填。" "留空则自动判断" "指定能看图的模型"
    let visionNoBox, visionNoField = field "指定只吃文字的模型" "每行一个。这里的模型带图时会在发送前拦下。" "留空则自动判断" "指定只吃文字的模型"
    let visionCurrent = Ui.caption ""
    let summary = Ui.caption ""
    let presetHint = Ui.caption ""
    let kindCaption = Ui.caption ""
    let presetCaption = Ui.smallText ""
    let probeStatus = Ui.caption ""
    let probeNeedsSave = Ui.caption "新的模型服务要先保存，才能自动获取模型列表；现在可以手动填写模型名。"
    let enabledRow, readEnabled, writeEnabled = Ui.switchRow "启用这个模型服务" "" true ignore
    let idleText = if isNew then "添加" else "保存"
    let probeButton = Ui.button "secondary" "获取模型列表" probeClicked.Trigger
    let transportButton = Ui.button "secondary" "" transportClicked.Trigger
    let cancelButton = Ui.button "ghost" "取消" cancel.Trigger
    let saveButton = Ui.button "primary" idleText (fun () -> if not pending then save.Trigger())
    let advancedPanel = StackPanel(Orientation = Orientation.Vertical, IsVisible = false)
    let mutable flipAdvanced: unit -> unit = ignore
    let advancedToggle = Ui.button "ghost" "高级设置 ▼" (fun () -> flipAdvanced ())
    let presetButton = Button()
    let trySave () = if not pending then save.Trigger()

    let setAdvanced (expanded: bool) =
        advanced <- expanded
        advancedPanel.IsVisible <- expanded
        advancedToggle.Content <- (if expanded then "收起高级设置 ▲" else "高级设置 ▼")
        let help = if expanded then "收起高级设置" else "展开高级设置（稳定标识、模型列表、超时与重试）"
        AutomationProperties.SetHelpText(advancedToggle, help)
        ToolTip.SetTip(advancedToggle, help)

    do flipAdvanced <- fun () -> setAdvanced (not advanced)

    let view =
        keyBox.PasswordChar <- '●'
        multiline modelsBox
        modelsBox.Bind(Layoutable.MinHeightProperty, Dyn.res "Metric.TextAreaMinHeight") |> ignore
        multiline visionYesBox
        multiline visionNoBox
        AutomationProperties.SetAutomationId(visionYesBox, "provider-vision-yes")
        AutomationProperties.SetAutomationId(visionNoBox, "provider-vision-no")
        AutomationProperties.SetAutomationId(visionCurrent, "provider-vision-current")
        summary.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        summary.IsVisible <- false
        summary.Focusable <- true
        AutomationProperties.SetName(summary, "表单错误摘要")
        AutomationProperties.SetLiveSetting(summary, AutomationLiveSetting.Assertive)
        presetHint.IsVisible <- false
        probeStatus.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        probeStatus.IsVisible <- false
        probeNeedsSave.IsVisible <- isNew
        AutomationProperties.SetAutomationId(probeButton, "provider-probe-models")
        probeButton.MinWidth <- 96.0
        saveButton.MinWidth <- 96.0
        if isNew then probeButton.ApplyEnabled false
        transportButton.HorizontalAlignment <- HorizontalAlignment.Left
        transportButton.IsVisible <- false
        // 预设选择器（旧 Menu.selectButton）：读屏 ComboBox，点开由组合者弹分组菜单
        presetCaption.VerticalAlignment <- VerticalAlignment.Center
        presetCaption.TextTrimming <- TextTrimming.CharacterEllipsis
        presetCaption.MaxWidth <- 260.0
        let chevron = TextBlock(Text = "▼", FontSize = 10.0)
        chevron.VerticalAlignment <- VerticalAlignment.Center
        chevron.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
        let presetStack = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        presetStack.Children.Add presetCaption
        presetStack.Children.Add chevron
        presetButton.Content <- presetStack
        presetButton.Classes.Add "surface"
        presetButton.Classes.Add "select-button"
        presetButton.Classes.Add "hover-bordered"
        presetButton.Click.Add(fun _ -> presetClicked.Trigger(presetButton :> Control))
        AutomationProperties.SetName(presetButton, "预设")
        AutomationProperties.SetControlTypeOverride(presetButton, Nullable AutomationControlType.ComboBox)
        // Down / Up 也能展开（旧 Menu.selectButton 的约定，与菜单内 Up/Down 对称）。
        presetButton.KeyDown.Add(fun e ->
            if presetButton.IsEnabled && (e.Key = Key.Down || e.Key = Key.Up) then
                e.Handled <- true
                presetClicked.Trigger(presetButton :> Control))
        presetButton.HorizontalAlignment <- HorizontalAlignment.Stretch
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭模型服务设置 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭 (Esc)")
        AutomationProperties.SetHelpText(saveButton, sprintf "确认%s模型服务设置（Ctrl / ⌘ + Enter）" idleText)
        ToolTip.SetTip(saveButton, sprintf "确认%s（Ctrl / ⌘ + Enter）" idleText)
        let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        row.Children.Add cancelButton
        row.Children.Add saveButton
        row.HorizontalAlignment <- HorizontalAlignment.Right
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Key.Right && saveButton.IsEnabled then
                e.Handled <- true
                saveButton.Focus(NavigationMethod.Directional) |> ignore)
        saveButton.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus(NavigationMethod.Directional) |> ignore)
        let ctrl (e: KeyEventArgs) = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
        // 单行字段回车即提交；多行框的回车是换行
        for box in [ idBox; labelBox; urlBox; keyBox; defaultModelBox; timeoutBox; retriesBox ] do
            box.KeyDown.Add(fun e ->
                if e.Key = Key.Enter && not (ctrl e) then
                    e.Handled <- true
                    trySave ())
        advancedPanel.Spacing <- 16.0
        let probeStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        probeStack.Children.Add probeButton
        probeStack.Children.Add probeNeedsSave
        probeStack.Children.Add probeStatus
        for c in
            [ transportButton :> Control; idField; modelsField; defaultModelField
              probeStack
              visionCurrent; visionYesField; visionNoField; timeoutField; retriesField; enabledRow ] do
            advancedPanel.Children.Add c
        advancedToggle.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetName(advancedToggle, "高级设置")
        setAdvanced false
        let midFields = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        midFields.Children.Add labelField
        midFields.Children.Add keyField
        midFields.Children.Add urlField
        let form = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        form.Children.Add(Ui.title (if isNew then "添加模型服务" else "编辑模型服务"))
        form.Children.Add(Ui.fieldGroup "预设" "" presetButton None)
        form.Children.Add kindCaption
        form.Children.Add presetHint
        form.Children.Add summary
        form.Children.Add midFields
        form.Children.Add advancedToggle
        form.Children.Add advancedPanel
        form.Children.Add(Ui.hairline())
        form.Children.Add row
        form.KeyDown.Add(fun e ->
            if e.Key = Key.Enter && ctrl e then
                e.Handled <- true
                trySave ())
        ScrollViewer(Content = form, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto)

    member _.View = view :> Control
    member _.IdBox = idBox
    member _.LabelBox = labelBox
    member _.UrlBox = urlBox
    member _.KeyBox = keyBox
    member _.ModelsBox = modelsBox
    member _.DefaultModelBox = defaultModelBox
    member _.TimeoutBox = timeoutBox
    member _.RetriesBox = retriesBox
    member _.VisionYesBox = visionYesBox
    member _.VisionNoBox = visionNoBox
    /// 全部输入框（摘要复位用）
    member _.Boxes = [ idBox; labelBox; urlBox; keyBox; modelsBox; defaultModelBox; visionYesBox; visionNoBox; timeoutBox; retriesBox ]
    /// 落在高级区的输入框（出错时组合者先展开高级区）
    member _.AdvancedBoxes = [ idBox; modelsBox; defaultModelBox; timeoutBox; retriesBox; visionYesBox; visionNoBox ]
    member _.Summary = summary
    /// 首个焦点：名称框
    member _.InitialFocus = labelBox :> Control
    member _.IsPending = pending
    member _.IsAdvanced = advanced
    member _.Enabled = readEnabled ()

    /// 编辑已有服务时标识只读
    member _.LockId() = idBox.IsEnabled <- false

    /// 预设标题、预设说明、传输说明与传输切换键：一次性投影（预设说明空则隐藏；传输键 None 为隐藏）
    member _.ApplyPresetCaption(label: string, hint: string, kind: string, transport: string option) =
        presetCaption.Text <- label
        presetHint.Text <- hint
        presetHint.IsVisible <- not (String.IsNullOrWhiteSpace hint)
        kindCaption.Text <- kind
        match transport with
        | Some t ->
            transportButton.ApplyText t
            transportButton.IsVisible <- true
        | None -> transportButton.IsVisible <- false

    /// 看图能力的当前判断与手动指定的两框：一次性投影
    member _.ApplyVisionCurrent(text: string, inheritOverride: string, customOverride: string) =
        visionCurrent.Text <- text
        visionYesBox.Text <- inheritOverride
        visionNoBox.Text <- customOverride

    /// 获取模型列表：往返中换「正在获取…」并禁用；探活失败的常驻行（空则隐藏）
    member _.ApplyProbeState(pending: bool, status: string) =
        let text = if pending then "正在获取…" else "获取模型列表"
        probeButton.ApplyText text
        probeButton.ApplyEnabled(not pending && not isNew)
        AutomationProperties.SetHelpText(probeButton, (if pending then text else ""))
        probeStatus.Text <- status
        probeStatus.IsVisible <- not (String.IsNullOrWhiteSpace text)

    /// 保存往返中：保存键换「正在保存…」并禁用，取消也禁用；重复的 Ctrl+Enter 不再提交
    member _.ApplyPending(value: bool) =
        pending <- value
        let text = if value then "正在保存…" else idleText
        saveButton.ApplyText text
        saveButton.ApplyEnabled(not value)
        AutomationProperties.SetHelpText(saveButton, (if value then text else sprintf "确认%s模型服务设置（Ctrl / ⌘ + Enter）" idleText))
        cancelButton.ApplyEnabled(not value)

    /// 「启用这个模型服务」开关的值
    member _.ApplyEnabledValue(v: bool) = writeEnabled v

    /// 强制展开高级区（表单错误落在收起的高级区时，组合者先展开）
    member _.ApplyAdvanced(expanded: bool) = setAdvanced expanded

    /// 预设选择器被点（带锚点）
    member _.PresetClicked = presetClicked.Publish
    member _.TransportClicked = transportClicked.Publish
    member _.ProbeClicked = probeClicked.Publish
    member _.SaveRequested = save.Publish
    member _.CancelRequested = cancel.Publish
