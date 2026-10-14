module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "draftsummarytext.txt"))

/// 金样由旧代码（从 旧 AppShell.fs 的 UpdateDraftSummary，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``disabled persistence wins over counts`` () =
    Assert.Equal("已关闭：不会在本机保存草稿。", DraftSummaryText.text false 3 4)
    Assert.Equal("当前没有保存的草稿。", DraftSummaryText.text true 0 0)
    Assert.Equal("已保存 0 份草稿、2 条未确认的消息。", DraftSummaryText.text true 0 2)
