module ToolCallStateTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``a result decides regardless of streaming`` () =
    for s in [ false; true ] do
        Assert.Equal(ToolCallState.Success, ToolCallState.resolve (Set.ofList [ "c" ]) s "c" (Some None))
        Assert.Equal(ToolCallState.Failed(Some "e"), ToolCallState.resolve Set.empty s "c" (Some(Some "e")))

[<Fact>]
let ``without a result streaming decides running or awaiting, else unknown`` () =
    Assert.Equal(ToolCallState.AwaitingApproval, ToolCallState.resolve (Set.ofList [ "c" ]) true "c" None)
    Assert.Equal(ToolCallState.Running, ToolCallState.resolve (Set.ofList [ "d" ]) true "c" None)
    Assert.Equal(ToolCallState.Unknown, ToolCallState.resolve (Set.ofList [ "c" ]) false "c" None)
