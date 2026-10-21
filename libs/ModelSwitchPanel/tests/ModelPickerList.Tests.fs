module ModelPickerListTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``headers only without a query and with several providers`` () =
    Assert.True(ModelPickerList.groups fst "" ModelPickerListSamples.two |> List.forall (fst >> Option.isSome))
    Assert.True(ModelPickerList.groups fst "q" ModelPickerListSamples.two |> List.forall (fst >> Option.isNone))
    Assert.True(ModelPickerList.groups fst "" ModelPickerListSamples.one |> List.forall (fst >> Option.isNone))

[<Fact>]
let ``up from the top returns to search; down stops at the end`` () =
    Assert.Equal(Some PickerMove.ToSearch, ModelPickerList.move true 0 3)
    Assert.Equal(Some(PickerMove.Focus 2), ModelPickerList.move false 2 3)
    Assert.Equal(Some(PickerMove.Focus 0), ModelPickerList.move false -1 3)
    Assert.Equal(None, ModelPickerList.move false 0 0)
