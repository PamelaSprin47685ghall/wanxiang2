namespace Wanxiang2.SettingsScreen

open System
open Wanxiang.UI
open Wanxiang2.WorkspaceState

// 归属（W7-B）：设置页的总输入是 Wanxiang2.WorkspaceState.SettingsVS（含设备分区
// SettingsDevicesVS），这里只留各分区自己的输入投影——它们是从总投影拆出来的派生形状，
// 不在 WorkspaceState 里重复定义；SettingsPart 是设置页自己的导航概念，同样只在这里。

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

/// 对话默认值、外观与操作、关于分区的视图状态投影
type SettingsGeneralVS =
    { Catalog: Catalog
      InstanceId: string
      ServerUrl: string
      Prefs: UiPrefs }

module SettingsGeneralVS =
    let empty (prefs: UiPrefs) : SettingsGeneralVS =
        { Catalog = Catalog.empty
          InstanceId = ""
          ServerUrl = ""
          Prefs = prefs }

/// 模型服务分区的视图状态投影
type SettingsProvidersVS =
    { Catalog: Catalog }

module SettingsProvidersVS =
    let empty: SettingsProvidersVS =
        { Catalog = Catalog.empty }

/// 工具分区的视图状态投影
type SettingsToolsVS =
    { Catalog: Catalog }

module SettingsToolsVS =
    let empty: SettingsToolsVS =
        { Catalog = Catalog.empty }

/// 首次运行引导卡片的视图状态投影
type SettingsOnboardingVS =
    { SelectedPresetKey: string option
      DiscoveredModels: string list option
      SwitchOffered: bool }

module SettingsOnboardingVS =
    let empty: SettingsOnboardingVS =
        { SelectedPresetKey = None
          DiscoveredModels = None
          SwitchOffered = false }
