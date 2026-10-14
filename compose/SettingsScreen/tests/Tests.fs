module Wanxiang2.SettingsScreen.Tests.Tests

// 真实组合：主题管理者给外观、OverlayDesk 出对话框与提示条、各分区面板经 SettingsScreen 协作。

open System
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.UI
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SettingsScreen
open Wanxiang2.DeskTests

let private all (root: Visual) = root.GetVisualDescendants() |> Seq.choose (function :? Control as c -> Some c | _ -> None) |> List.ofSeq
let private byId (root: Visual) id = all root |> List.tryFind (fun c -> AutomationProperties.GetAutomationId c = id)
let private byName (root: Visual) name = all root |> List.tryFind (fun c -> AutomationProperties.GetName c = name)
let private textOf (root: Visual) =
    Dispatcher.UIThread.RunJobs()
    match TopLevel.GetTopLevel root with
    | null -> ()
    | top -> top.UpdateLayout()
    String.Join("\n", Desk.texts root)

let private invoke (c: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement c
    (Assert.IsAssignableFrom<IInvokeProvider> peer).Invoke()
    Dispatcher.UIThread.RunJobs()

let private prefs: UiPrefs =
    { theme = FollowSystem; fontScale = 14.0; autoCollapseReasoning = true; codeWrap = false; sidebarWidth = 280.0
      sidebarCollapsed = false; showArchived = false; reduceMotion = false; enterSends = true; persistDrafts = true
      windowWidth = 1200.0; windowHeight = 800.0; windowX = 0; windowY = 0; hasWindowPosition = false }

type private Harness =
    { theme: ThemeManager
      overlay: OverlayDesk
      screen: SettingsScreen
      root: Grid
      window: Window }

let private setup (hooks: SettingsHooks) =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let screen = SettingsScreen(theme, overlay, hooks, prefs)
    root.Children.Insert(0, screen.View)
    let window = Desk.show root 1100.0 800.0
    { theme = theme; overlay = overlay; screen = screen; root = root; window = window }

let private device id name : DeviceInfo =
    { id = id; name = name; createdAt = Some(DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)); lastSeen = None
      revoked = false; online = false; current = false; desktop = false }

let private provider: ProviderInfo =
    { id = "test-prov"; label = "Test Provider"; kind = "openai"; baseUrl = "https://api.test.com/v1"; hasApiKey = true
      models = [ "old-model" ]; defaultModel = "old-model"; timeoutSeconds = 120; maxRetries = 2; enabled = true
      visionOverrides = Map.empty; visionDetected = Map.empty }

// 迁自 DevicesUiTests 的同名用例
[<Fact>]
let ``settings has a devices section in the host group`` () =
    Assert.Contains(SettingsPart.Devices, SettingsPart.all)
    let group = SettingsPart.groups |> List.find (fun (_, parts) -> List.contains SettingsPart.Devices parts) |> snd
    Assert.Contains(SettingsPart.Providers, group)
    Assert.Equal("设备", SettingsPart.label SettingsPart.Devices)
    Assert.Equal(SettingsPart.scope SettingsPart.Providers, SettingsPart.scope SettingsPart.Devices)

[<Fact>]
let ``selecting the devices section requests the list once`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        let requests = ResizeArray<unit>()
        h.screen.Devices.ListRequested.Add requests.Add
        Dispatcher.UIThread.RunJobs()
        Assert.Empty requests
        let nav = all h.screen.View |> List.find (fun c -> AutomationProperties.GetHelpText c = "设备")
        invoke nav
        Assert.Equal(1, requests.Count)
        Assert.Equal(SettingsPart.Devices, h.screen.Current)
        h.window.Close())

[<Fact>]
let ``device list read without a reply times out with the older host hint`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        h.screen.ShowSection SettingsPart.Devices
        Assert.True h.screen.Devices.IsLoading
        h.screen.Devices.NotifyLoadTimeout()
        Assert.False h.screen.Devices.IsLoading
        Assert.Contains("主机版本较旧", textOf h.screen.View)
        h.window.Close())

[<Fact>]
let ``device list marks current desktop online and revoked and sorts revoked last`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        h.screen.ShowSection SettingsPart.Devices
        let now = DateTimeOffset.UtcNow
        h.screen.Devices.SetDevices(
            [ { device "dev-revoked" "旧手机" with revoked = true }
              { device "dev-phone" "手机" with online = true }
              { device "dev-desktop" "wanxiang-desktop" with desktop = true; current = true; online = true }
              { device "dev-pad" "平板" with lastSeen = Some(now - TimeSpan.FromMinutes 5.0) } ],
            true)
        Dispatcher.UIThread.RunJobs()
        let rows =
            all h.screen.View
            |> List.choose (fun c -> let id = AutomationProperties.GetAutomationId c in if not (isNull id) && id.StartsWith "devices-row-" then Some id else None)
        Assert.Equal<string list>([ "devices-row-dev-desktop"; "devices-row-dev-phone"; "devices-row-dev-pad"; "devices-row-dev-revoked" ], rows)
        let text = textOf h.screen.View
        for s in [ "这台设备"; "桌面"; "在线"; "已吊销"; "5 分钟前在线"; "本机桌面客户端" ] do Assert.Contains(s, text)
        Assert.True((byId h.screen.View "devices-revoke-dev-desktop").IsNone)
        Assert.True((byId h.screen.View "devices-revoke-dev-phone").IsSome)
        Assert.True((byId h.screen.View "devices-rename-dev-desktop").IsNone)
        let revoked = ResizeArray<DeviceInfo>()
        h.screen.Devices.RevokeRequested.Add revoked.Add
        invoke (byId h.screen.View "devices-revoke-dev-phone").Value
        Assert.Equal("dev-phone", revoked[0].id)
        h.window.Close())

[<Fact>]
let ``rename edits inline trims and sends the new name`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        h.screen.ShowSection SettingsPart.Devices
        let renames = ResizeArray<string * string>()
        h.screen.Devices.RenameRequested.Add renames.Add
        h.screen.Devices.SetDevices([ device "dev-phone" "手机" ], true)
        invoke (byId h.screen.View "devices-rename-dev-phone").Value
        let box = (byId h.screen.View "devices-name-dev-phone").Value :?> TextBox
        Assert.Equal("手机", box.Text)
        box.Text <- "   "
        invoke (byId h.screen.View "devices-name-save-dev-phone").Value
        Assert.Empty renames
        Assert.Contains("设备名称不能为空。", textOf h.root)
        box.Text <- "  书房平板 "
        invoke (byId h.screen.View "devices-name-save-dev-phone").Value
        Assert.Equal<(string * string) list>([ "dev-phone", "书房平板" ], List.ofSeq renames)
        h.window.Close())

[<Fact>]
let ``a disconnect while loading explains the host may be too old`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        let requests = ResizeArray<unit>()
        h.screen.Devices.ListRequested.Add requests.Add
        h.screen.ShowSection SettingsPart.Devices
        Assert.Equal(1, requests.Count)
        h.screen.Devices.NotifyDisconnected()
        Assert.False h.screen.Devices.IsLoading
        Assert.Contains("版本较旧", textOf h.screen.View)
        h.window.Close())

[<Fact>]
let ``invite card shows grouped code address qr and countdown`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        h.screen.ShowSection SettingsPart.Devices
        let d = h.screen.Devices
        d.SetLanSource(fun () -> [])
        d.SetConnection "ws://192.168.1.5:8765/ws"
        d.SetDevices([], true)
        let now = DateTimeOffset.UtcNow
        d.SetInvite("012345", now + TimeSpan.FromMinutes 5.0, now)
        Assert.True((textOf h.screen.View).Contains "5:00 后失效")
        d.Tick(now + TimeSpan.FromMinutes 5.0 + TimeSpan.FromSeconds 1.0)
        Assert.Contains("已过期", textOf h.screen.View)
        invoke (byId h.screen.View "devices-close-invite").Value
        Assert.True((byId h.screen.View "devices-add").IsSome)
        h.window.Close())

// 迁自 SettingsViewTests 的同名用例
[<Fact>]
let ``SettingsView navigates sections, remembers scroll positions and invalidates cache`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        Assert.Equal(SettingsPart.Providers, h.screen.Current)
        h.screen.SetSectionScrollOffset(SettingsPart.Tools, 40.0)
        Assert.Equal(40.0, h.screen.GetSectionScrollOffset SettingsPart.Tools)
        h.screen.ShowSection SettingsPart.About
        Assert.Equal(SettingsPart.About, h.screen.Current)
        h.screen.SetConnection("inst-1", "ws://host.example:8765/ws")
        Assert.Contains("inst-1", textOf h.screen.View)
        h.screen.ShowSection SettingsPart.Generation
        Assert.Contains("host.example:8765 · 配置由连接这台主机的客户端共用。", textOf h.screen.View)
        h.window.Close())

[<Fact>]
let ``SettingsProviders saveCore with invalid advanced field automatically expands advanced layer and focuses error`` () =
    Headless.run (fun () ->
        let upserts = ResizeArray<JsonObject>()
        let h = setup { SettingsHooks.none with UpsertProvider = fun p cb -> upserts.Add p; cb true }
        h.screen.SetCatalog { Catalog.empty with providers = [ provider ] }
        h.screen.Providers.OpenEditor "test-prov"
        Dispatcher.UIThread.RunJobs()
        let editor = h.screen.Providers.Session.Value.Editor
        Assert.False editor.IsAdvanced
        editor.TimeoutBox.Text <- "abc"
        invoke (byName h.root "保存").Value
        Assert.True editor.IsAdvanced
        Assert.Empty upserts
        editor.TimeoutBox.Text <- "60"
        invoke (byName h.root "保存").Value
        Assert.Single upserts |> ignore
        Assert.True h.screen.Providers.Session.IsNone
        h.window.Close())

[<Fact>]
let ``SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`` () =
    Headless.run (fun () ->
        let upserts = ResizeArray<JsonObject>()
        let probes = ResizeArray<string>()
        let h = setup { SettingsHooks.none with UpsertProvider = (fun p cb -> upserts.Add p; cb true); ProbeProvider = probes.Add }
        h.screen.SetCatalog { Catalog.empty with providers = [ provider ] }
        h.screen.Providers.OpenEditor "test-prov"
        Dispatcher.UIThread.RunJobs()
        let editor = h.screen.Providers.Session.Value.Editor
        editor.SetAdvanced true
        invoke (byId h.root "provider-probe-models").Value
        Assert.Equal<string list>([ "test-prov" ], List.ofSeq probes)
        h.screen.ApplyProbe("test-prov", true, [ "probed-model-1"; "probed-model-2" ], None)
        Assert.Contains("probed-model-1", editor.ModelsBox.Text)
        Assert.Empty upserts
        h.overlay.CloseDialog()
        h.screen.ApplyProbe("test-prov", true, [ "bg-model-a" ], None)
        Assert.Empty upserts
        // 缓存：再打开时模型框用探到的清单
        h.screen.Providers.OpenEditor "test-prov"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("bg-model-a", h.screen.Providers.Session.Value.Editor.ModelsBox.Text)
        h.window.Close())

[<Fact>]
let ``SettingsGeneral SaveGenerationCore validates numeric ranges and generates payload`` () =
    Headless.run (fun () ->
        let payloads = ResizeArray<JsonObject>()
        let mutable reply = true
        let h = setup { SettingsHooks.none with UpdateGeneration = fun p cb -> payloads.Add p; cb reply }
        h.screen.SetCatalog Catalog.empty
        h.screen.ShowSection SettingsPart.Generation
        let g = h.screen.General.Generation
        g.TemperatureBox.Text <- "5"
        h.screen.General.SaveGeneration()
        Assert.Empty payloads
        Assert.True h.screen.HasUnsavedChanges
        g.TemperatureBox.Text <- "0.7"
        reply <- false
        h.screen.General.SaveGeneration()
        Assert.Single payloads |> ignore
        Assert.True((textOf h.root).Contains "保存失败，请重试。", textOf h.root)
        h.window.Close())

[<Fact>]
let ``SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`` () =
    Headless.run (fun () ->
        let mcp = ResizeArray<JsonObject>()
        let gens = ResizeArray<JsonObject>()
        let h = setup { SettingsHooks.none with UpsertMcp = (fun p cb -> mcp.Add p; cb true); UpdateGeneration = (fun p cb -> gens.Add p; cb true) }
        h.screen.SetCatalog Catalog.empty
        h.screen.ShowSection SettingsPart.Tools
        invoke (byName h.screen.View "添加 MCP 服务").Value
        let ed = h.screen.Tools.McpEditor.Value
        invoke (byName h.root "添加").Value
        Assert.Empty mcp
        ed.IdBox.Text <- "fs"
        ed.LabelBox.Text <- "文件"
        ed.CommandBox.Text <- "npx"
        invoke (byName h.root "添加").Value
        Assert.Single mcp |> ignore
        // 授权文件夹：空、相对、重复都不写
        invoke (byId h.screen.View "tools-add-folder").Value
        let fe = h.screen.Tools.FolderEditor.Value
        fe.PathBox.Text <- "relative/path"
        invoke (byName h.root "授权").Value
        Assert.Empty gens
        Assert.Contains("需要绝对路径", textOf h.root)
        fe.PathBox.Text <- "/home/me/docs"
        invoke (byName h.root "授权").Value
        Assert.True(gens.Count = 1, textOf h.root)
        h.window.Close())

// 新增：样式值归主题管理者且可变——改一项度量，已显示的设置控件跟着变
[<Fact>]
let ``changing a theme metric updates shown settings controls`` () =
    Headless.run (fun () ->
        let h = setup SettingsHooks.none
        h.screen.ShowSection SettingsPart.Generation
        let box = h.screen.General.Generation.InstructionsBox
        let before = (h.theme.Metric Metric.TextAreaMinHeight).Value
        Assert.Equal(before, box.MinHeight)
        h.theme.SetMetric(Metric.TextAreaMinHeight, before + 37.0)
        Assert.Equal(before + 37.0, box.MinHeight)
        h.window.Close())

// 迁自 OnboardingUiTests：密钥为空时不发起任何请求并就地说明；探测失败撤销半成品
[<Fact>]
let ``密钥为空时不发起任何请求并就地说明`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let root = Grid()
        let overlay = OverlayDesk(root, Desk.look ())
        let upserts = ResizeArray<JsonObject>()
        let discarded = ResizeArray<string>()
        let probes = ResizeArray<string>()
        let card =
            OnboardingDesk(
                theme, overlay,
                { UpsertProvider = (fun p cb -> upserts.Add p; cb true)
                  DiscardProvider = discarded.Add
                  ProbeProvider = probes.Add
                  TakenIds = (fun () -> [])
                  DiscoverLocal = (fun () -> Threading.Tasks.Task.FromResult None)
                  OpenAdvanced = ignore })
        root.Children.Insert(0, card.View)
        let window = Desk.show root 900.0 700.0
        invoke (byName card.View "连接").Value
        Assert.Empty upserts
        Assert.False card.IsBusy
        card.Pane.KeyBox.Text <- "sk-test"
        invoke (byName card.View "连接").Value
        Assert.Single upserts |> ignore
        Assert.Single probes |> ignore
        Assert.True(card.ApplyProbe(probes[0], false, [], Some "[auth] 密钥无效"))
        Assert.Equal<string list>([ probes[0] ], List.ofSeq discarded)
        Assert.False(card.ApplyProbe("other", true, [], None))
        window.Close())
