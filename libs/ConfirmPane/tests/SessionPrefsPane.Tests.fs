module Wanxiang2.SessionPrefsPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Threading
open Xunit
open Wanxiang2.SessionPrefsPane
open Wanxiang2.SessionPrefsPane.Tests.Probe

let private init =
    {
        Instructions = "简洁"
        Temperature = "0.7"
        TopP = ""
        MaxTokens = ""
        ThinkingBudget = ""
        Tools =
            [
                {
                    Id = "time"
                    Label = "当前时间"
                    Source = "内置"
                    Enabled = true
                }
                {
                    Id = "srv_x"
                    Label = "X"
                    Source = "srv"
                    Enabled = false
                }
            ]
        Customized = true
    }

let private make () =
    let p = SessionPrefsPane(Button(Content = "OpenAI · gpt-4o"), init, fun _ -> None)
    let w = host p.View
    p, w

[<Fact>]
let ``sessionSettings dialog validates numeric bounds and updates config`` () =
    Headless.run (fun () ->
        let p, w = make ()
        let drafts = ResizeArray<SessionPrefsDraft>()
        p.SaveRequested.Add drafts.Add
        Assert.Contains("生成参数 · 已自定义", textOf p.View)
        Assert.Contains("工具 · 已启用 1 项", textOf p.View)
        p.TopPBox.Text <- "0.9"
        p.InstructionsBox.Focus() |> ignore
        w.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.None, null)
        Dispatcher.UIThread.RunJobs()
        let d = drafts[0]
        Assert.Equal("0.7", d.Temperature)
        Assert.Equal("0.9", d.TopP)
        Assert.Equal<string list>([ "time" ], d.Tools)
        // 摘要：多错时可见，一改即收起
        p.ShowSummary(Some "有 2 处需要修正")
        p.RevealParameters()
        Assert.True p.Summary.IsVisible
        Assert.True p.Parameters.IsExpanded
        p.TemperatureBox.Text <- "1"
        Dispatcher.UIThread.RunJobs()
        Assert.False p.Summary.IsVisible
        p.ApplyPending true
        Assert.Equal("正在保存…", AutomationProperties.GetName p.SaveButton)
        click p.SaveButton
        Assert.Single drafts |> ignore)

[<Fact>]
let ``tool toggles change the saved set and the header counts on expand`` () =
    Headless.run (fun () ->
        let p, _ = make ()
        let drafts = ResizeArray<SessionPrefsDraft>()
        p.SaveRequested.Add drafts.Add
        p.ToolsExpander.IsExpanded <- true
        Dispatcher.UIThread.RunJobs()
        let toggle = (named p.View "启用工具 X").Value
        toggle.Focus() |> ignore
        let w = TopLevel.GetTopLevel toggle :?> Window
        w.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null)
        Dispatcher.UIThread.RunJobs()
        p.ToolsExpander.IsExpanded <- false
        p.ToolsExpander.IsExpanded <- true
        Assert.Contains("工具 · 已启用 2 项", textOf p.View)
        click p.SaveButton
        Assert.Equal<string list>([ "time"; "srv_x" ], drafts[0].Tools))
