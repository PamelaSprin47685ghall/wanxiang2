module FuzzyMatchTests
open Xunit
open Wanxiang.UI

[<Fact>]
let ``empty query matches with zero`` () = Assert.Equal(Some 0, FuzzyMatch.score "  " "anything")

[<Fact>]
let ``substring beats subsequence and prefix beats inner substring`` () =
    let prefix = FuzzyMatch.score "new" "new chat" |> Option.get
    let inner = FuzzyMatch.score "new" "a new chat" |> Option.get
    let subseq = FuzzyMatch.score "nct" "new chat" |> Option.get
    Assert.True(prefix > inner)
    Assert.True(inner > subseq)

[<Fact>]
let ``missing character yields None and spaces in query are skipped`` () =
    Assert.Equal(None, FuzzyMatch.score "xyz" "new chat")
    Assert.True((FuzzyMatch.score "n c" "new chat").IsSome)

[<Fact>]
let ``scores are stable values from the old implementation`` () =
    // 数值即旧 CommandPalette.fuzzyScore 的输出，排序依赖它们。
    Assert.Equal(Some 1192, FuzzyMatch.score "new" "new chat")
    Assert.Equal(Some 972, FuzzyMatch.score "chat" "new chat")
