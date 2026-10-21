module GenerationDraftToolsPolicyTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``unknown policy reads as automatic`` () =
    Assert.Equal("首次询问", ToolsPolicy.label "first")
    Assert.Equal("每次询问", ToolsPolicy.label "always")
    Assert.Equal("自动执行", ToolsPolicy.label "x")

[<Fact>]
let ``payload nests under tools with default first`` () =
    let j = (ToolsPolicy.payload [ "/a" ] "first" (Map.ofList [ "shell", "always" ]) true).ToJsonString()
    Assert.Equal("""{"tools":{"fileReadRoots":["/a"],"policy":{"default":"first","shell":"always"},"exposeDebug":true}}""", j)
