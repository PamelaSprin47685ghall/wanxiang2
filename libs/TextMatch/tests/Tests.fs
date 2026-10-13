module TextMatchTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``terms split on whitespace and drop duplicates`` () =
    Assert.Equal<string list>([ "a"; "b" ], TextMatch.terms "  a\tb a\n")
    Assert.Empty(TextMatch.terms "   ")

[<Fact>]
let ``every word must appear; the earliest one is reported`` () =
    Assert.Equal(Some(2, 2), TextMatch.firstMatch [ "WOR"; "he" ] "x he world")
    Assert.Equal(None, TextMatch.firstMatch [ "he"; "zz" ] "hello")

[<Fact>]
let ``snippet keeps 30 chars before the hit, marks cuts and flattens newlines`` () =
    let text = String.replicate 50 "a" + "HIT\n" + String.replicate 200 "b"
    let s, start = TextMatch.snippetAround 120 text 50 3
    Assert.StartsWith("…", s)
    Assert.EndsWith("…", s)
    Assert.Equal("HIT", s.Substring(start, 3))
    Assert.DoesNotContain("\n", s)
    let s2, start2 = TextMatch.snippetAround 120 "short hit" 6 3
    Assert.Equal(("short hit", 6), (s2, start2))

[<Fact>]
let ``snippet never splits a surrogate pair`` () =
    let text = "a😀" + String.replicate 29 "a" + "HIT"
    let s, _ = TextMatch.snippetAround 120 text 32 3
    Assert.StartsWith("…😀", s)
