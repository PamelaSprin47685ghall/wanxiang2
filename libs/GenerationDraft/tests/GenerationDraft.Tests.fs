module GenerationDraftTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "GenerationDraft", "generationdraft.txt"))

/// 金样由旧代码（从 旧 Views/SettingsGeneral.fs 的草稿字段与分支，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), GenerationDraftRender.render ())

// ---- 文档规则的真实行为

let private fresh () =
    let d = GenerationDraft<int>(0)
    d.SetScope "A"
    d.CatalogArrived(1, ignore)
    d

[<Fact>]
let ``catalog refills a clean form and edits during the refill do not count`` () =
    let d = fresh ()
    let mutable filled = 0
    d.CatalogArrived(2, fun () -> filled <- filled + 1; d.Edited())
    Assert.Equal(1, filled)
    Assert.False d.Dirty
    Assert.Equal(2, d.Baseline)

[<Fact>]
let ``catalog never overwrites an unsaved draft`` () =
    let d = fresh ()
    d.Edited()
    let mutable filled = false
    d.CatalogArrived(9, fun () -> filled <- true)
    Assert.False filled
    Assert.Equal(1, d.Baseline)
    Assert.StartsWith("主机默认值已变化", d.Status)

[<Fact>]
let ``a draft cannot be saved to another host but can after coming back`` () =
    let d = fresh ()
    d.Edited()
    d.SetScope "B"
    Assert.False d.CanSave
    d.SetScope "A"
    Assert.True d.CanSave

[<Fact>]
let ``an earlier confirmation does not mark later edits as saved`` () =
    let d = fresh ()
    d.Edited()
    let save = d.BeginSave 5
    d.Edited()
    Assert.False(d.Finish(save, true))
    Assert.True d.Dirty
    Assert.Equal(5, d.Baseline)

[<Fact>]
let ``switching hosts voids the pending confirmation`` () =
    let d = fresh ()
    d.Edited()
    let save = d.BeginSave 5
    d.SetScope "B"
    Assert.False d.Saving
    Assert.False(d.Finish(save, true))
    Assert.Equal(1, d.Baseline)
    Assert.True d.Dirty

[<Fact>]
let ``a failed save keeps the draft and asks for a toast`` () =
    let d = fresh ()
    d.Edited()
    Assert.True(d.Finish(d.BeginSave 5, false))
    Assert.True d.Dirty
    Assert.True d.CanSave

[<Fact>]
let ``discard is refused while saving and otherwise refills from the given value`` () =
    let d = fresh ()
    d.Edited()
    let save = d.BeginSave 5
    Assert.False(d.Discard(7, ignore))
    d.Finish(save, false) |> ignore
    let mutable filled = false
    Assert.True(d.Discard(7, fun () -> filled <- true))
    Assert.True filled
    Assert.False d.Dirty
    Assert.Equal(7, d.Baseline)
