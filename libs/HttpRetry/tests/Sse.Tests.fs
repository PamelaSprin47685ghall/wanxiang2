module SseTests

open System.IO
open System.Text
open System.Threading
open Xunit
open Wanxiang.Agent

let events (text: string) =
    use stream = new MemoryStream(Encoding.UTF8.GetBytes text)
    let e = (Sse.read stream CancellationToken.None).GetAsyncEnumerator()
    [ while e.MoveNextAsync().AsTask().Result do yield e.Current ]

[<Fact>]
let ``frames split on blank lines, multi-line data joins with newline, tail without newline still counts`` () =
    let es =
        events ("event: custom\ndata: first line\ndata: second line without space\n\n" + "data: solo event\n\n" + "data: trailing data without final newline")
    Assert.Equal<SseEvent list>(
        [ { name = "custom"; data = "first line\nsecond line without space" }
          { name = ""; data = "solo event" }
          { name = ""; data = "trailing data without final newline" } ], es)

[<Fact>]
let ``comments are skipped and the event name resets after each frame`` () =
    let es = events (": keepalive\nevent: update\ndata: line 1\ndata: line 2\n\ndata: final\n\n")
    Assert.Equal<SseEvent list>([ { name = "update"; data = "line 1\nline 2" }; { name = ""; data = "final" } ], es)

[<Fact>]
let ``only the single space after the colon is stripped, indentation survives`` () =
    let es = events ("data:     def indented_code():\ndata:         return 42\n\n")
    Assert.Equal("    def indented_code():\n        return 42", (List.exactlyOne es).data)

[<Fact>]
let ``a frame without data is not an event`` () =
    Assert.Empty(events "event: ping\n\n")
