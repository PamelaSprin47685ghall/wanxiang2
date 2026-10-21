module Wanxiang2.GeneralPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.GeneralPane
open Wanxiang2.GeneralPane.Tests.Probe

let private filled =
    {
        Temperature = "0.7"
        TopP = ""
        MaxTokens = ""
        ContextMessages = "200"
        ToolRounds = "12"
        Instructions = ""
        AutoTitle = true
    }

let private enter (box: TextBox) mods =
    box.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = mods))

[<Fact>]
let ``editing a generation number stays local until explicitly saved`` () =
    Headless.run (fun () ->
        let g = GenerationSection(fun _ -> None)
        let _ = host g.View
        g.Fill filled
        let edits, saves = ResizeArray<unit>(), ResizeArray<unit>()
        g.Edited.Add edits.Add
        g.SaveRequested.Add saves.Add
        g.TemperatureBox.Text <- "0.9"
        g.TemperatureBox.Focus() |> ignore
        g.ContextBox.Focus() |> ignore
        Assert.Equal(1, edits.Count)
        Assert.Empty saves
        Assert.Equal("0.9", g.Read().Temperature)
        enter g.TemperatureBox KeyModifiers.None
        Assert.Single saves |> ignore
        // 指令框的回车是换行
        enter g.InstructionsBox KeyModifiers.None
        Assert.Single saves |> ignore

        g.View.RaiseEvent(
            KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control)
        )

        Assert.Equal(2, saves.Count))

[<Fact>]
let ``generation save button advertises the ctrl enter shortcut`` () =
    Headless.run (fun () ->
        let g = GenerationSection(fun _ -> None)
        let _ = host g.View
        let save = (named g.View "保存默认值").Value
        Assert.Equal("保存默认值（Ctrl / ⌘ + Enter）", AutomationProperties.GetHelpText save)
        Assert.Equal(96.0, save.MinWidth)

        g.Render
            { GeneralSectionVS.empty with
                Pending = true
                CanSave = true
            }

        Assert.False save.IsEnabled
        Assert.Equal("正在保存…", AutomationProperties.GetName save)
        Assert.False (named g.View "放弃修改").Value.IsEnabled

        g.Render
            { GeneralSectionVS.empty with
                CanSave = false
            }

        Assert.False save.IsEnabled

        g.Render
            { GeneralSectionVS.empty with
                CanSave = true
            }

        Assert.True save.IsEnabled)

[<Fact>]
let ``generation form fields sit inside a grouping card`` () =
    Headless.run (fun () ->
        let g = GenerationSection(fun _ -> None)
        let _ = host g.View

        let rec inCard (c: Control) =
            match c.Parent with
            | :? Border as b when b.Classes.Contains "grouping-card" -> true
            | :? Control as p -> inCard p
            | _ -> false

        for box in g.NumberBoxes @ [ g.InstructionsBox ] do
            Assert.True(inCard box)

        Assert.True(inCard (named g.View "自动生成对话标题").Value))

[<Fact>]
let ``generation form consumes shared column spacing and text metrics`` () =
    Headless.run (fun () ->
        let g = GenerationSection(fun _ -> None)
        let w = host g.View

        let grid =
            descendants g.View
            |> Seq.pick (fun c ->
                match c with
                | :? Grid as gr when gr.ColumnDefinitions.Count = 2 -> Some gr
                | _ -> None)

        Assert.Equal(20.0, grid.ColumnSpacing)
        Assert.Equal(16.0, grid.RowSpacing)
        Assert.Equal(1, Grid.GetColumn(g.TopPBox.Parent :?> Control))
        w.Close())

[<Fact>]
let ``validation rows sit in each field group and hide the hint`` () =
    Headless.run (fun () ->
        let lines = Collections.Generic.Dictionary<TextBox, TextBlock>()

        let g =
            GenerationSection(fun b ->
                let t = TextBlock(IsVisible = false) in
                lines[b] <- t
                Some(t :> Control))

        let _ = host g.View
        Assert.Equal(5, lines.Count)

        let hint =
            descendants g.View
            |> Seq.pick (fun c ->
                match c with
                | :? TextBlock as t when t.Text = "核采样阈值，0–1。" -> Some t
                | _ -> None)

        Assert.True hint.IsVisible
        lines[g.TopPBox].IsVisible <- true
        Assert.False hint.IsVisible)

[<Fact>]
let ``presets list offers rename and delete per preset and an empty explanation`` () =
    Headless.run (fun () ->
        let g = GenerationSection(fun _ -> None)
        let _ = host g.View

        g.Render
            { GeneralSectionVS.empty with
                Presets = []
            }

        Assert.Contains("存为预设", textOf g.PresetsPanel)
        let renames, deletes = ResizeArray<string>(), ResizeArray<string>()
        g.PresetRenameClicked.Add renames.Add
        g.PresetDeleteClicked.Add deletes.Add

        g.Render
            { GeneralSectionVS.empty with
                Presets =
                    [
                        {
                            Id = "p1"
                            Name = "写作"
                            Detail = "gpt · 2 个工具"
                        }
                    ]
            }

        click (named g.View "重命名预设 写作").Value
        click (named g.View "删除预设 写作").Value
        Assert.Equal<string list>([ "p1" ], List.ofSeq renames)
        Assert.Equal<string list>([ "p1" ], List.ofSeq deletes)
        Assert.False (named g.View "导入对话").Value.IsEffectivelyVisible
        g.WireImportHandler(fun () -> ())
        Assert.True (named g.View "导入对话").Value.IsEffectivelyVisible)

let private appearance =
    {
        ThemeLabel = "跟随系统"
        FontSizeText = "14.0 pt"
        CanShrink = true
        CanGrow = true
        EnterSends = true
        CollapseReasoning = false
        CodeWrap = false
        ShowArchived = false
        ReduceMotion = false
        PersistDrafts = true
        DraftSummary = "没有保存的草稿。"
    }

[<Fact>]
let ``SettingsGeneral BuildAppearance adjusts fontScale within bounds and updates prefs`` () =
    Headless.run (fun () ->
        let a = AppearanceSection()
        let _ = host a.View
        a.Render appearance
        let steps, toggles = ResizeArray<float>(), ResizeArray<AppearanceSwitch * bool>()
        a.FontStepped.Add steps.Add
        a.Toggled.Add toggles.Add
        click (named a.View "更大").Value
        click (named a.View "更小").Value
        Assert.Equal<float list>([ 0.5; -0.5 ], List.ofSeq steps)

        a.Render
            { appearance with
                FontSizeText = "20.0 pt"
                CanGrow = false
            }

        Assert.False (named a.View "更大").Value.IsEnabled
        Assert.Contains("20.0 pt", textOf a.View)
        let enterRow = (named a.View "Enter 直接发送").Value
        Assert.Equal("开启", AutomationProperties.GetItemStatus enterRow)
        click enterRow
        Assert.Equal<(AppearanceSwitch * bool) list>([ AppearanceSwitch.EnterSends, false ], List.ofSeq toggles)

        a.Render
            { appearance with
                DraftSummary = "2 条草稿"
            }

        Assert.Equal("2 条草稿", textOf (byId a.View "local-drafts-summary").Value)
        Assert.Single toggles |> ignore)

[<Fact>]
let ``theme selector reads as a combo box and opens the menu at itself`` () =
    Headless.run (fun () ->
        let a = AppearanceSection()
        let _ = host a.View
        a.Render appearance
        let anchors = ResizeArray<Control>()
        a.ThemeClicked.Add anchors.Add
        let selector = (named a.View "主题").Value
        Assert.Contains("select-button", selector.Classes)
        click selector
        Assert.Same(selector, anchors[0])
        let clears = ResizeArray<unit>()
        a.ClearDraftsClicked.Add clears.Add
        click (byId a.View "clear-local-drafts").Value
        Assert.Single clears |> ignore)

[<Fact>]
let ``SettingsGeneral BuildAbout formats system diagnostics with version and platform`` () =
    Headless.run (fun () ->
        let a = AboutSection()
        let _ = host a.View

        a.Render
            { GeneralSectionVS.empty with
                AboutRows = [ "协议版本", "3"; "客户端平台", "Desktop (.NET 10.0.0)"; "主机实例", "未连接" ]
            }

        let text = textOf a.View
        Assert.Contains("协议版本", text)
        Assert.Contains("Desktop (.NET 10.0.0)", text)
        Assert.Contains("未连接", text)
        let copies = ResizeArray<unit>()
        a.CopyDiagnosticsClicked.Add copies.Add
        click (named a.View "复制诊断报告").Value
        Assert.Single copies |> ignore)

[<Fact>]
let ``about section drops the misplaced spacer`` () =
    Headless.run (fun () ->
        let a = AboutSection()
        let _ = host a.View
        let copy = (named a.View "复制诊断报告").Value
        let column = copy.Parent :?> StackPanel
        let index = column.Children.IndexOf copy
        Assert.Contains("hairline", column.Children[index - 1].Classes))

[<Fact>]
let ``host status shows config issues and doctor results`` () =
    Headless.run (fun () ->
        let a = AboutSection()
        let _ = host a.View
        let checks = ResizeArray<unit>()
        a.CheckHostClicked.Add checks.Add

        a.Render
            { GeneralSectionVS.empty with
                Health =
                    { GeneralHealth.empty with
                        ConfigIssue = Some("config.toml 第 3 行", [ "未知键 foo" ])
                    }
            }

        Assert.Equal("config.toml 第 3 行", textOf (byId a.View "host-config-issue").Value)
        click (byId a.View "check-host").Value
        Assert.Single checks |> ignore

        a.Render
            { GeneralSectionVS.empty with
                Health =
                    { GeneralHealth.empty with
                        Doctor = AboutDoctor.Running
                    }
            }

        Assert.True((byId a.View "host-config-ok").IsSome)
        Assert.False (byId a.View "check-host").Value.IsEnabled

        a.Render
            { GeneralSectionVS.empty with
                Health =
                    { GeneralHealth.empty with
                        Doctor =
                            AboutDoctor.Report(
                                false,
                                [
                                    {
                                        Name = "disk"
                                        Label = "磁盘"
                                        Ok = false
                                        Detail = "剩余 1%"
                                    }
                                ],
                                "10:00:00"
                            )
                    }
            }

        Assert.Equal("有项目需要处理", textOf (byId a.View "doctor-headline").Value)
        Assert.Contains("剩余 1%", textOf (byId a.View "doctor-check-disk").Value)
        Assert.Contains("检查时间 10:00:00", textOf a.View))
