module SearchSessionTests
open Xunit
open Wanxiang.UI

let texts (s: SearchSession<string>) box =
    match s.Section box with
    | Some(_, lines) -> lines |> List.map _.text
    | None -> []

// 迁自旧 LibraryTests（原用例起窗口；这里留请求、等待、结果、截断、迟到丢弃的状态部分）。
[<Fact>]
let ``sidebar shows hit snippets under message content and clicking opens the hit`` () =
    let s = SearchSession<string>()
    Assert.Equal(Some "Quetzal", s.Begin(" Quetzal ", true))
    Assert.Equal<string list>([ "正在搜索消息…" ], texts s "Quetzal")
    Assert.True(s.Apply("Quetzal", [ "hit" ], false, true, 0))
    Assert.Equal<string list>([ "hit" ], s.Hits)
    Assert.Equal<string list>([ "结果已截断：只显示了最近的一部分匹配，请换更具体的词。" ], texts s "Quetzal")
    // 迟到的旧词结果被丢弃
    Assert.False(s.Apply("old", [], false, false, 0))
    Assert.Equal(1, s.Hits.Length)

[<Fact>]
let ``显示更多追加，在途时不重复请求；失败可重试`` () =
    let s = SearchSession<string>()
    s.Begin("q", true) |> ignore
    Assert.Equal(None, s.LoadMore())
    s.Apply("q", [ "a"; "b" ], true, false, 0) |> ignore
    let lines = (s.Section "q").Value |> snd
    Assert.Equal(Some SearchStatusAction.LoadMore, lines.Head.action)
    Assert.Equal(Some("q", 2), s.LoadMore())
    Assert.Equal<string list>([ "正在加载更多…" ], texts s "q")
    Assert.True(s.Fail("q", "超时"))
    Assert.Equal<string list>([ "消息搜索失败：超时" ], texts s "q")
    Assert.Equal(Some("q", 2), s.Retry())
    s.Apply("q", [ "c" ], false, false, 2) |> ignore
    Assert.Equal<string list>([ "a"; "b"; "c" ], s.Hits)
    Assert.Equal(Some([ "a"; "b"; "c" ], []), s.Section "q")

[<Fact>]
let ``不能发时不显示「搜索中」；词一变旧命中作废；断线不挂着`` () =
    let s = SearchSession<string>()
    Assert.Equal(None, s.Begin("q", false))
    Assert.Equal(None, s.Section "q")
    s.Begin("q", true) |> ignore
    Assert.Equal(None, s.Section "qq")
    s.Disconnected()
    Assert.False s.IsPending
    Assert.Equal(None, s.Section "q")
    Assert.Equal(None, s.Begin("", true))
    Assert.Empty s.Hits
    Assert.Equal("", s.Query)

[<Fact>]
let ``SearchSession: Query 是上次已发出的词`` () =
    let s = SearchSession<string>()
    s.Begin("天气", true) |> ignore
    Assert.Equal("天气", s.Query)
    Assert.Equal("重试", SearchStatusAction.label SearchStatusAction.Retry)
    Assert.Equal("显示更多", SearchStatusAction.label SearchStatusAction.LoadMore)
