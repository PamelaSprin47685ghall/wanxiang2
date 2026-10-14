namespace Wanxiang2.SettingsScreen

open System.Text.Json.Nodes
open Wanxiang.UI

/// 宿主给设置页的出口（旧 SettingsActions 去掉 toast：提示条归 OverlayDesk）。
/// 带 (bool -> unit) 的是有回执的写入：宿主拿到主机回复后回调 true / false。
type SettingsHooks =
    { UpsertProvider: JsonObject -> (bool -> unit) -> unit
      DeleteProvider: string -> unit
      ProbeProvider: string -> unit
      UpsertMcp: JsonObject -> (bool -> unit) -> unit
      DeleteMcp: string -> unit
      UpdateGeneration: JsonObject -> (bool -> unit) -> unit
      SavePrefs: UiPrefs -> unit }

module SettingsHooks =
    /// 全部空操作（测试与尚未接线时）
    let none: SettingsHooks =
        { UpsertProvider = fun _ _ -> ()
          DeleteProvider = ignore
          ProbeProvider = ignore
          UpsertMcp = fun _ _ -> ()
          DeleteMcp = ignore
          UpdateGeneration = fun _ _ -> ()
          SavePrefs = ignore }
