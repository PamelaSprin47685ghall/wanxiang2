namespace Wanxiang2.SessionPrefsPane

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 一个可选工具（标签已是人话名）
type SessionTool = { Id: string; Label: string; Source: string; Enabled: bool }

/// 打开时的值（数字已格式化成文字，空 = 跟随默认）
type SessionPrefsInit =
    { Instructions: string
      Temperature: string
      TopP: string
      MaxTokens: string
      ThinkingBudget: string
      Tools: SessionTool list
      /// 生成参数是否已自定义（决定折叠头文案）
      Customized: bool }

/// 保存时交出的原始输入（校验与换算归组合者）
type SessionPrefsDraft =
    { Instructions: string
      Temperature: string
      TopP: string
      MaxTokens: string
      ThinkingBudget: string
      Tools: string list }

/// 对话设置的内容（迁自旧 Views/Dialogs.fs 的 sessionSettings）：模型选择键（组合者给）、系统指令、工具开关、
/// 生成参数四个框、错误摘要（assertive、可聚焦，任一框改动即收起）、取消 / 保存。Ctrl/⌘ + Enter 与行级 Enter 保存。
type SessionPrefsPane(?modelPicker: Control, ?init: SessionPrefsInit, ?validationFor: TextBox -> Control option) =
    let initialConfig =
        defaultArg init {
            Instructions = ""
            Temperature = ""
            TopP = ""
            MaxTokens = ""
            ThinkingBudget = ""
            Tools = []
            Customized = false
        }
    let picker = defaultArg modelPicker (Button(Content = "选择模型") :> Control)
    let save = Event<SessionPrefsDraft>()
    let cancelled = Event<unit>()
    let selected = HashSet<string>(initialConfig.Tools |> List.filter (fun t -> t.Enabled) |> List.map (fun t -> t.Id))
    let text (b: TextBox) = if isNull b.Text then "" else b.Text
    let instructionsBox = TextBox(PlaceholderText = "留空则使用主机默认指令", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80.0)
    do
        instructionsBox.Classes.Add "control-stroke"
        instructionsBox.Classes.Add "text-area"
    let field (placeholder: string) (value: string) =
        let box = TextBox(PlaceholderText = placeholder)
        box.Classes.Add "control-stroke"
        box.Text <- value
        box
    let temperatureBox = field "0–2，留空跟随默认" initialConfig.Temperature
    let topPBox = field "0–1，留空跟随默认" initialConfig.TopP
    let maxTokensBox = field "留空不限制" initialConfig.MaxTokens
    let thinkingBudgetBox = field "留空跟随默认；0 关闭思维链" initialConfig.ThinkingBudget
    let boxes = [ temperatureBox; topPBox; maxTokensBox; thinkingBudgetBox ]
    let summary =
        let t = TextBlock(Text = "", TextWrapping = TextWrapping.Wrap)
        t.Classes.Add "danger"
        t.IsVisible <- false
        t.Focusable <- true
        AutomationProperties.SetName(t, "表单错误摘要")
        AutomationProperties.SetLiveSetting(t, AutomationLiveSetting.Assertive)
        t
    let mutable pending = false
    let mutable availableTools: SessionTool list = initialConfig.Tools
    let draft () =
        { Instructions = text instructionsBox
          Temperature = text temperatureBox
          TopP = text topPBox
          MaxTokens = text maxTokensBox
          ThinkingBudget = text thinkingBudgetBox
          Tools = availableTools |> List.filter (fun t -> selected.Contains t.Id) |> List.map (fun t -> t.Id) }
    let trySave () = if not pending then save.Trigger(draft ())

    let cancelButton = Button(Content = "取消")
    do
        cancelButton.Classes.Add "ghost"
        cancelButton.Classes.Add "button-ghost"
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭对话框 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭对话框 (Esc)")
        cancelButton.Click.Add(fun _ -> cancelled.Trigger())

    let confirmButton = Button(Content = "保存")
    do
        confirmButton.Classes.Add "primary"
        confirmButton.Classes.Add "button-primary"
        confirmButton.Click.Add(fun _ -> trySave ())

    let footerRow = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8.0)
    do
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Key.Right && confirmButton.IsEnabled then
                e.Handled <- true
                confirmButton.Focus NavigationMethod.Directional |> ignore)
        confirmButton.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus NavigationMethod.Directional |> ignore)
        footerRow.Children.Add cancelButton
        footerRow.Children.Add confirmButton

    let toolsPanel =
        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
        p

    let renderToolsList (toolsList: SessionTool list) =
        toolsPanel.Children.Clear()
        if List.isEmpty toolsList then
            let empty = TextBlock(Text = "主机当前没有可用工具。")
            empty.Classes.Add "muted"
            toolsPanel.Children.Add empty
        else
            for tool in toolsList do
                let sw = ToggleSwitch(IsChecked = Nullable(selected.Contains tool.Id), OnContent = "", OffContent = "", Focusable = true)
                AutomationProperties.SetName(sw, sprintf "启用工具 %s" tool.Label)
                let updateVal (isChecked: bool) =
                    if isChecked then selected.Add tool.Id |> ignore else selected.Remove tool.Id |> ignore
                sw.AddHandler(
                    InputElement.KeyDownEvent,
                    (fun _ (e: KeyEventArgs) ->
                        if e.Key = Key.Space || e.Key = Key.Enter then
                            e.Handled <- true
                            let next = not (sw.IsChecked.GetValueOrDefault false)
                            sw.IsChecked <- Nullable next
                            updateVal next),
                    Interactivity.RoutingStrategies.Tunnel)
                sw.IsCheckedChanged.Add(fun _ ->
                    updateVal (sw.IsChecked.GetValueOrDefault false))
                let caption = TextBlock(Text = tool.Label, VerticalAlignment = VerticalAlignment.Center)
                let tag = Border(Child = TextBlock(Text = tool.Source, FontSize = 11.0), Padding = Thickness(4.0, 2.0), CornerRadius = CornerRadius 4.0)
                tag.Classes.Add "tag"
                let left = StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0)
                left.Children.Add caption
                left.Children.Add tag
                let row = DockPanel(LastChildFill = true)
                DockPanel.SetDock(sw, Dock.Right)
                row.Children.Add sw
                row.Children.Add left
                toolsPanel.Children.Add row

    do renderToolsList initialConfig.Tools

    let group label (hint: string) (box: TextBox) =
        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        let lbl = TextBlock(Text = label, FontWeight = FontWeight.Medium)
        lbl.Classes.Add "muted"
        p.Children.Add lbl
        p.Children.Add box
        match validationFor with
        | Some vf -> vf box |> Option.iter p.Children.Add
        | None -> ()
        let h = TextBlock(Text = hint, TextWrapping = TextWrapping.Wrap)
        h.Classes.Add "muted"
        p.Children.Add h
        p
    let paramGrid =
        let g = UniformGrid(Columns = 2)
        for c in
            [ group "Temperature" "0–2，留空跟随主机默认" temperatureBox
              group "Top P" "0–1，留空跟随主机默认" topPBox
              group "最大输出 token" "限制单次回复，留空不限制" maxTokensBox
              group "思维链预算（token）" "0 关闭思维链，留空跟随默认" thinkingBudgetBox ] do
            c.Margin <- Thickness(0.0, 0.0, 16.0, 16.0)
            g.Children.Add c
        g

    let advancedHeader = TextBlock(Text = (if initialConfig.Customized then "生成参数 · 已自定义" else "生成参数 · 跟随默认值"), FontWeight = FontWeight.Medium)
    let advanced =
        let e =
            Expander(
                Header = advancedHeader,
                Content = paramGrid,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch)
        AutomationProperties.SetName(e, "生成参数")
        e
    let toolsHeader = TextBlock(Text = (sprintf "工具 · 已启用 %d 项" selected.Count), FontWeight = FontWeight.Medium)
    let tools =
        let e =
            Expander(
                Header = toolsHeader,
                Content = toolsPanel,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch)
        AutomationProperties.SetName(e, "对话工具")
        // 计数在展开 / 收起时更新，不重建开关
        e.PropertyChanged.Add(fun a ->
            if a.Property = Expander.IsExpandedProperty then
                e.Header <- TextBlock(Text = sprintf "工具 · 已启用 %d 项" selected.Count, FontWeight = FontWeight.Medium))
        e

    let view =
        instructionsBox.Text <- initialConfig.Instructions
        AutomationProperties.SetName(instructionsBox, "系统指令")
        AutomationProperties.SetHelpText(confirmButton, "保存对话设置（Ctrl / ⌘ + Enter）")
        ToolTip.SetTip(confirmButton, "保存对话设置（Ctrl / ⌘ + Enter）")
        picker.HorizontalAlignment <- HorizontalAlignment.Stretch
        // 一改即收起摘要，不再播报已修正的旧错误
        for b in boxes do b.TextChanged.Add(fun _ -> summary.IsVisible <- false; summary.Text <- "")
        let content =
            let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
            p.Children.Add summary
            let modelGroup = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
            let modelLabel = TextBlock(Text = "模型", FontWeight = FontWeight.Medium)
            modelLabel.Classes.Add "muted"
            modelGroup.Children.Add modelLabel
            modelGroup.Children.Add picker
            p.Children.Add modelGroup
            let instrGroup = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
            let instrLabel = TextBlock(Text = "系统指令", FontWeight = FontWeight.Medium)
            instrLabel.Classes.Add "muted"
            instrGroup.Children.Add instrLabel
            instrGroup.Children.Add instructionsBox
            p.Children.Add instrGroup
            p.Children.Add tools
            p.Children.Add advanced
            p
        let heading =
            let h = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
            let title = TextBlock(Text = "对话上下文", FontWeight = FontWeight.Medium)
            let desc = TextBlock(Text = "只影响当前对话。保存后用于后续请求，不改动已有消息。", TextWrapping = TextWrapping.Wrap)
            desc.Classes.Add "muted"
            h.Children.Add title
            h.Children.Add desc
            h
        heading.Margin <- Thickness(0.0, 0.0, 0.0, 16.0)
        footerRow.Margin <- Thickness(0.0, 16.0, 0.0, 0.0)
        let dock = DockPanel(LastChildFill = true)
        dock.Bind(Layoutable.MaxHeightProperty, Dyn.res "Metric.DialogContentMaxHeight") |> ignore
        DockPanel.SetDock(heading, Dock.Top)
        DockPanel.SetDock(footerRow, Dock.Bottom)
        dock.Children.Add heading
        dock.Children.Add footerRow
        dock.Children.Add(
            ScrollViewer(
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto))
        // 隧道阶段接 Ctrl/⌘ + Enter：焦点在系统指令多行框时，框自己会先把它当换行吃掉
        dock.AddHandler(
            InputElement.KeyDownEvent,
            (fun _ (e: KeyEventArgs) ->
                let ctrl = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
                if e.Key = Key.Enter && ctrl then
                    e.Handled <- true
                    trySave ()),
            Avalonia.Interactivity.RoutingStrategies.Tunnel)
        dock

    member _.View = view :> Control
    member _.InstructionsBox = instructionsBox
    member _.TemperatureBox = temperatureBox
    member _.TopPBox = topPBox
    member _.MaxTokensBox = maxTokensBox
    member _.ThinkingBudgetBox = thinkingBudgetBox
    member _.SaveButton = confirmButton :> Control
    member _.CancelButton = cancelButton :> Control
    member _.Parameters = advanced
    member _.ToolsExpander = tools
    member _.Summary = summary
    member _.IsPending = pending

    /// 多错时显示摘要（单错时组合者传 None 收起）
    member _.ShowSummary(message: string option) =
        summary.Text <- defaultArg message ""
        summary.IsVisible <- message.IsSome

    /// 有错时展开生成参数
    member _.RevealParameters() = advanced.IsExpanded <- true

    member _.SetPending(value: bool) =
        pending <- value
        let text = if value then "正在保存…" else "保存"
        confirmButton.Content <- text
        AutomationProperties.SetName(confirmButton, text)
        confirmButton.IsEnabled <- true
        cancelButton.IsEnabled <- not value

    member this.Render(vs: DialogVS) =
        match vs.SessionPrefs with
        | Some sp ->
            instructionsBox.Text <- sp.Instructions
            temperatureBox.Text <- sp.Temperature
            topPBox.Text <- sp.TopP
            maxTokensBox.Text <- sp.MaxTokens
            thinkingBudgetBox.Text <- sp.ThinkingBudget
            availableTools <- sp.Tools |> List.map (fun t -> { Id = t.Id; Label = t.Label; Source = t.Source; Enabled = t.Enabled })
            selected.Clear()
            for t in sp.Tools do if t.Enabled then selected.Add t.Id |> ignore
            renderToolsList availableTools
            advancedHeader.Text <- if sp.Customized then "生成参数 · 已自定义" else "生成参数 · 跟随默认值"
            toolsHeader.Text <- sprintf "工具 · 已启用 %d 项" selected.Count
            this.ShowSummary sp.SummaryError
            this.SetPending sp.IsPending
        | None -> ()

    [<CLIEvent>] member _.SaveRequested = save.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
