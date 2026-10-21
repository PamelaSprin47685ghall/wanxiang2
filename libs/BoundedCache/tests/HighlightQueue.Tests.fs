module HighlightQueueTests

open System
open System.IO
open System.Threading.Tasks
open Xunit
open Wanxiang.UI


[<Fact>]
let ``completions inside one window share a single notification`` () =
    let q = HighlightQueue<int>()

    for k in 1..3 do
        Assert.True(q.TryClaim k)

    Assert.True(q.Finished(1, true))
    Assert.False(q.Finished(2, true))
    Assert.False(q.Finished(3, false))
    q.NotifyFired()
    Assert.True(q.TryClaim 4)
    Assert.True(q.Finished(4, true))

[<Fact>]
let ``concurrent claims of one key let exactly one thread win and one completion notify`` () =
    let q = HighlightQueue<string>()
    let claims = [| for _ in 1..64 -> Task.Run(fun () -> q.TryClaim "same") |]
    Task.WaitAll(claims |> Array.map (fun t -> t :> Task))
    Assert.Equal(1, claims |> Array.filter (fun t -> t.Result) |> Array.length)
    let keys = [| for i in 1..64 -> string i |]

    for k in keys do
        q.TryClaim k |> ignore

    let done' = [| for k in keys -> Task.Run(fun () -> q.Finished(k, true)) |]
    Task.WaitAll(done' |> Array.map (fun t -> t :> Task))
    Assert.Equal(1, done' |> Array.filter (fun t -> t.Result) |> Array.length)
    Assert.Equal(1, q.InFlightCount)
