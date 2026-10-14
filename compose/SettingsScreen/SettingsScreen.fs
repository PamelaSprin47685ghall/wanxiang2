namespace Wanxiang2.SettingsScreen

open System
open Avalonia.Controls
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SettingsFrame

/// 设置页分区（旧 SettingsSection；改名避开旧宿主模块）
[<RequireQualifiedAccess>]
type SettingsPart =
    | Providers
    | Tools
    | Generation
    | Devices
    | Appearance
    | About

module SettingsPart =
    let groups =
        [ "主机", [ SettingsPart.Providers; SettingsPart.Tools; SettingsPart.Generation; SettingsPart.Devices ]
          "这台客户端", [ SettingsPart.Appearance; SettingsPart.About ] ]

    let all = groups |> List.collect snd

    let scope =
        function
        | SettingsPart.Providers | SettingsPart.Tools | SettingsPart.Generation | SettingsPart.Devices -> "主机"
        | SettingsPart.Appearance | SettingsPart.About -> "这台客户端"

    let label =
        function
        | SettingsPart.Providers -> "模型服务"
        | SettingsPart.Tools -> "工具"
        | SettingsPart.Generation -> "对话默认值"
        | SettingsPart.Devices -> "设备"
        | SettingsPart.Appearance -> "外观与操作"
        | SettingsPart.About -> "关于"

    let glyph =
        function
        | SettingsPart.Providers -> "server"
        | SettingsPart.Tools -> "wrench"
        | SettingsPart.Generation -> "sliders"
        | SettingsPart.Devices -> "devices"
        | SettingsPart.Appearance -> "sun"
        | SettingsPart.About -> "info"

    let key (part: SettingsPart) = (sprintf "%A" part).ToLowerInvariant()
    let ofKey (k: string) = all |> List.tryFind (fun p -> key p = k)

/// 设置界面组合者（旧 Views/SettingsView.fs）：分区导航与缓存、各分区滚动位置（SectionMemory）、范围说明、
/// 切进设备分区时读取最新列表；各分区的协作在 ProvidersDesk / ToolsDesk / GeneralDesk / DevicesDesk。
/// 外框、导航与响应式归 SettingsFrame。
type SettingsScreen(theme: ThemeManager, overlay: OverlayDesk, initialHooks: SettingsHooks, initialPrefs: UiPrefs) =
    let mutable hooks = initialHooks
    let prefsChanged = Event<UiPrefs>()
    let closeRequested = Event<unit>()
    let frame =
        SettingsFrame(
            SettingsLooks.frameOf theme,
            [ for title, parts in SettingsPart.groups ->
                  title, [ for p in parts -> { Key = SettingsPart.key p; Label = SettingsPart.label p; Glyph = SettingsPart.glyph p } ] ])
    let providers = ProvidersDesk(theme, overlay, (fun () -> hooks))
    let tools = ToolsDesk(theme, overlay, (fun () -> hooks))
    let general =
        GeneralDesk(theme, overlay, (fun () -> hooks), initialPrefs, (fun p ->
            hooks.SavePrefs p
            prefsChanged.Trigger p))
    let devices = DevicesDesk(theme, overlay)
    let memory = SectionMemory<SettingsPart, Control>(SettingsPart.Providers)
    let mutable serverUrl = ""

    let build =
        function
        | SettingsPart.Providers -> providers.View
        | SettingsPart.Tools -> tools.View
        | SettingsPart.Generation -> general.GenerationView
        | SettingsPart.Devices -> devices.View
        | SettingsPart.Appearance -> general.AppearanceView
        | SettingsPart.About -> general.AboutView

    // 每个分区只构建一次并缓存：面板复用同一批输入控件实例，重复挂载会抛「already has a visual parent」
    let render part =
        match memory.TryBuilt part with
        | Some built -> built
        | None ->
            let built = build part
            memory.Keep(part, built)
            built

    let select (part: SettingsPart) =
        let restoreY = memory.Select(part, Some frame.ContentOffset)
        let changed = restoreY.IsSome
        frame.SetCaptions(
            SettingsPart.scope part,
            match part with
            | SettingsPart.Appearance -> "仅在这台客户端即时生效，不修改主机配置。"
            | SettingsPart.About -> "应用版本与当前连接信息。"
            | _ ->
                match Uri.TryCreate(serverUrl, UriKind.Absolute) with
                | true, uri -> sprintf "%s · 配置由连接这台主机的客户端共用。" uri.Authority
                | _ -> "尚未连接主机")
        frame.Select(SettingsPart.key part)
        frame.SetContent(render part, restoreY)
        // 设备列表是运行时状态：每次切进来都向主机要最新的
        if part = SettingsPart.Devices && changed then devices.Refresh()

    do
        frame.SelectRequested.Add(fun k -> SettingsPart.ofKey k |> Option.iter select)
        frame.CloseClicked.Add(fun () -> closeRequested.Trigger())
        select memory.Current

    member _.View = frame :> Control
    member _.Frame = frame
    member _.Providers = providers
    member _.Tools = tools
    member _.General = general
    /// 设备分区：宿主订阅它的读取 / 邀请 / 改名 / 吊销事件并转发设备相关的主机事件
    member _.Devices = devices
    member _.Current = memory.Current

    /// 换一套出口（宿主先构造设置页、再接线时用）
    member _.Bind(next: SettingsHooks) = hooks <- next

    /// 直达某个分区（错误卡「打开模型服务设置」等）
    member _.ShowSection(part: SettingsPart) = select part

    /// 打开设置时的焦点归宿：当前分区的导航键
    member _.FocusInitial() = frame.FocusNav(SettingsPart.key memory.Current)

    member _.GetSectionScrollOffset(part: SettingsPart) = memory.Offset part

    member _.SetSectionScrollOffset(part: SettingsPart, y: float) =
        memory.SetOffset(part, y)
        if memory.Current = part then frame.ScrollTo y

    member _.SetCatalog(catalog: Catalog) =
        providers.SetCatalog catalog
        tools.SetCatalog catalog
        general.SetCatalog catalog
        // 只有「关于」依赖构建时的快照，交给切换时重建
        memory.Invalidate [ SettingsPart.About ]
        if memory.Current = SettingsPart.About then frame.SetContent(render memory.Current, None)

    member _.SetPrefs(prefs: UiPrefs) = general.SetPrefs prefs

    member this.SetConnection(instance: string, url: string) =
        general.SetConnection(instance, url)
        serverUrl <- url
        devices.SetConnection url
        memory.Invalidate [ SettingsPart.About ]
        select memory.Current

    member _.HasUnsavedChanges = general.HasUnsavedGeneration

    member _.ApplyProbe(providerId: string, ok: bool, models: string list, error: string option) =
        providers.ApplyProbe(providerId, ok, models, error)

    /// 直接打开「添加模型服务」编辑器
    member _.OpenAddProvider() =
        select SettingsPart.Providers
        providers.OpenAddEditor()

    /// 主机状态（配置有误、检查主机）与本机草稿控件
    member _.Health = general.Health
    member _.LocalDrafts = general.LocalDrafts
    member _.ConfigureImport(handler: unit -> unit) = general.SetImportHandler handler
    member _.ConfigurePresets(rename: PresetInfo -> unit, delete: PresetInfo -> unit) = general.SetPresetHandlers(rename, delete)
    member _.PresetsPanel = general.PresetsPanel

    /// 本机偏好变了（已调用 SavePrefs 之后）
    member _.PrefsChanged = prefsChanged.Publish
    /// 「返回对话」
    member _.CloseRequested = closeRequested.Publish
