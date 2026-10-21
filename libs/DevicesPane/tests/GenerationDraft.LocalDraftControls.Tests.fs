module GenerationDraftLocalDraftControlsTests
open Xunit
open Wanxiang.UI

[<Fact>]
let ``summary change fires once per distinct value`` () =
    let c = LocalDraftControls()
    let mutable fired = 0
    c.Changed.Add(fun () -> fired <- fired + 1)
    c.Summary <- "2 条草稿"
    c.Summary <- "2 条草稿"
    c.Summary <- ""
    Assert.Equal(2, fired)
    let mutable cleared = false
    c.ClearRequested.Add(fun () -> cleared <- true)
    c.RequestClear()
    Assert.True cleared
