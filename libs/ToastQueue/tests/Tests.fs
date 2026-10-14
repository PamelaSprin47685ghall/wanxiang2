module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "toastqueue.txt"))

/// 金样由旧代码（从 旧 Controls/OverlayHost.fs 的入条步骤，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``same text keeps only the newest entry`` () =
    let q = ToastQueue()
    q.Admit "a" |> ignore
    q.Admit "b" |> ignore
    Assert.Equal<string list>([ "a" ], q.Admit "a")
    Assert.Equal<string list>([ "b"; "a" ], q.Showing)

[<Fact>]
let ``never more than the capacity and the oldest goes first`` () =
    let q = ToastQueue(3)
    for t in [ "1"; "2"; "3"; "4"; "5" ] do q.Admit t |> ignore
    Assert.Equal<string list>([ "3"; "4"; "5" ], q.Showing)
    let q2 = ToastQueue(1)
    q2.Admit "x" |> ignore
    Assert.Equal<string list>([ "x" ], q2.Admit "y")

[<Fact>]
let ``removing an unknown text changes nothing`` () =
    let q = ToastQueue()
    q.Admit "a" |> ignore
    q.Remove "zzz"
    Assert.Equal<string list>([ "a" ], q.Showing)
