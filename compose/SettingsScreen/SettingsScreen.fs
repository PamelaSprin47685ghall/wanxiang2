namespace Wanxiang2.SettingsScreen

open System
open Avalonia.Controls
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SettingsFrame
open Wanxiang2.WorkspaceState

/// 设置界面组合者（旧 Views/SettingsView.fs）：分区导航与缓存、各分区滚动位置（SectionMemory）、范围说明、
/// 命令类操作登记：
/// - WireImport / WirePresets: 注入外部回调
/// - ApplySectionScrollOffset: 滚动恢复一次性命令
/// - ApplyProbe: 探活回执
/// - ApplyOpenAddProvider: 直达添加服务表单
/// 切进设备分区时读取最新列表；各分区的协作在 ProvidersDesk / ToolsDesk / GeneralDesk / DevicesDesk。
/// 外框、导航与响应式归 SettingsFrame。
type SettingsScreen(theme: ThemeManager, overlay: OverlayDesk, initialHooks: SettingsHooks, initialPrefs: UiPrefs) =
    let mutable hooks = initialHooks
    let prefsChanged = Event<UiPrefs>()
    let closeRequested = Event<unit>()
    let frame =
        SettingsFrame(
            [ for title, parts in SettingsPart.groups ->
                  title, [ for p in parts -> { Key = SettingsPart.key p; Label = SettingsPart.label p; Glyph = SettingsPart.glyph p } ] ],
            SettingsLooks.glyphOf theme)
    // 主题资源字典不在这里挂：同一份字典已有 parent（入口壳挂进 Application.Resources），
    // 一个 ResourceDictionary 只能有一个 owner；DynamicResource 沿可视树查到 Application 即得值。
    let providers = ProvidersDesk(theme, overlay, (fun () -> hooks))
    let tools = ToolsDesk(theme, overlay, (fun () -> hooks))
    let general =
        GeneralDesk(theme, overlay, (fun () -> hooks), initialPrefs, (fun p ->
            hooks.SavePrefs p
            prefsChanged.Trigger p))
    let devices = DevicesDesk(theme, overlay)
    let memory = SectionMemory<SettingsPart, Control>(SettingsPart.Providers)
    let mutable serverUrl = ""
    let mutable lastCatalog = Catalog.empty

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
        frame.ApplyCaptions(
            SettingsPart.scope part,
            match part with
            | SettingsPart.Appearance -> "仅在这台客户端即时生效，不修改主机配置。"
            | SettingsPart.About -> "应用版本与当前连接信息。"
            | _ ->
                match Uri.TryCreate(serverUrl, UriKind.Absolute) with
                | true, uri -> sprintf "%s · 配置由连接这台主机的客户端共用。" uri.Authority
                | _ -> "尚未连接主机")
        frame.Select(SettingsPart.key part)
        frame.ApplyContent(render part, restoreY)
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

    /// 单入口幂等渲染：唯一输入是 WorkspaceState 的 SettingsVS（W7-B 收口，没有旁路参数）。
    member this.Render(vs: SettingsVS) =
        let urlChanged = serverUrl <> vs.ServerUrl
        serverUrl <- vs.ServerUrl
        match vs.Catalog with
        | Some cat ->
            lastCatalog <- cat
            providers.Render { Catalog = cat }
            tools.Render { Catalog = cat }
            general.Render { Catalog = cat; InstanceId = vs.InstanceId; ServerUrl = vs.ServerUrl; Prefs = vs.Prefs }
            memory.Invalidate [ SettingsPart.About ]
            if memory.Current = SettingsPart.About then frame.ApplyContent(render memory.Current, None)
        | None ->
            general.Render { Catalog = lastCatalog; InstanceId = vs.InstanceId; ServerUrl = vs.ServerUrl; Prefs = vs.Prefs }
        devices.Render vs.Devices
        if urlChanged then
            memory.Invalidate [ SettingsPart.About ]
            select memory.Current
        match vs.ActiveSection |> Option.bind SettingsPart.ofKey with
        | Some section when memory.Current <> section -> select section
        | _ -> ()

    /// 换一套出口（宿主先构造设置页、再接线时用）
    member _.Bind(next: SettingsHooks) = hooks <- next

    /// 直达某个分区（错误卡「打开模型服务设置」等，一次性命令）
    member _.ShowSection(part: SettingsPart) = select part

    /// 打开设置时的焦点归宿：当前分区的导航键
    member _.FocusInitial() = frame.FocusNav(SettingsPart.key memory.Current)

    member _.GetSectionScrollOffset(part: SettingsPart) = memory.Offset part

    member _.ApplySectionScrollOffset(part: SettingsPart, y: float) =
        memory.ApplyOffset(part, y)
        if memory.Current = part then frame.ScrollTo y

    member _.HasUnsavedChanges = general.HasUnsavedGeneration

    member _.ApplyProbe(providerId: string, ok: bool, models: string list, error: string option) =
        providers.ApplyProbe(providerId, ok, models, error)

    /// 直接打开「添加模型服务」编辑器（一次性命令）
    member _.ApplyOpenAddProvider() =
        select SettingsPart.Providers
        providers.ApplyOpenAddEditor()

    member this.OpenAddProvider() = this.ApplyOpenAddProvider()

    /// 主机状态（配置有误、检查主机）与本机草稿控件
    member _.Health = general.Health
    member _.LocalDrafts = general.LocalDrafts
    member _.WireImport(handler: unit -> unit) = general.WireImportHandler handler
    member _.WirePresets(rename: PresetInfo -> unit, delete: PresetInfo -> unit) = general.WirePresetHandlers(rename, delete)
    member _.PresetsPanel = general.PresetsPanel

    /// 本机偏好变了（已调用 SavePrefs 之后）
    member _.PrefsChanged = prefsChanged.Publish
    /// 「返回对话」
    member _.CloseRequested = closeRequested.Publish
