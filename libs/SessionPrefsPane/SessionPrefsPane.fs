namespace Wanxiang2.SessionPrefsPane

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

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
type SessionPrefsPane(look: SessionPrefsPaneLook, modelPicker: Control, init: SessionPrefsInit, validationFor: TextBox -> Control option) =
    let kit = Kit(look)
    let save = Event<SessionPrefsDraft>()
    let cancelled = Event<unit>()
    let selected = HashSet<string>(init.Tools |> List.filter (fun t -> t.Enabled) |> List.map (fun t -> t.Id))
    let text (b: TextBox) = if isNull b.Text then "" else b.Text
    let instructionsShell, instructionsBox = kit.TextArea("留空则使用主机默认指令", look.TextAreaMinHeight)
    let field placeholder (value: string) =
        let shell, box = kit.TextField placeholder
        box.Text <- value
        shell, box
    let tShell, temperatureBox = field "0–2，留空跟随默认" init.Temperature
    let pShell, topPBox = field "0–1，留空跟随默认" init.TopP
    let mShell, maxTokensBox = field "留空不限制" init.MaxTokens
    let bShell, thinkingBudgetBox = field "留空跟随默认；0 关闭思维链" init.ThinkingBudget
    let boxes = [ temperatureBox; topPBox; maxTokensBox; thinkingBudgetBox ]
    let summary =
        let t = kit.Caption ""
        t.Foreground <- look.Danger
        t.IsVisible <- false
        t.Focusable <- true
        AutomationProperties.SetName(t, "表单错误摘要")
        AutomationProperties.SetLiveSetting(t, AutomationLiveSetting.Assertive)
        t
    let mutable pending = false
    let draft () =
        { Instructions = text instructionsBox
          Temperature = text temperatureBox
          TopP = text topPBox
          MaxTokens = text maxTokensBox
          ThinkingBudget = text thinkingBudgetBox
          Tools = init.Tools |> List.filter (fun t -> selected.Contains t.Id) |> List.map (fun t -> t.Id) }
    let trySave () = if not pending then save.Trigger(draft ())
    let footer = kit.DialogFooter("保存", Tone.Primary, (fun () -> cancelled.Trigger()), trySave)

    let toolsPanel =
        let p = StackPanel(Orientation = Orientation.Vertical)
        kit.Live(look.SpaceXs, fun v -> p.Spacing <- v)
        if List.isEmpty init.Tools then p.Children.Add(kit.Caption "主机当前没有可用工具。")
        else
            for tool in init.Tools do
                let track, _, _, _ =
                    kit.Toggle(tool.Enabled, fun v -> if v then selected.Add tool.Id |> ignore else selected.Remove tool.Id |> ignore)
                AutomationProperties.SetName(track, sprintf "启用工具 %s" tool.Label)
                let caption = kit.Text(tool.Label, look.SmallFontSize, look.Text)
                caption.VerticalAlignment <- VerticalAlignment.Center
                let left = kit.HStack(look.SpaceMd, [ caption :> Control; kit.Tag(TagTone.Neutral, tool.Source) :> Control ])
                let row = DockPanel(LastChildFill = true)
                DockPanel.SetDock(track, Dock.Right)
                row.Children.Add track
                row.Children.Add left
                p.Children.Add row
        p

    let group label hint (shell: Border) (box: TextBox) =
        AutomationProperties.SetName(box, label)
        kit.FieldGroup(label, hint, shell, validationFor box)

    let paramGrid =
        let g = UniformGrid(Columns = 2)
        for c in
            [ group "Temperature" "0–2，留空跟随主机默认" tShell temperatureBox
              group "Top P" "0–1，留空跟随主机默认" pShell topPBox
              group "最大输出 token" "限制单次回复，留空不限制" mShell maxTokensBox
              group "思维链预算（token）" "0 关闭思维链，留空跟随默认" bShell thinkingBudgetBox ] do
            kit.Live(Kit.Pair(look.SpaceXl, look.Space2xl), fun (x, y) -> c.Margin <- Thickness(0.0, 0.0, x, y))
            g.Children.Add c
        g

    let advanced =
        let e =
            Expander(
                Header = kit.Title(if init.Customized then "生成参数 · 已自定义" else "生成参数 · 跟随默认值"),
                Content = paramGrid,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch)
        AutomationProperties.SetName(e, "生成参数")
        e
    let tools =
        let e =
            Expander(
                Header = kit.Title(sprintf "工具 · 已启用 %d 项" selected.Count),
                Content = toolsPanel,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch)
        AutomationProperties.SetName(e, "对话工具")
        // 计数在展开 / 收起时更新，不重建开关
        e.PropertyChanged.Add(fun a ->
            if a.Property = Expander.IsExpandedProperty then e.Header <- kit.Title(sprintf "工具 · 已启用 %d 项" selected.Count))
        e

    let view =
        instructionsBox.Text <- init.Instructions
        AutomationProperties.SetName(instructionsBox, "系统指令")
        AutomationProperties.SetHelpText(footer.Confirm, "保存对话设置（Ctrl / ⌘ + Enter）")
        ToolTip.SetTip(footer.Confirm, "保存对话设置（Ctrl / ⌘ + Enter）")
        kit.Live(look.PendingMinWidth, fun v -> footer.Confirm.MinWidth <- v)
        kit.Live(look.SpaceMd, footer.Spacing)
        modelPicker.HorizontalAlignment <- HorizontalAlignment.Stretch
        // 一改即收起摘要，不再播报已修正的旧错误
        for b in boxes do b.TextChanged.Add(fun _ -> summary.IsVisible <- false; summary.Text <- "")
        let content =
            kit.VStack(
                look.Space2xl,
                [ summary :> Control
                  kit.FieldGroup("模型", "", modelPicker, None)
                  kit.FieldGroup("系统指令", "", instructionsShell, None)
                  tools :> Control
                  advanced :> Control ])
        let heading = kit.VStack(look.SpaceXs, [ kit.Heading "对话上下文" :> Control; kit.Caption "只影响当前对话。保存后用于后续请求，不改动已有消息。" :> Control ])
        kit.Live(look.Space2xl, fun v ->
            heading.Margin <- Thickness(0.0, 0.0, 0.0, v)
            footer.Row.Margin <- Thickness(0.0, v, 0.0, 0.0))
        let dock = DockPanel(LastChildFill = true)
        kit.Live(look.ContentMaxHeight, fun v -> dock.MaxHeight <- v)
        DockPanel.SetDock(heading, Dock.Top)
        DockPanel.SetDock(footer.Row, Dock.Bottom)
        dock.Children.Add heading
        dock.Children.Add footer.Row
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
    member _.SaveButton = footer.Confirm :> Control
    member _.CancelButton = footer.Cancel :> Control
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
        footer.Confirm.SetText(if value then "正在保存…" else "保存")
        footer.Confirm.SetEnabled(not value)
        footer.Cancel.SetEnabled(not value)

    [<CLIEvent>] member _.SaveRequested = save.Publish
    [<CLIEvent>] member _.Cancelled = cancelled.Publish
