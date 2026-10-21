module ToolOverviewTests

open System
open System.IO
open Xunit
open Wanxiang.UI


let c r a s f u =
    {
        running = r
        awaiting = a
        success = s
        failed = f
        unknown = u
    }

[<Fact>]
let ``single call names its state; several list counts in fixed order`` () =
    Assert.Equal("调用 1 个工具 · 等待批准", ToolOverview.text (c 0 1 0 0 0))
    Assert.Equal("调用 4 个工具 · 2 个完成 · 1 个执行中 · 1 个失败", ToolOverview.text (c 1 0 2 1 0))

[<Fact>]
let ``awaiting outranks running; never a check while anything is pending`` () =
    Assert.Equal(OverviewTone.Warning, ToolOverview.tone (c 1 1 0 0 0))
    Assert.Equal(OverviewIcon.Spinner, ToolOverview.icon (c 1 0 3 0 0))
    Assert.Equal(OverviewIcon.Check, ToolOverview.icon (c 0 0 3 0 0))
