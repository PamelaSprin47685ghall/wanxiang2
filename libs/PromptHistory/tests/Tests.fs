module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "prompthistory.txt"))

/// 金样由旧代码（从 旧 Views/Composer.fs 的历史召回分支与 Submit，逐行照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``recall then come back restores the half typed draft`` () =
    let h = PromptHistory()
    h.Record "a"
    h.Record "b"
    Assert.Equal(Some "b", h.Up("draft", 0, false))
    Assert.Equal("draft", h.Draft "b")
    Assert.Equal(Some "a", h.Up("b", 0, false))
    Assert.Equal(Some "b", h.Down("a", 1, false))
    Assert.Equal(Some "draft", h.Down("b", 1, false))
    Assert.False h.IsRecalling

[<Fact>]
let ``duplicates and blanks are not recorded and capacity drops the oldest`` () =
    let h = PromptHistory(2)
    for t in [ "a"; "a"; " "; "b"; "c" ] do h.Record t
    Assert.Equal(Some "c", h.Up("", 0, false))
    Assert.Equal(Some "b", h.Up("", 0, false))
    Assert.Equal(Some "b", h.Up("", 0, false))
