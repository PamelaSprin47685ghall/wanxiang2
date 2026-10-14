module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "breadcrumbaction.txt"))

/// 金样由旧代码（旧 Views/ChatView.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``unavailable parent shows text but does not navigate`` () =
    let b = BreadcrumbAction<int>()
    let v = b.Set(Some("来自已删除", "已删除", false, 3))
    Assert.True v.visible
    Assert.False v.clickable
    Assert.Equal(None, b.Activate())
    b.Set(Some("来自 A", "回到 A", true, 4)) |> ignore
    Assert.Equal(Some 4, b.Activate())
    b.Set None |> ignore
    Assert.Equal("", b.Text)
    Assert.Equal(None, b.Activate())
