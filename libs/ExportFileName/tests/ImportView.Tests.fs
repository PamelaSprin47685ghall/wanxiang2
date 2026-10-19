module ImportViewTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ImportView", "importview.txt"))

/// 金样由旧代码（旧 Views/ImportDialog.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ImportViewRender.render ())

// ---- 文档规则的真实行为

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
    Assert.False (ImportView.chrome false false).importVisible
    Assert.False (ImportView.chrome true true).importEnabled
    Assert.Equal("换一个文件…", (ImportView.chrome true false).pickText)
