module BreadcrumbActionRevealFlashTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``a new highlight restores the previous one first`` () =
    let f = RevealFlash()
    f.Revealed 1UL
    Assert.Equal(None, f.Begin 1UL)
    Assert.Equal(Some 1UL, f.Begin 2UL)
    Assert.Equal(Some 2UL, f.End())
    Assert.Equal(None, f.End())
    Assert.Equal(Some 1UL, f.LastFlashed)
