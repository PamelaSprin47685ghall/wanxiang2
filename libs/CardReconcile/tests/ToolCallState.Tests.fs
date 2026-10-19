module ToolCallStateTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ToolCallState", "toolcallstate.txt"))

/// 金样由旧代码（旧 Views/MessageCard.fs resolveToolCallStatusWith，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ToolCallStateRender.render ())

// ---- 文档规则的真实行为

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
