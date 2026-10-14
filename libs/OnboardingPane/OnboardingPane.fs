namespace Wanxiang2.OnboardingPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 一枚服务商选择键
type OnboardingChip =
    { Key: string
      Label: string
      Selected: bool }

/// 卡片的一帧（组合者从 OnboardingFlow 与所选预设得出）
type OnboardingPaneState =
    { Chips: OnboardingChip list
      /// 「更多」键文字（选中了折叠里的一家时是「更多：xx」）
      MoreLabel: string
      /// 所选预设的说明（空则不显示）
      Hint: string
      KeyVisible: bool
      CustomUrlVisible: bool
      Busy: bool
      StatusText: string
      StatusIsError: bool
      /// 兼容端点切换键（None 为不显示）
      SwitchLabel: string option
      /// 本机发现的说明（None 为不显示发现区）
      Discovered: string option }

/// 聊天主界面上的「添加一个模型服务」卡片（迁自旧 Views/OnboardingCard.fs 的界面部分）。
/// 步骤、写回、探测、发现、预设清单与「更多」菜单都由组合者接（OnboardingFlow、ProviderPresets、OnboardingRules、PopupMenu）。
type OnboardingPane(look: OnboardingPaneLook) =
    let kit = Kit(look)
    let chipClicked = Event<string>()
    let moreClicked = Event<Control>()
    let connect = Event<unit>()
    let switchClicked = Event<unit>()
    let advancedClicked = Event<unit>()
    let useLocal = Event<unit>()

    let chipRow = WrapPanel(Orientation = Orientation.Horizontal)
    let title = kit.Title "添加一个模型服务"
    let subtitle = kit.Caption "主机已连上。再选一家模型服务、粘贴密钥，就能开始对话；密钥只保存在主机上。"
    let _, customUrlBox = kit.TextField "https://example.com/v1"
    let customUrlGroup = kit.FieldGroup("服务地址", "兼容 OpenAI 的接口地址，通常以 /v1 结尾。", (customUrlBox.Parent :?> Control), None)
    let _, keyBox = kit.TextField "粘贴 API Key"
    let keyGroup = kit.FieldGroup("API Key", "只保存在主机上，界面不会回显。", (keyBox.Parent :?> Control), None)
    let hintText = kit.Caption ""
    let statusText = kit.Caption ""
    let discoveryPanel = StackPanel(Orientation = Orientation.Vertical, IsVisible = false)
    let discoveryText = kit.Caption ""
    let connectButton = kit.Button(Tone.Primary, "连接", connect.Trigger)
    let switchButton = kit.Button(Tone.Secondary, "改用兼容端点", switchClicked.Trigger)
    let advanced = kit.Button(Tone.Ghost, "需要更多选项？打开设置", advancedClicked.Trigger)
    let useLocalButton = kit.Button(Tone.Secondary, "一键使用本机 Ollama", useLocal.Trigger)
    let mutable busy = false
    let chipMargin (b: Control) = kit.Live(look.SpaceMd, fun v -> b.Margin <- Thickness(0.0, 0.0, v, v))

    let card =
        let form = StackPanel(Orientation = Orientation.Vertical)
        kit.Live(look.SpaceXl, fun v -> form.Spacing <- v)
        advanced.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetName(advanced, "高级设置")
        connectButton.HorizontalAlignment <- HorizontalAlignment.Left
        kit.Live(look.PendingMinWidth, fun v -> connectButton.MinWidth <- v)
        switchButton.IsVisible <- false
        switchButton.HorizontalAlignment <- HorizontalAlignment.Left
        statusText.IsVisible <- false
        statusText.Focusable <- true
        AutomationProperties.SetLiveSetting(statusText, AutomationLiveSetting.Assertive)
        AutomationProperties.SetName(statusText, "连接状态")
        AutomationProperties.SetName(keyBox, "API Key")
        keyBox.PasswordChar <- '●'
        keyBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                connect.Trigger())
        kit.Live(look.SpaceMd, fun v -> discoveryPanel.Spacing <- v)
        useLocalButton.HorizontalAlignment <- HorizontalAlignment.Left
        discoveryPanel.Children.Add discoveryText
        discoveryPanel.Children.Add useLocalButton
        for c in [ title :> Control; subtitle; discoveryPanel; chipRow; hintText; customUrlGroup; keyGroup; statusText; switchButton; connectButton; advanced ] do
            form.Children.Add c
        let card = kit.Card(look.Space3xl, look.Space3xl, look.RadiusLg, form)
        kit.Live(look.CardMaxWidth, fun v -> card.MaxWidth <- v)
        // 拉伸到最宽（受限后居中）：出现错误提示或切换服务商时卡片不忽宽忽窄
        card.HorizontalAlignment <- HorizontalAlignment.Stretch
        AutomationProperties.SetName(card, "添加一个模型服务")
        card

    member _.View = card :> Control
    member _.KeyBox = keyBox
    member _.CustomUrlBox = customUrlBox
    member _.ConnectButton = connectButton :> Control

    /// 输入框里的文字（去首尾空白）
    member _.KeyText = if isNull keyBox.Text then "" else keyBox.Text.Trim()
    member _.CustomUrlText = if isNull customUrlBox.Text then "" else customUrlBox.Text.Trim()
    member _.ClearKey() = keyBox.Text <- ""

    member _.Render(state: OnboardingPaneState) =
        busy <- state.Busy
        chipRow.Children.Clear()
        for chip in state.Chips do
            let b = kit.Button((if chip.Selected then Tone.Primary else Tone.Secondary), chip.Label, (fun () -> chipClicked.Trigger chip.Key))
            chipMargin b
            AutomationProperties.SetHelpText(b, "选择这家模型服务：" + chip.Label)
            chipRow.Children.Add b
        let mutable more: Tap = Unchecked.defaultof<_>
        more <- kit.Button(Tone.Ghost, state.MoreLabel, (fun () -> if not busy then moreClicked.Trigger(more :> Control)))
        chipMargin more
        AutomationProperties.SetName(more, "更多模型服务")
        chipRow.Children.Add more
        hintText.Text <- state.Hint
        hintText.IsVisible <- not (String.IsNullOrWhiteSpace state.Hint)
        keyGroup.IsVisible <- state.KeyVisible
        customUrlGroup.IsVisible <- state.CustomUrlVisible
        customUrlBox.IsEnabled <- not state.Busy
        keyBox.IsEnabled <- not state.Busy
        // 异步提交键保持同一几何，只换文字与可用（旧 Ui.setButtonPending）
        let text = if state.Busy then "正在连接…" else "连接"
        connectButton.SetText text
        connectButton.SetEnabled(not state.Busy)
        AutomationProperties.SetHelpText(connectButton, (if state.Busy then text else ""))
        AutomationProperties.SetName(connectButton, "连接")
        statusText.Text <- state.StatusText
        statusText.Foreground <- (if state.StatusIsError then look.Danger else look.Muted)
        statusText.IsVisible <- not (String.IsNullOrWhiteSpace state.StatusText)
        match state.SwitchLabel with
        | Some label ->
            switchButton.SetText label
            switchButton.IsVisible <- true
        | None -> switchButton.IsVisible <- false
        match state.Discovered with
        | Some text ->
            discoveryText.Text <- text
            discoveryPanel.IsVisible <- true
        | None -> discoveryPanel.IsVisible <- false

    /// 首个焦点：要密钥就进密钥框，否则落在「连接」
    member _.FocusInitial(needsKey: bool) =
        if needsKey then keyBox.Focus() |> ignore else connectButton.Focus() |> ignore

    /// 服务商键（预设 key）
    member _.ChipClicked = chipClicked.Publish
    /// 「更多」键被点（带锚点，组合者在锚点处弹菜单）；连接中不发
    member _.MoreClicked = moreClicked.Publish
    /// 「连接」或密钥框里回车
    member _.ConnectClicked = connect.Publish
    member _.SwitchClicked = switchClicked.Publish
    member _.AdvancedClicked = advancedClicked.Publish
    member _.UseLocalClicked = useLocal.Publish
