module Tests

open System
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Server

let private u (i: int64 option) (o: int64 option) (c: int64 option) (t: int64 option) =
    let d = UsageDetails()
    let n = function Some v -> Nullable v | None -> Nullable()
    d.InputTokenCount <- n i
    d.OutputTokenCount <- n o
    d.CachedInputTokenCount <- n c
    d.TotalTokenCount <- n t
    d

let private all (t: UsageTally) = t.PromptTokens, t.CompletionTokens, t.CachedTokens, t.TotalTokens

[<Fact>]
let ``没有任何一轮时全部为 None`` () =
    Assert.Equal((None, None, None, None), all (UsageTally()))

[<Fact>]
let ``多轮逐项相加`` () =
    let t = UsageTally()
    t.Add(u (Some 10L) (Some 2L) (Some 1L) (Some 12L))
    t.Add(u (Some 5L) (Some 3L) (Some 0L) (Some 8L))
    Assert.Equal((Some 15, Some 5, Some 1, Some 20), all t)

[<Fact>]
let ``某轮缺项时保留已有值，从没报告过的项保持 None`` () =
    let t = UsageTally()
    t.Add(u (Some 10L) None None None)
    t.Add(u None (Some 4L) None None)
    Assert.Equal((Some 10, Some 4, None, None), all t)

[<Fact>]
let ``报告为 0 的项是 Some 0 而不是 None`` () =
    let t = UsageTally()
    t.Add(u (Some 0L) None None None)
    Assert.Equal(Some 0, t.PromptTokens)
