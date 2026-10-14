module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "blockdiff.txt"))

/// 金样由旧代码（旧 StreamingMarkdown.Update 的记账，照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``only blocks after the first changed one are rendered again`` () =
    let d = BlockDiff<string, string>()
    Assert.True(d.Accept "a")
    for b in (d.Diff [ "p1"; "p2" ]).Render do d.Mounted(b, [ b + "-ctl" ])
    Assert.True(d.Accept "b")
    let plan = d.Diff [ "p1"; "p2x"; "p3" ]
    Assert.Equal(1, plan.KeepControls)
    Assert.Equal<string list>([ "p1-ctl" ], plan.Restore)
    Assert.Equal<string list>([ "p2x"; "p3" ], plan.Render)
    Assert.Equal(1, d.BlockCount)

[<Fact>]
let ``the same text twice does nothing`` () =
    let d = BlockDiff<string, string>()
    Assert.True(d.Accept "x")
    Assert.False(d.Accept "x")
