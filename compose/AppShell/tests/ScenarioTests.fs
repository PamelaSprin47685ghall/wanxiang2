module Wanxiang2.AppShell.Tests.ScenarioTests

// 使用场景四条。每条只钉既有旅程钉不住的一个事实：
// 配好自定义服务再开聊、探活失败不落盘、发出去或新开对话后旧草稿不再回来、
// 选附件在调用当下启动选择器。
// 配对、附件确认、流式、审批、搜索删除与 800 ms 草稿节流不在这里重测。

open System
open System.Net
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server
open Wanxiang.UI
open Wanxiang2.AppShell.Tests
open Wanxiang2.WorkspaceState

let private click (control: Control) =
    let button = control :?> Button
    let peer = ControlAutomationPeer.CreatePeerForElement button
    (peer :> obj :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private named (root: Visual) (name: string) =
    root.GetVisualDescendants()
    |> Seq.tryPick (function
        | :? Control as c when AutomationProperties.GetName c = name && c.IsEffectivelyVisible -> Some c
        | _ -> None)

/// 模型切换器里的行是 Border（读屏名=模型名）；设置页探活清单里的勾选框是 CheckBox，同名要避开。
let private modelRow (root: Visual) (name: string) =
    root.GetVisualDescendants()
    |> Seq.tryPick (function
        | :? Border as c when AutomationProperties.GetName c = name && c.IsVisible -> Some c
        | _ -> None)

let private connectEmpty (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "inst-1" |})

    rig.Fake.Deliver(
        CatalogSnapshot
            {|
                providers = JsonArray()
                tools = JsonArray()
                generation = JsonObject()
            |}
    )

    Pump.settle ()

let private catalogOf (id: string) (models: string list) =
    let provider = JsonObject()
    provider["id"] <- JsonValue.Create id
    provider["label"] <- JsonValue.Create "自定义"
    provider["models"] <- JsonArray([| for name in models -> JsonValue.Create name :> JsonNode |])
    provider["defaultModel"] <- JsonValue.Create(List.head models)
    provider["enabled"] <- JsonValue.Create true
    JsonArray([| provider :> JsonNode |])

let private upserts (rig: Rig) =
    rig.Fake.Events
    |> List.choose (function
        | ConfigUpsertProvider d -> Some d.provider
        | _ -> None)

let private show (rig: Rig) =
    let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
    window.Show()
    Pump.settle ()
    window

let private openAdd (rig: Rig) =
    rig.Shell.Dispatch(User(Settings(SettingsUserMsg.OpenSection(Some "providers"))))
    Pump.settle ()
    rig.Views.Settings.ApplyOpenAddProvider()
    Pump.settle ()
    rig.Views.Settings.Providers.Session.Value.Editor

/// 本机假模型服务。只认输入框里带来的密钥，返回两行模型。
let private serveModels () =
    let listener = new HttpListener()
    let port = new Sockets.TcpListener(IPAddress.Loopback, 0)
    port.Start()
    let number = (port.LocalEndpoint :?> IPEndPoint).Port
    port.Stop()
    listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" number)
    listener.Start()

    let seen = ResizeArray<string>()

    Task.Run(fun () ->
        try
            while listener.IsListening do
                let ctx = listener.GetContext()

                if ctx.Request.Url.AbsolutePath.EndsWith("/models") then
                    seen.Add(ctx.Request.Headers["Authorization"] |> Option.ofObj |> Option.defaultValue "")

                let body = """{"data":[{"id":"local-a"},{"id":"local-b"}]}"""
                let bytes = Encoding.UTF8.GetBytes body
                ctx.Response.StatusCode <- 200
                ctx.Response.ContentType <- "application/json"
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
                ctx.Response.OutputStream.Close()
        with _ ->
            ())
    |> ignore

    let probe url keyOpt kind done' =
        let provider =
            {
                id = "draft"
                kind = kind
                label = ""
                baseUrl = url
                apiKey = keyOpt
                models = []
                defaultModel = ""
                timeoutSeconds = 30
                maxRetries = 0
                enabled = true
                promptCaching = false
                headers = Map.empty
                extraJson = None
                visionOverrides = Map.empty
            }

        task {
            let! result = ProviderCatalog.probeModels provider CancellationToken.None

            let outcome =
                match result with
                | Result.Ok ids -> true, ids, None
                | Result.Error message -> false, [], Some message

            Dispatcher.UIThread.Post(fun () -> done' outcome)
        }
        |> ignore

    listener, sprintf "http://127.0.0.1:%d/v1" number, seen, probe

let private fillCustom (editor: Wanxiang2.ProvidersPane.ProviderEditor) =
    editor.ApplyAdvanced true
    editor.UrlBox.Text <- "https://example.test/v1"
    editor.KeyBox.Text <- "sk-test"
    editor.ModelsBox.Text <- ""
    editor.DefaultModelBox.Text <- ""

[<Fact>]
let ``场景：自定义服务先探活再保存，目录回来后能选模型并开聊`` () =
    Headless.run (fun () ->
        let probes = ResizeArray<string * string option * string>()
        let mutable reply: (bool * string list * string option -> unit) option = None

        let rig =
            Rig.createWithProbe UiPrefsFile.defaults (fun url key kind done' ->
                probes.Add(url, key, kind)
                reply <- Some done')

        let window = show rig

        try
            connectEmpty rig
            let editor = openAdd rig
            fillCustom editor
            click (named editor.View "添加").Value

            Assert.Equal(1, probes.Count)
            Assert.Equal(("https://example.test/v1", Some "sk-test", "openai"), probes[0])
            Assert.Empty(upserts rig)
            Assert.Equal("https://example.test/v1", editor.UrlBox.Text)

            reply.Value(true, [ "gemini-2.5-flash"; "gemini-2.5-pro" ], None)
            Pump.settle ()
            Assert.Empty(upserts rig)
            Assert.True(named editor.View "探测结果" |> Option.isSome, "探活回来后应列出探测结果")
            click (named editor.View "保存所选模型").Value

            let saved = upserts rig
            let first = saved[0]
            let modelsJson = first["models"].ToJsonString()
            Assert.Equal(1, saved.Length)
            Assert.Equal("gemini-2.5-flash", first["defaultModel"].GetValue<string>())
            Assert.Contains("gemini-2.5-flash", modelsJson)
            Assert.Contains("gemini-2.5-pro", modelsJson)

            rig.Fake.Deliver(
                CatalogSnapshot
                    {|
                        providers = catalogOf (first["id"].GetValue<string>()) [ "gemini-2.5-flash"; "gemini-2.5-pro" ]
                        tools = JsonArray()
                        generation = JsonObject()
                    |}
            )

            Pump.until (fun () -> rig.Views.Composer.Chip.Host.IsEnabled)

            Assert.True(rig.Views.Composer.Chip.Host.IsEnabled, "模型芯片应可点")
            // 无障碍 Invoke 会先禁用按钮再执行命令；这里只验证芯片自己的点击。
            rig.Views.Composer.Chip.Host.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent))
            Pump.settle ()
            Assert.True(rig.Views.Overlay.Layer.IsPopupOpen, "点模型芯片后切换器应打开")

            let row = modelRow rig.Root "gemini-2.5-pro"
            Assert.True(row.IsSome, "切换器里应有探到的第二个模型")
            let pointer = new Pointer(1, PointerType.Mouse, true)

            row.Value.RaiseEvent(
                PointerPressedEventArgs(
                    row.Value,
                    pointer,
                    window,
                    Point(8.0, 8.0),
                    0UL,
                    PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                    KeyModifiers.None
                )
            )

            Pump.settle ()
            let providerId = first["id"].GetValue<string>()

            Assert.Equal(Some(providerId, "gemini-2.5-pro"), rig.Shell.Model.Composer.SelectedModel)

            rig.Views.Composer.Box.Input.Text <- "你好"
            Pump.settle ()
            Assert.True(rig.Views.Composer.Box.SendButton.IsEnabled, "写好后发送键应可点")
            rig.Views.Composer.Box.SendButton.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent))
            Pump.settle ()

            Assert.Contains(
                rig.Fake.Commands,
                function
                | ClientCommand.SendUserMessage d -> d.messageJson["contents"].[0].["text"].GetValue<string>() = "你好"
                | _ -> false
            )

        finally
            window.Close())

[<Fact>]
let ``场景：输入框里的地址和密钥原样探活，模型清单回来后才保存`` () =
    Headless.run (fun () ->
        let listener, url, seen, probe = serveModels ()

        let rig = Rig.createWithProbe UiPrefsFile.defaults probe
        let window = show rig

        try
            connectEmpty rig
            let editor = openAdd rig
            editor.ApplyAdvanced true
            editor.UrlBox.Text <- url
            editor.KeyBox.Text <- "sk-from-box"
            editor.ModelsBox.Text <- ""
            editor.DefaultModelBox.Text <- ""
            click (named editor.View "添加").Value
            Pump.until (fun () -> named editor.View "保存所选模型" |> Option.isSome)
            Assert.Empty(upserts rig)
            click (named editor.View "保存所选模型").Value

            Pump.until (fun () -> (upserts rig).Length = 1)
            Assert.Equal<string list>([ "Bearer sk-from-box" ], List.ofSeq seen)

            let saved = (upserts rig)[0]
            Assert.Equal("local-a", saved["defaultModel"].GetValue<string>())
            Assert.Contains("local-b", saved["models"].ToJsonString())
        finally
            window.Close()
            listener.Stop()
            (listener :> IDisposable).Dispose())

[<Fact>]
let ``场景：探活失败或清单为空时不保存，地址和密钥留着`` () =
    Headless.run (fun () ->
        let mutable reply: (bool * string list * string option -> unit) option = None

        let rig =
            Rig.createWithProbe UiPrefsFile.defaults (fun _ _ _ done' -> reply <- Some done')

        let window = show rig

        try
            connectEmpty rig
            let editor = openAdd rig
            fillCustom editor
            click (named editor.View "添加").Value
            reply.Value(false, [], Some "密钥无效")
            Pump.settle ()

            Assert.Empty(upserts rig)
            Assert.Equal("https://example.test/v1", editor.UrlBox.Text)
            Assert.Equal("sk-test", editor.KeyBox.Text)
            Assert.True(editor.View.IsVisible)

            let failure =
                editor.View.GetVisualDescendants()
                |> Seq.tryPick (function
                    | :? TextBlock as t when t.IsVisible && t.Text = "密钥无效" -> Some t.Text
                    | _ -> None)

            Assert.Equal(Some "密钥无效", failure)

            reply.Value(true, [], None)
            Pump.settle ()
            Assert.Empty(upserts rig)

        finally
            window.Close())

[<Fact>]
let ``场景：发送或新开对话后，重开不再带回旧草稿`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Fake.SetLocalDraft(Some "上次没发出去")
        Pump.until (fun () -> (Workspace.project rig.Shell.Model).Composer.Text = "上次没发出去")

        connectEmpty rig

        rig.Fake.Deliver(
            CatalogSnapshot
                {|
                    providers = catalogOf "p1" [ "m1" ]
                    tools = JsonArray()
                    generation = JsonObject()
                |}
        )

        Pump.settle ()
        rig.Views.Composer.Box.Input.Text <- "这次发出去"
        Pump.settle ()
        click rig.Views.Composer.Box.SendButton
        Pump.settle ()
        Assert.Contains(DraftWrite.Delete, rig.Fake.DraftWrites)

        rig.Fake.SetLocalDraft(Some "另一段没发出去")
        rig.Views.Composer.Box.Input.Text <- "另一段没发出去"
        Pump.settle ()
        click (named rig.Root "新对话").Value
        Pump.settle ()
        Assert.Equal(2, rig.Fake.DraftWrites |> List.filter ((=) DraftWrite.Delete) |> List.length)

        let reopened = Rig.create ()
        Pump.settle ()
        let restored = (Workspace.project reopened.Shell.Model).Composer.Text
        Assert.Equal("", restored))

[<Fact>]
let ``场景：选附件在调用当下启动选择器`` () =
    let caller = Thread.CurrentThread.ManagedThreadId
    let calledOn = ref -1
    let gate = new TaskCompletionSource<bool>()

    FilePick.startOnCaller
        (fun () ->
            calledOn := Thread.CurrentThread.ManagedThreadId
            gate.SetResult(true)
            Task.FromResult true)
        ignore

    Assert.True(gate.Task.Wait(TimeSpan.FromSeconds 2.0), "选择器没有在调用当下启动")
    Assert.Equal(caller, calledOn.Value)
