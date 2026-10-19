module Wanxiang2.SidebarBrand.Tests.Tests

open System
open Avalonia.Controls
open Xunit
open Wanxiang2.SidebarBrand
open Wanxiang2.SidebarBrand.Tests.Probe

let private make () =
    let look, cells = Fixture.make ()
    let b = SidebarBrand look
    host b.View |> ignore
    b, cells

[<Fact>]
let ``brand row fires new, preset with anchor and back`` () =
    Headless.run (fun () ->
        let b, _ = make ()
        Assert.Contains("万象", textOf b.View)
        Assert.False b.PresetButton.IsVisible
        Assert.False b.BackButton.IsVisible
        let log = ResizeArray<string>()
        b.NewClicked.Add(fun () -> log.Add "new")
        b.PresetClicked.Add(fun a -> log.Add(if obj.ReferenceEquals(a, b.PresetButton) then "preset" else "?"))
        b.BackClicked.Add(fun () -> log.Add "back")
        click b.NewButton
        b.SetPresetsVisible true
        click b.PresetButton
        b.SetCompact true
        click b.BackButton
        Assert.Equal<string list>([ "new"; "preset"; "back" ], List.ofSeq log))

[<Fact>]
let ``compact raises targets and theme cells update them`` () =
    Headless.run (fun () ->
        let b, cells = make ()
        cells["IconButtonSize"].Set 30.0
        cells["CompactTarget"].Set 44.0
        cells["TextButtonMinHeight"].Set 36.0
        Assert.Equal(30.0, b.PresetButton.Width)
        Assert.Equal(36.0, b.NewButton.MinHeight)
        b.SetCompact true
        Assert.Equal(44.0, b.PresetButton.Width)
        Assert.Equal(44.0, b.NewButton.MinHeight)
        cells["CompactTarget"].Set 48.0
        Assert.Equal(48.0, b.BackButton.Height))
