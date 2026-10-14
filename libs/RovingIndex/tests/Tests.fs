module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "rovingindex.txt"))

/// 金样由旧代码（旧 Controls/Menu.fs 与 Views/SettingsView.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``up and down wrap, home and end jump`` () =
    Assert.Equal(Some 4, RovingIndex.menu RovingKey.Up 0 5)
    Assert.Equal(Some 0, RovingIndex.menu RovingKey.Down 4 5)
    Assert.Equal(Some 0, RovingIndex.menu RovingKey.Down -1 5)
    Assert.Equal(Some 4, RovingIndex.menu RovingKey.PageDown 2 5)
    Assert.Equal(None, RovingIndex.menu RovingKey.Down 0 0)

[<Fact>]
let ``section navigation wraps both ways`` () =
    Assert.Equal(3, RovingIndex.wrap 0 -1 4)
    Assert.Equal(0, RovingIndex.wrap 3 1 4)
