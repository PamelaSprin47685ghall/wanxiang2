module Wanxiang2.ModelSwitchDesk.Tests.Tests

// 真实组合：主题外观 + ModelSwitcher 筛选 + ModelPickerList 分组与 shouldPick + OverlayDesk 浮层。

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.ModelSwitchDesk
open Wanxiang2.DeskTests

let private options =
    [ { providerId = "oa"; providerLabel = "OpenAI"; model = "gpt-4o"; isCurrent = true; isDefault = false }
      { providerId = "oa"; providerLabel = "OpenAI"; model = "o3"; isCurrent = false; isDefault = false }
      { providerId = "ds"; providerLabel = "DeepSeek"; model = "deepseek-chat"; isCurrent = false; isDefault = true } ]

let private rowNames (root: Visual) =
    root.GetVisualDescendants()
    |> Seq.choose (function
        | :? Border as b when b.Focusable -> Some(AutomationProperties.GetName b + "，" + AutomationProperties.GetHelpText b)
        | _ -> None)
    |> List.ofSeq

let private setup () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let anchor = Button(Content = "模型")
    root.Children.Add anchor
    let overlay = OverlayDesk(root, Desk.look ())
    let window = Desk.show root 800.0 600.0
    overlay, ModelSwitchDesk(theme, overlay), anchor, root, window

let private searchOf (root: Visual) = root.GetVisualDescendants() |> Seq.pick (function :? TextBox as t -> Some t | _ -> None)

// 迁自 ModelChipUiTests：浮层内容经本组合者打开在 OverlayDesk 上。
[<Fact>]
let ``switcher shows provider, default and current, plus the next reply note`` () =
    Headless.run (fun () ->
        let overlay, desk, anchor, root, window = setup ()
        desk.Show(anchor, false, options, "切换本对话的模型", ignore)
        Dispatcher.UIThread.RunJobs()
        Assert.True overlay.Layer.IsPopupOpen
        let names = rowNames root
        Assert.Equal(3, names.Length)
        Assert.Contains("gpt-4o，OpenAI，当前", names)
        Assert.Contains("deepseek-chat，DeepSeek，默认", names)
        let texts = Desk.texts root
        Assert.Contains(ModelSwitcher.appliesNote, texts)
        Assert.Contains("默认", texts)
        window.Close())

[<Fact>]
let ``typing filters rows and enter picks the first hit`` () =
    Headless.run (fun () ->
        let overlay, desk, anchor, root, window = setup ()
        let mutable picked: ModelOption option = None
        desk.Show(anchor, false, options, "t", fun o -> picked <- Some o)
        Dispatcher.UIThread.RunJobs()
        let search = searchOf root
        search.Text <- "deep"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "deepseek-chat，DeepSeek，默认" ], rowNames root)
        Desk.key search Key.Enter
        Assert.Equal(Some "deepseek-chat", picked |> Option.map _.model)
        // 选中前已关浮层
        Assert.False overlay.Layer.IsPopupOpen
        window.Close())

[<Fact>]
let ``picking the current model is a no-op`` () =
    Headless.run (fun () ->
        let overlay, desk, anchor, root, window = setup ()
        let mutable picked = 0
        desk.Show(anchor, false, options, "t", fun _ -> picked <- picked + 1)
        Dispatcher.UIThread.RunJobs()
        let search = searchOf root
        search.Text <- "gpt"
        Dispatcher.UIThread.RunJobs()
        Desk.key search Key.Enter
        Assert.Equal(0, picked)
        // 当前项也关浮层
        Assert.False overlay.Layer.IsPopupOpen
        window.Close())

// 迁自 A11yRegressionTests：键盘路径写进帮助文字，空结果以 polite 播报。
[<Fact>]
let ``模型切换器写明键盘路径且空结果会播报`` () =
    Headless.run (fun () ->
        let _, desk, anchor, root, window = setup ()
        desk.Show(anchor, false, options, "t", ignore)
        Dispatcher.UIThread.RunJobs()
        let search = searchOf root
        Assert.Equal(ModelPickerList.searchHelp, AutomationProperties.GetHelpText search)
        search.Text <- "zzzz"
        Dispatcher.UIThread.RunJobs()
        let empty = root.GetVisualDescendants() |> Seq.pick (function :? TextBlock as t when t.Text = ModelPickerList.emptyText -> Some t | _ -> None)
        Assert.True empty.IsVisible
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting empty)
        window.Close())
