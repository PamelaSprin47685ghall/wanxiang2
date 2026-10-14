module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "modelpickerlist.txt"))

/// 金样由旧代码（旧 Views/ModelSwitcherView.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``headers only without a query and with several providers`` () =
    Assert.True(ModelPickerList.groups fst "" Samples.two |> List.forall (fst >> Option.isSome))
    Assert.True(ModelPickerList.groups fst "q" Samples.two |> List.forall (fst >> Option.isNone))
    Assert.True(ModelPickerList.groups fst "" Samples.one |> List.forall (fst >> Option.isNone))

[<Fact>]
let ``up from the top returns to search; down stops at the end`` () =
    Assert.Equal(Some PickerMove.ToSearch, ModelPickerList.move true 0 3)
    Assert.Equal(Some(PickerMove.Focus 2), ModelPickerList.move false 2 3)
    Assert.Equal(Some(PickerMove.Focus 0), ModelPickerList.move false -1 3)
    Assert.Equal(None, ModelPickerList.move false 0 0)
