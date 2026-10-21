module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let private moduleType name =
    Spacing.ramp |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.pick (fun a ->
        match a.GetType("Wanxiang.UI." + name) with
        | null -> None
        | t -> Some t)

[<Fact>]
let ``ramp is strictly increasing and starts at zero`` () =
    Assert.Equal(0.0, List.head Spacing.ramp)
    Assert.True(Spacing.ramp |> List.pairwise |> List.forall (fun (a, b) -> a < b))

[<Fact>]
let ``off-ramp values are rejected`` () =
    Assert.False(Spacing.isRamp 14.0)
    Assert.False(Spacing.isRamp -4.0)
    Assert.True(Spacing.isRamp 6.0)

[<Fact>]
let ``geometry covers ramp slots radii and strokes`` () =
    for v in Spacing.ramp @ Spacing.Slot.all @ Spacing.Radius.all @ Spacing.Stroke.all do
        Assert.Contains(v, Spacing.geometry)

    Assert.DoesNotContain(Spacing.Tracking.label, Spacing.geometry)
