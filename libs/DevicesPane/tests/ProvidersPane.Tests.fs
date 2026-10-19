module Wanxiang2.ProvidersPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ProvidersPane
open Wanxiang2.ProvidersPane.Tests.Probe

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
      SpaceXXs = Cell 2.0; TextAreaMinHeight = Cell 72.0; EmptyLogoSize = Cell 40.0; PendingMinWidth = Cell 96.0
      StatusDotSize = Cell 7.0; SelectCaptionMaxWidth = Cell 260.0; SelectPaddingY = Cell 6.0 }

let private row id label : ProviderRow =
    { Id = id; Label = label; Meta = "https://api.openai.com/v1 · 2 个模型"; Enabled = true; Dot = ProviderDot.Configured; IsDefault = false }

let private enter (box: TextBox) =
    box.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter))

[<Fact>]
let ``missing key reads as warning tag and disabled provider dims its text`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let p = ProvidersPane l
        let _ = host p.View
        p.Render
            [ { row "a" "缺钥" with Dot = ProviderDot.MissingKey; Meta = "x · 1 个模型 · 未设置密钥" }
              { row "b" "停用" with Dot = ProviderDot.Disabled; Enabled = false }
              { row "c" "默认" with IsDefault = true } ]
        let tags = descendants p.View |> Seq.filter (fun c -> c.Classes.Contains "tag") |> List.ofSeq
        let warning = tags |> List.find (fun t -> textOf t = "缺少密钥")
        Assert.Contains("tag-warning", warning.Classes)
        let disabled = tags |> List.find (fun t -> textOf t = "已停用")
        Assert.DoesNotContain("tag-warning", disabled.Classes)
        Assert.True(tags |> List.exists (fun t -> textOf t = "默认"))
        let name = descendants p.View |> Seq.pick (fun c -> match c with :? TextBlock as t when t.Text = "停用" -> Some t | _ -> None)
        Assert.Same(l.Muted, name.Foreground)
        let dots = descendants p.View |> Seq.choose (fun c -> match c with :? Ellipse as e -> Some e | _ -> None) |> List.ofSeq
        Assert.Equal<IBrush list>([ l.Warning; l.Faint; l.Accent ], dots |> List.map (fun e -> e.Fill))
        Assert.All(dots, fun d -> Assert.Equal(7.0, d.Width)))

[<Fact>]
let ``empty provider list hands the add action to the empty state`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let p = ProvidersPane l
        let _ = host p.View
        let adds = ResizeArray<unit>()
        p.AddClicked.Add adds.Add
        p.Render []
        let visibleAdds = descendants p.View |> Seq.filter (fun c -> AutomationProperties.GetName c = "添加模型服务" && c.IsEffectivelyVisible) |> List.ofSeq
        Assert.Single visibleAdds |> ignore
        click visibleAdds.Head
        p.Render [ row "a" "OpenAI" ]
        let edits, mores = ResizeArray<string>(), ResizeArray<string>()
        p.EditClicked.Add edits.Add
        p.MoreClicked.Add(fun (id, _) -> mores.Add id)
        click (named p.View "编辑").Value
        click (named p.View "更多").Value
        click (named p.View "添加模型服务").Value
        Assert.Equal(2, adds.Count)
        Assert.Equal<string list>([ "a" ], List.ofSeq edits)
        Assert.Equal<string list>([ "a" ], List.ofSeq mores))

[<Fact>]
let ``SettingsProviders two-tier form defaults to quick access and preserves drafts when toggling advanced settings`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let e = ProviderEditor(l, true, (fun _ -> None))
        let _ = host e.View
        Assert.False e.IsAdvanced
        Assert.True e.LabelBox.IsEffectivelyVisible
        Assert.False e.IdBox.IsEffectivelyVisible
        e.IdBox.Text <- "my-id"
        let toggle = (named e.View "高级设置").Value
        click toggle
        Assert.True e.IsAdvanced
        Assert.True e.IdBox.IsEffectivelyVisible
        Assert.Equal("收起高级设置", AutomationProperties.GetHelpText toggle)
        click toggle
        Assert.False e.IsAdvanced
        Assert.Equal("my-id", e.IdBox.Text)
        Assert.Same(e.LabelBox, e.InitialFocus))

[<Fact>]
let ``SettingsProviders ShowEditor validates required fields and default model existence`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let lines = Collections.Generic.Dictionary<TextBox, TextBlock>()
        let e = ProviderEditor(l, true, (fun b -> let t = TextBlock(IsVisible = false) in lines[b] <- t; Some(t :> Control)))
        let _ = host e.View
        Assert.Equal(10, lines.Count)
        let saves = ResizeArray<unit>()
        e.SaveRequested.Add saves.Add
        enter e.LabelBox
        enter e.KeyBox
        Assert.Equal(2, saves.Count)
        // 多行模型框的回车是换行
        enter e.ModelsBox
        Assert.Equal(2, saves.Count)
        e.SetPending true
        Assert.False (named e.View "正在保存…").Value.IsEnabled
        Assert.False (named e.View "取消").Value.IsEnabled
        e.Summary.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control))
        Assert.Equal(2, saves.Count)
        e.SetPending false
        e.Summary.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control))
        Assert.Equal(3, saves.Count)
        Assert.Equal('●', e.KeyBox.PasswordChar))

[<Fact>]
let ``new provider cannot probe until saved and explains why`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let e = ProviderEditor(l, true, (fun _ -> None))
        let _ = host e.View
        e.SetAdvanced true
        let probe = (byId e.View "provider-probe-models").Value
        Assert.False probe.IsEnabled
        Assert.Contains("要先保存", textOf e.View)
        let e2 = ProviderEditor(l, false, (fun _ -> None))
        let _ = host e2.View
        e2.SetAdvanced true
        let probes = ResizeArray<unit>()
        e2.ProbeClicked.Add probes.Add
        let probe2 = (byId e2.View "provider-probe-models").Value
        click probe2
        Assert.Single probes |> ignore
        e2.SetProbePending true
        Assert.False probe2.IsEnabled
        Assert.Equal("正在获取…", AutomationProperties.GetName probe2)
        e2.SetProbePending false
        e2.SetProbeStatus "密钥无效"
        Assert.Contains("密钥无效", textOf e2.View)
        Assert.True((named e2.View "保存").IsSome))

[<Fact>]
let ``SettingsProviders applyPreset updates endpoint, models and tracks divergence to custom`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let e = ProviderEditor(l, true, (fun _ -> None))
        let _ = host e.View
        let anchors, transports = ResizeArray<Control>(), ResizeArray<unit>()
        e.PresetClicked.Add anchors.Add
        e.TransportClicked.Add transports.Add
        e.SetPresetLabel "OpenAI"
        e.SetPresetHint "去 platform.openai.com 生成密钥。"
        e.SetKindText "传输：OpenAI 兼容 · /v1/chat/completions"
        let selector = (named e.View "预设").Value
        Assert.Contains("OpenAI", textOf selector)
        click selector
        Assert.Same(selector, anchors[0])
        Assert.Contains("platform.openai.com", textOf e.View)
        e.SetPresetLabel "自定义"
        e.SetPresetHint ""
        Assert.DoesNotContain("platform.openai.com", textOf e.View)
        e.SetAdvanced true
        e.SetTransport(Some "改用兼容端点")
        click (named e.View "改用兼容端点").Value
        Assert.Single transports |> ignore
        e.SetTransport None
        Assert.False (named e.View "改用兼容端点").Value.IsVisible)

[<Fact>]
let ``look values stay live in the editor`` () =
    Headless.run (fun () ->
        let body, gap, l = look ()
        let e = ProviderEditor(l, false, (fun _ -> None))
        let _ = host e.View
        body.Set 18.0
        gap.Set 9.0
        Assert.Equal(18.0, e.LabelBox.FontSize))

// 旧 Menu.selectButton：Down / Up 都能展开预设菜单（UiStabilityTests `preset dropdown marks exactly the current preset` 的展开路径）。
[<Fact>]
let ``预设选择器按 Down 或 Up 展开`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let e = ProviderEditor(l, true, (fun _ -> None))
        let _ = host e.View
        let anchors = ResizeArray<Control>()
        e.PresetClicked.Add anchors.Add
        let selector = (named e.View "预设").Value
        for k in [ Avalonia.Input.Key.Down; Avalonia.Input.Key.Up ] do
            selector.RaiseEvent(Avalonia.Input.KeyEventArgs(RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = k))
        Assert.Equal(2, anchors.Count))
