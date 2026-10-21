module Wanxiang2.DialogsDesk.Tests.Tests

// 真实组合：主题管理者给外观、OverlayDesk 出对话框，各面板经 DialogsDesk 协作。测试名沿用旧 DialogsTests。

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input.TextInput
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.ConnectPane
open Wanxiang2.DialogsDesk
open Wanxiang2.DeskTests

let private all (root: Visual) =
    root.GetVisualDescendants()
    |> Seq.choose (function
        | :? Control as c -> Some c
        | _ -> None)
    |> List.ofSeq

let private visibleButton (root: Visual) name =
    all root
    |> List.tryFind (fun c -> AutomationProperties.GetName c = name && c.IsEffectivelyVisible && c.Focusable)

let private invoke (c: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement c
    (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    Dispatcher.UIThread.RunJobs()

let private boxes (root: Visual) =
    all root
    |> List.choose (function
        | :? TextBox as b -> Some b
        | _ -> None)

type private Rig =
    {
        overlay: OverlayDesk
        desk: DialogsDesk
        root: Grid
        window: Window
    }

let private setup () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let window = Desk.show root 1000.0 800.0

    {
        overlay = overlay
        desk = DialogsDesk(theme, overlay)
        root = root
        window = window
    }

let private isOpen r = r.overlay.Layer.IsDialogOpen

[<Fact>]
let ``prompt dialog validates input and handles confirm and cancel`` () =
    Headless.run (fun () ->
        let r = setup ()
        let mutable confirmed = None

        r.desk.Prompt(
            "重命名",
            "名称",
            "旧名",
            "确定",
            fun v ok ->
                confirmed <- Some v
                ok true
        )
        |> ignore

        Dispatcher.UIThread.RunJobs()
        Assert.True(isOpen r)
        let box = boxes r.root |> List.find (fun b -> b.Text = "旧名")
        box.Text <- "   "
        Dispatcher.UIThread.RunJobs()
        let pane = r.overlay.Layer.IsDialogOpen
        Assert.True pane
        Assert.False((all r.root |> List.find (fun c -> AutomationProperties.GetName c = "确定")).IsEnabled, "空白时确认键禁用")
        Assert.True(confirmed.IsNone, "空白不得提交")
        Assert.True(r.desk.FieldCheck.HasError box, "空白应有行内错误")
        box.Text <- "  新名 "
        Dispatcher.UIThread.RunJobs()
        invoke (visibleButton r.root "确定").Value
        Assert.Equal(Some "新名", confirmed)
        Assert.False(isOpen r)
        r.desk.Prompt("重命名", "名称", "x", "确定", fun _ _ -> ()) |> ignore
        Dispatcher.UIThread.RunJobs()
        invoke (visibleButton r.root "取消").Value
        Assert.False(isOpen r))

[<Fact>]
let ``confirm and confirmWithFocus dialog trigger callbacks correctly`` () =
    Headless.run (fun () ->
        let r = setup ()
        let mutable confirmed = false
        let pane = r.desk.Confirm("删除对话", "无法撤销。", "删除", fun () -> confirmed <- true)
        Dispatcher.UIThread.RunJobs()
        Assert.True(isOpen r)
        Assert.Same(pane.CancelButton, pane.InitialFocus)
        invoke pane.CancelButton
        Assert.False confirmed
        Assert.False(isOpen r)
        let mutable confirmedWithFocus = false

        let pane2 =
            r.desk.ConfirmWithFocus("重置", "恢复默认。", "确认重置", (fun () -> confirmedWithFocus <- true), true)

        Dispatcher.UIThread.RunJobs()
        Assert.Same(pane2.ConfirmButton, pane2.InitialFocus)
        invoke (visibleButton r.root "确认重置").Value
        Assert.True confirmedWithFocus
        Assert.False(isOpen r))

[<Fact>]
let ``editText dialog loads text and submits on confirm`` () =
    Headless.run (fun () ->
        let r = setup ()
        let mutable result = None
        r.desk.EditText("编辑消息", "原文", "确定", fun v -> result <- Some v) |> ignore
        Dispatcher.UIThread.RunJobs()
        let tb = boxes r.root |> List.find (fun b -> b.Text = "原文")
        tb.Text <- "改后"
        invoke (visibleButton r.root "确定").Value
        Assert.Equal(Some "改后", result)
        Assert.False(isOpen r))

[<Fact>]
let ``connect dialog validates pairing code and token connections`` () =
    Headless.run (fun () ->
        let r = setup ()
        let mutable req: ConnectAsk option = None
        let mutable code: string option = None

        let handle =
            r.desk.Connect("ws://127.0.0.1:8765/ws", (fun q -> req <- Some q), (fun c -> code <- Some c))

        Dispatcher.UIThread.RunJobs()
        Assert.True(isOpen r)
        invoke (visibleButton r.root "连接").Value
        Assert.Equal("ws://127.0.0.1:8765/ws", req.Value.Url)
        Assert.False req.Value.UsePairing
        Assert.True(isOpen r, "发起连接后等待结果期间对话框保持打开")
        // 类型化失败：超时给「重试」，点它原样再连
        req <- None
        handle.Fail ConnectionIssue.TimedOut
        Dispatcher.UIThread.RunJobs()
        Assert.True(handle.Pane.RetryButton.IsVisible)
        invoke handle.Pane.RetryButton
        Assert.True(req.IsSome && not req.Value.UsePairing)
        handle.Info "正在连接…"
        Assert.False(handle.Pane.RetryButton.IsVisible)
        // 配对模式
        invoke (visibleButton r.root "没有令牌？改用配对码").Value
        Assert.True req.Value.UsePairing
        Assert.False(handle.Pane.ConnectButton.IsVisible)
        Assert.True(handle.Pane.CodeButton.IsVisible)
        handle.Pane.CodeBox.Text <- "123"
        Dispatcher.UIThread.RunJobs()
        invoke handle.Pane.CodeButton
        Assert.True(code.IsNone, "配对码位数不足时不得触发提交")
        Assert.Equal(PairingCodeInput.incomplete, handle.Pane.StatusText)
        handle.Pane.CodeBox.Text <- "123456"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(Some "123456", code)
        // 码错：不给重试键
        handle.Fail(ConnectionIssue.PairingRejected "invalid pairing code")
        Dispatcher.UIThread.RunJobs()
        Assert.False(handle.Pane.RetryButton.IsVisible))

[<Fact>]
let ``pairing code box normalizes pasted text and auto-submits at six digits`` () =
    Headless.run (fun () ->
        let r = setup ()
        let submitted = ResizeArray<string>()
        let handle = r.desk.Connect("ws://127.0.0.1:8765/ws", ignore, submitted.Add)
        Dispatcher.UIThread.RunJobs()
        handle.StartPairing()
        let codeBox = handle.Pane.CodeBox
        Assert.Equal(TextInputContentType.Digits, TextInputOptions.GetContentType codeBox)
        codeBox.Text <- "12 3-a4"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("1234", codeBox.Text)
        Assert.Empty submitted
        codeBox.Text <- "123 456"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("123456", codeBox.Text)
        Assert.Equal<string list>([ "123456" ], List.ofSeq submitted))

[<Fact>]
let ``sessionSettings dialog validates numeric bounds and updates config`` () =
    Headless.run (fun () ->
        let r = setup ()
        let mutable saved: SessionConfig option = None

        let catalog =
            { Catalog.empty with
                providers =
                    [
                        {
                            id = "openai"
                            label = "OpenAI"
                            kind = "openai"
                            baseUrl = "https://api.openai.com"
                            hasApiKey = true
                            models = [ "gpt-4o"; "gpt-4o-mini" ]
                            defaultModel = "gpt-4o"
                            timeoutSeconds = 60
                            maxRetries = 2
                            enabled = true
                            visionOverrides = Map.empty
                            visionDetected = Map.empty
                        }
                    ]
                tools =
                    [
                        {
                            id = "builtin:fs"
                            label = "文件系统"
                            description = "读写本地文件"
                            source = "builtin"
                            serverId = None
                        }
                    ]
            }

        let current =
            { SessionConfig.empty with
                provider = "openai"
                model = "gpt-4o"
                temperature = Some 0.7
                topP = Some 0.9
                maxTokens = Some 2048
                thinkingBudget = Some 1000
                tools = [ "builtin:fs" ]
                instructions = Some "遵循用户指示"
            }

        let pane =
            r.desk.SessionSettings(
                catalog,
                current,
                fun cfg ok ->
                    saved <- Some cfg
                    ok true
            )

        Dispatcher.UIThread.RunJobs()
        Assert.True(isOpen r)
        Assert.Equal("0.7", pane.TemperatureBox.Text)
        let save () = invoke pane.SaveButton
        pane.TemperatureBox.Text <- "3.5"
        save ()
        Assert.True(saved.IsNone, "温度超出合法范围不得保存")
        Assert.True(pane.Parameters.IsExpanded)
        pane.TemperatureBox.Text <- "0.8"
        pane.TopPBox.Text <- "1.5"
        save ()
        Assert.True(saved.IsNone)
        pane.TopPBox.Text <- "0.95"
        pane.MaxTokensBox.Text <- "-50"
        save ()
        Assert.True(saved.IsNone)
        pane.MaxTokensBox.Text <- "4096"
        pane.ThinkingBudgetBox.Text <- "-1"
        save ()
        Assert.True(saved.IsNone)
        pane.ThinkingBudgetBox.Text <- "2000"
        save ()
        Assert.True(saved.IsSome, "所有表单项均合法时应当成功保存")
        Assert.Equal(Some 0.8, saved.Value.temperature)
        Assert.Equal(Some 0.95, saved.Value.topP)
        Assert.Equal(Some 4096, saved.Value.maxTokens)
        Assert.Equal(Some 2000, saved.Value.thinkingBudget)
        Assert.Equal<string list>([ "builtin:fs" ], saved.Value.tools)
        Assert.Equal(Some "遵循用户指示", saved.Value.instructions)
        Assert.False(isOpen r))

[<Fact>]
let ``shortcuts dialog displays sections and closes on button click`` () =
    Headless.run (fun () ->
        let r = setup ()
        let pane = r.desk.Shortcuts()
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("新建对话", Desk.texts r.root)
        invoke pane.CloseButton
        Assert.False(isOpen r))

// 迁自旧 ConversationWorkspaceTests（375×520 一组）：展开生成参数后「保存」仍在视口内、在滚动区之外。
[<Fact>]
let ``session parameters reveal validation errors and leave save outside the scroll area`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let root = Grid()
        let overlay = OverlayDesk(root, Desk.look ())
        let window = Desk.show root 375.0 520.0
        let desk = DialogsDesk(theme, overlay)

        let catalog =
            { Catalog.empty with
                providers =
                    [
                        {
                            id = "openai"
                            label = "OpenAI"
                            kind = "openai"
                            baseUrl = "https://api.openai.com"
                            hasApiKey = true
                            models = [ "gpt-4o" ]
                            defaultModel = "gpt-4o"
                            timeoutSeconds = 60
                            maxRetries = 2
                            enabled = true
                            visionOverrides = Map.empty
                            visionDetected = Map.empty
                        }
                    ]
                tools =
                    [
                        for i in 1..6 ->
                            {
                                id = sprintf "builtin:t%d" i
                                label = "工具"
                                description = "说明"
                                source = "builtin"
                                serverId = None
                            }
                    ]
            }

        let current =
            { SessionConfig.empty with
                provider = "openai"
                model = "gpt-4o"
            }

        let pane = desk.SessionSettings(catalog, current, fun _ ok -> ok true)
        pane.TemperatureBox.Text <- "9"
        Dispatcher.UIThread.RunJobs()
        invoke pane.SaveButton
        window.UpdateLayout()
        Dispatcher.UIThread.RunJobs()
        window.UpdateLayout()
        Assert.True(pane.TemperatureBox.IsEffectivelyVisible, "校验失败应展开生成参数")
        let save = pane.SaveButton
        let bottom = save.TranslatePoint(Point(0.0, save.Bounds.Height), root)
        Assert.True(bottom.HasValue && bottom.Value.Y <= 520.0, sprintf "保存键被挤出视口：%A" bottom)
        Assert.False(save.GetVisualAncestors() |> Seq.exists (fun a -> a :? ScrollViewer), "保存键不得在滚动区内"))
