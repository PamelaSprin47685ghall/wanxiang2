module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "toolspolicy.txt"))

/// 金样由旧代码（旧 Views/SettingsTools.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``unknown policy reads as automatic`` () =
    Assert.Equal("首次询问", ToolsPolicy.label "first")
    Assert.Equal("每次询问", ToolsPolicy.label "always")
    Assert.Equal("自动执行", ToolsPolicy.label "x")

[<Fact>]
let ``payload nests under tools with default first`` () =
    let j = (ToolsPolicy.payload [ "/a" ] "first" (Map.ofList [ "shell", "always" ]) true).ToJsonString()
    Assert.Equal("""{"tools":{"fileReadRoots":["/a"],"policy":{"default":"first","shell":"always"},"exposeDebug":true}}""", j)
