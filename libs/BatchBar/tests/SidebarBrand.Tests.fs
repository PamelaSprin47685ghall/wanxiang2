module Wanxiang2.SidebarBrand.Tests.Tests

open System
open Avalonia.Controls
open Xunit
open Wanxiang2.SidebarBrand
open Wanxiang2.SidebarBrand.Tests.Probe
open Wanxiang2.WorkspaceState

let private make () =
    let b = SidebarBrand()
    host b.View |> ignore
    b

[<Fact>]
let ``brand row fires new, preset with anchor and back`` () =
    Headless.run (fun () ->
        let b = make ()
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
let ``render SidebarBrandVS controls preset visibility`` () =
    Headless.run (fun () ->
        let b = make ()
        Assert.False b.PresetButton.IsVisible
        b.Render { PresetsVisible = true }
        Assert.True b.PresetButton.IsVisible
        b.Render { PresetsVisible = false }
        Assert.False b.PresetButton.IsVisible)
