module Wanxiang2.OnboardingPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.OnboardingPane
open Wanxiang2.OnboardingPane.Tests.Probe

let private look () =
    let body = Cell 14.0
    let gap = Cell 12.0
    body, gap,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = body; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = gap; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      Space3xl = Cell 24.0; CardMaxWidth = Cell 520.0; PendingMinWidth = Cell 96.0 }

let private idle =
    { Chips = [ { Key = "openai"; Label = "OpenAI"; Selected = true }; { Key = "anthropic"; Label = "Claude"; Selected = false } ]
      MoreLabel = "更多"; Hint = ""; KeyVisible = true; CustomUrlVisible = false; Busy = false
      StatusText = ""; StatusIsError = false; SwitchLabel = None; Discovered = None }

let private pane () =
    let _, _, l = look ()
    let p = OnboardingPane l
    let _ = host p.View
    p.Render idle
    p

[<Fact>]
let ``密钥为空时不发起任何请求并就地说明`` () =
    Headless.run (fun () ->
        let p = pane ()
        let connects = ResizeArray<unit>()
        p.ConnectClicked.Add connects.Add
        click (named p.View "连接").Value
        Assert.Equal(1, connects.Count)
        // 组合者判出缺密钥：只出状态行，把焦点送进密钥框
        p.Render { idle with StatusText = "请先粘贴 API Key。"; StatusIsError = true }
        p.FocusInitial true
        let status = (named p.View "连接状态").Value :?> TextBlock
        Assert.True status.IsVisible
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting status)
        Assert.True p.KeyBox.IsFocused
        // 密钥框里回车同样是连接
        p.KeyBox.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter))
        Assert.Equal(2, connects.Count))

[<Fact>]
let ``连接中按钮保持几何只换文字并禁用，选择键与更多不再响应`` () =
    Headless.run (fun () ->
        let p = pane ()
        let mores = ResizeArray<Control>()
        p.MoreClicked.Add mores.Add
        p.Render { idle with Busy = true }
        let button = (named p.View "连接").Value
        Assert.False button.IsEnabled
        Assert.Equal("正在连接…", AutomationProperties.GetHelpText button)
        Assert.Equal(96.0, button.MinWidth)
        Assert.False p.KeyBox.IsEnabled
        click (named p.View "更多模型服务").Value
        Assert.Empty mores
        p.Render idle
        click (named p.View "更多模型服务").Value
        Assert.Single mores |> ignore)

[<Fact>]
let ``路径错时改用兼容端点后重试，载荷换成兼容传输`` () =
    Headless.run (fun () ->
        let p = pane ()
        let switches = ResizeArray<unit>()
        p.SwitchClicked.Add switches.Add
        Assert.False (named p.View "改用兼容端点").Value.IsVisible
        p.Render { idle with SwitchLabel = Some "改用 OpenAI 兼容端点"; StatusText = "地址不对"; StatusIsError = true }
        let b = (named p.View "改用 OpenAI 兼容端点").Value
        Assert.True b.IsVisible
        click b
        Assert.Equal(1, switches.Count))

[<Fact>]
let ``发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`` () =
    Headless.run (fun () ->
        let p = pane ()
        let uses = ResizeArray<unit>()
        p.UseLocalClicked.Add uses.Add
        p.Render { idle with Discovered = Some "本机 Ollama 正在运行，已装 2 个模型。" }
        Assert.Contains("已装 2 个模型", textOf p.View)
        click (named p.View "一键使用本机 Ollama").Value
        Assert.Equal(1, uses.Count)
        p.Render { idle with KeyVisible = false; Chips = [ { Key = "ollama"; Label = "Ollama"; Selected = true } ] }
        Assert.False (p.KeyBox.Parent.Parent :?> Control).IsVisible)

[<Fact>]
let ``没有 Ollama 时不显示一键使用`` () =
    Headless.run (fun () ->
        let p = pane ()
        Assert.False (named p.View "一键使用本机 Ollama").Value.IsEffectivelyVisible)

[<Fact>]
let ``服务商键选中为主色，点击发出预设 key`` () =
    Headless.run (fun () ->
        let p = pane ()
        let keys = ResizeArray<string>()
        p.ChipClicked.Add keys.Add
        let claude = (named p.View "Claude").Value
        Assert.Contains("button-secondary", claude.Classes)
        Assert.Contains("button-primary", (named p.View "OpenAI").Value.Classes)
        click claude
        Assert.Equal<string list>([ "anthropic" ], List.ofSeq keys))

[<Fact>]
let ``look values stay live after the card is shown`` () =
    Headless.run (fun () ->
        let body, gap, l = look ()
        let p = OnboardingPane l
        let _ = host p.View
        p.Render idle
        body.Set 19.0
        gap.Set 3.0
        Assert.Equal(19.0, p.KeyBox.FontSize)
        Assert.Equal(Thickness(0.0, 0.0, 3.0, 3.0), (named p.View "Claude").Value.Margin))
