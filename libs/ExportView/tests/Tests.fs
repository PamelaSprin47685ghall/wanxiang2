module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "exportview.txt"))

/// 金样由旧代码（旧 Views/ExportDialog.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``reading shows progress and the close key cancels`` () =
    let v = ExportView.decide (ExportPhase.Reading(3, Some 12)) ExportView.idleSave false false false
    Assert.True v.progressVisible
    Assert.Equal(25.0, v.progressValue)
    Assert.Equal("取消导出", v.closeText)
    Assert.True (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave false false false).indeterminate
    Assert.False (ExportView.decide (ExportPhase.Reading(3, None)) ExportView.idleSave false false true).indeterminate

[<Fact>]
let ``ready with output can save both formats; empty or saving cannot`` () =
    let v = ExportView.decide (ExportPhase.Ready 4) ExportView.idleSave true true false
    Assert.True(v.saveEnabled && v.saveJsonEnabled)
    Assert.False (ExportView.decide (ExportPhase.Ready 0) ExportView.idleSave true true false).saveEnabled
    Assert.False (ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saving = true } true true false).saveEnabled
    Assert.Equal(ExportTone.Success, (ExportView.decide (ExportPhase.Ready 4) { ExportView.idleSave with saved = true; savedKind = "JSON" } true true false).tone)

[<Fact>]
let ``only reading is throttled`` () =
    Assert.Equal(ExportRefresh.Now, ExportView.refresh false (ExportPhase.Ready 1) 0.0 120.0 false)
    Assert.Equal(ExportRefresh.Schedule 80.0, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 false)
    Assert.Equal(ExportRefresh.Skip, ExportView.refresh false (ExportPhase.Reading(1, None)) 40.0 120.0 true)
    Assert.Equal(ExportRefresh.Now, ExportView.refresh true (ExportPhase.Reading(1, None)) 40.0 120.0 true)
