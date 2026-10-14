module Samples

open Wanxiang.UI

let phases =
    [ ExportPhase.Idle; ExportPhase.Reading(0, None); ExportPhase.Reading(3, Some 10); ExportPhase.Reading(5, Some 0)
      ExportPhase.Ready 0; ExportPhase.Ready 12; ExportPhase.Failed "读取失败：断开"; ExportPhase.Cancelled ]
let saves =
    [ ExportView.idleSave
      { ExportView.idleSave with saving = true }
      { ExportView.idleSave with saved = true; savedKind = "JSON" }
      { ExportView.idleSave with error = Some "保存失败：磁盘满。完整内容仍保留，可以再次保存。" } ]
let elapsed = [ 0.0; 40.0; 120.0; 500.0 ]
