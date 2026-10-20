module ExportViewTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``reading shows progress and the close key cancels`` () =
    let v = ExportView.decide (ExportPhase.Reading(3, Some 12)) ExportView.idleSave ExportOutputKind.None false
    match v.progress with
    | ExportProgress.Determinate p -> Assert.Equal(25.0, p)
    | other -> Assert.Fail(sprintf "Expected Determinate, got %A" other)
    Assert.Equal("取消导出", v.closeText)
    Assert.Equal(ExportProgress.Indeterminate, (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave ExportOutputKind.None false).progress)
    Assert.Equal(ExportProgress.Determinate 0.0, (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave ExportOutputKind.None true).progress)

[<Fact>]
let ``ready with output can save both formats; empty or saving cannot`` () =
    let v = ExportView.decide (ExportPhase.Ready 4) ExportView.idleSave ExportOutputKind.Both false
    Assert.Equal(ExportAction.Save(SaveGate.Enabled, true), v.action)
    let empty = ExportView.decide (ExportPhase.Ready 0) ExportView.idleSave ExportOutputKind.Both false
    Assert.Equal(ExportAction.Save(SaveGate.Disabled, true), empty.action)
    let saving = ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saving = true } ExportOutputKind.Both false
    Assert.Equal(ExportAction.Save(SaveGate.Disabled, true), saving.action)
    Assert.Equal(ExportTone.Success, (ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saved = true; savedKind = "JSON" } ExportOutputKind.Both false).tone)

[<Fact>]
let ``only reading is throttled`` () =
    Assert.Equal(ExportRefresh.Now, ExportView.refresh false (ExportPhase.Ready 1) 0.0 120.0 false)
    Assert.Equal(ExportRefresh.Schedule 80.0, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 false)
    Assert.Equal(ExportRefresh.Skip, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 true)
    Assert.Equal(ExportRefresh.Now, ExportView.refresh true (ExportPhase.Reading(1, None)) 40.0 120.0 true)
