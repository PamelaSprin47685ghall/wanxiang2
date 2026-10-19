module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


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


let private filled (items: string list) =
    let h = PromptHistory()
    for t in items do h.Record t
    h

[<Fact>]
let ``single line recalls only from an empty box or with the caret at the start`` () =
    Assert.Equal(None, (filled [ "a" ]).Up("typed", 3, false))
    Assert.Equal(Some "a", (filled [ "a" ]).Up("typed", 0, false))
    Assert.Equal(Some "a", (filled [ "a" ]).Up("", 0, false))

[<Fact>]
let ``multi line recalls only with the caret at the very start`` () =
    Assert.Equal(None, (filled [ "a" ]).Up("x\ny", 2, false))
    Assert.Equal(None, (filled [ "a" ]).Up("x\ny", 1, false))
    Assert.Equal(Some "a", (filled [ "a" ]).Up("x\ny", 0, false))

[<Fact>]
let ``a selection or an empty history leaves the keys alone`` () =
    Assert.Equal(None, (filled [ "a" ]).Up("", 0, true))
    Assert.Equal(None, PromptHistory().Up("", 0, false))
    let h = filled [ "a"; "b" ]
    h.Up("", 0, false) |> ignore
    Assert.Equal(None, h.Down("b", 1, true))

[<Fact>]
let ``down only acts while recalling and with the caret at the end`` () =
    let h = filled [ "a"; "b" ]
    Assert.Equal(None, h.Down("", 0, false))
    h.Up("", 0, false) |> ignore
    h.Up("b", 0, false) |> ignore
    Assert.Equal(None, h.Down("a", 0, false))
    Assert.Equal(Some "b", h.Down("a", 1, false))

[<Fact>]
let ``up stops at the oldest entry`` () =
    let h = filled [ "a"; "b" ]
    for _ in 1 .. 5 do h.Up("", 0, false) |> ignore
    Assert.Equal(Some "a", h.Up("", 0, false))

[<Fact>]
let ``escape returns to the draft and leaves recall`` () =
    let h = filled [ "a" ]
    h.Up("", 0, false) |> ignore
    Assert.Equal(Some "", h.Escape())
    Assert.False h.IsRecalling
    Assert.Equal(None, h.Escape())

[<Fact>]
let ``only an immediate repeat is dropped`` () =
    let h = filled [ "a"; "b"; "a" ]
    Assert.Equal(Some "a", h.Up("", 0, false))
    Assert.Equal(Some "b", h.Up("", 0, false))
    Assert.Equal(Some "a", h.Up("", 0, false))

[<Fact>]
let ``default capacity keeps the latest 100`` () =
    let h = filled [ for i in 1 .. 150 -> string i ]
    let mutable last = None
    for _ in 1 .. 200 do last <- h.Up("", 0, false)
    Assert.Equal(Some "51", last)

[<Fact>]
let ``record and reset both end recall and forget the draft`` () =
    let h = filled [ "a" ]
    h.Up("half", 0, false) |> ignore
    h.Record "new"
    Assert.False h.IsRecalling
    Assert.Equal("box", h.Draft "box")
    h.Up("half", 0, false) |> ignore
    h.Reset()
    Assert.Equal("box", h.Draft "box")
    Assert.Equal("", h.Draft null)
