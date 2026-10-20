module ExportViewTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``reading shows progress and the close key cancels`` () =
    let v = ExportView.decide (ExportPhase.Reading(3, Some 12)) ExportView.idleSave ExportOutputKind.None false
    Assert.True v.progressVisible
    Assert.Equal(25.0, v.progressValue)
    Assert.Equal("取消导出", v.closeText)
    Assert.True (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave ExportOutputKind.None false).indeterminate
    Assert.False (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave ExportOutputKind.None true).indeterminate

[<Fact>]
let ``ready with output can save both formats; empty or saving cannot`` () =
    let v = ExportView.decide (ExportPhase.Ready 4) ExportView.idleSave ExportOutputKind.Both false
    Assert.True(v.saveEnabled && v.saveJsonEnabled)
    Assert.False (ExportView.decide (ExportPhase.Ready 0) ExportView.idleSave ExportOutputKind.Both false).saveEnabled
    Assert.False (ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saving = true } ExportOutputKind.Both false).saveEnabled
    Assert.Equal(ExportTone.Success, (ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saved = true; savedKind = "JSON" } ExportOutputKind.Both false).tone)

[<Fact>]
let ``only reading is throttled`` () =
    Assert.Equal(ExportRefresh.Now, ExportView.refresh false (ExportPhase.Ready 1) 0.0 120.0 false)
    Assert.Equal(ExportRefresh.Schedule 80.0, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 false)
    Assert.Equal(ExportRefresh.Skip, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 true)
    Assert.Equal(ExportRefresh.Now, ExportView.refresh true (ExportPhase.Reading(1, None)) 40.0 120.0 true)
