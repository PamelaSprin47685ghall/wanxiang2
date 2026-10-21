module ApprovalsTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiang.Server

let desk () =
    ApprovalDesk<string, bool, string>((fun ok -> if ok then "approved" else "denied"), 2)

[<Fact>]
let ``first decision wins; later ones see the memo`` () =
    let d = desk ()
    let id = Guid.NewGuid()
    let t = d.Open(id, "item")
    let seen = ResizeArray()
    Assert.True(d.Resolve(id, true, fun i m -> seen.Add(i + ":" + m)))
    Assert.False(d.Resolve(id, false, fun _ _ -> failwith "no"))
    Assert.True(t.Result)
    Assert.Equal<string list>([ "item:approved" ], List.ofSeq seen)
    Assert.Equal(Some "approved", d.Recent id)
    Assert.Equal(0, d.Count)

[<Fact>]
let ``beforeComplete runs before the waiter sees the result`` () =
    let d = desk ()
    let id = Guid.NewGuid()
    let mutable flag = false
    let t = d.Open(id, "x").ContinueWith(fun (_: Task<bool>) -> flag)
    d.Resolve(id, true, fun _ _ -> flag <- true) |> ignore
    Assert.True(t.Result)

[<Fact>]
let ``recent memory is bounded`` () =
    let d = desk ()
    let ids = [ for _ in 1..3 -> Guid.NewGuid() ]

    for id in ids do
        d.Open(id, "") |> ignore
        d.Resolve(id, false, fun _ _ -> ()) |> ignore

    Assert.Equal(None, d.Recent ids[0])
    Assert.Equal(Some "denied", d.Recent ids[2])

[<Fact>]
let ``pending lists what is still waiting`` () =
    let d = desk ()
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    d.Open(a, "a") |> ignore
    d.Open(b, "b") |> ignore
    d.Resolve(a, true, fun _ _ -> ()) |> ignore
    Assert.Equal<(Guid * string) list>([ b, "b" ], d.Pending)
