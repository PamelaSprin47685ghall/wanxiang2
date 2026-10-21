module ExportFileNameImportViewTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``only the latest attempt's reply counts`` () =
    let a = ImportAttempt()
    let first = (a.Begin()).Value
    Assert.Equal(None, a.Begin())
    Assert.False(a.Finish(first + 1))
    Assert.True(a.Finish first)
    Assert.False(a.Finish first)

[<Fact>]
let ``import key needs a preview and an idle dialog`` () =
    Assert.Equal(ImportGate.Hidden, (ImportView.chrome false false).gate)
    Assert.Equal(ImportGate.Disabled, (ImportView.chrome true true).gate)
    Assert.Equal("换一个文件…", (ImportView.chrome true false).pickText)
