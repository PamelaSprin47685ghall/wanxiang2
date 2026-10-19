module BreathPhaseTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``opacity stays inside 0.55 to 0.95 and restarts from zero phase`` () =
    let b = BreathPhase()
    Assert.True(b.Start(false, true))
    for _ in 1 .. 200 do Assert.InRange(b.Step(), 0.55, 0.95)
    Assert.True(b.Start(false, true))
    Assert.Equal(0.0, b.Phase)

[<Fact>]
let ``reduced motion or a detached view does not breathe`` () =
    let b = BreathPhase()
    Assert.False(b.Start(true, true))
    Assert.False(b.Start(false, false))
