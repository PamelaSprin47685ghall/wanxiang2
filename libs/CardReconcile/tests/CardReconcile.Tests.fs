module CardReconcileTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "CardReconcile", "cardreconcile.txt"))

/// 金样由旧代码（旧 Views/ChatView.fs 对账段，照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), CardReconcileRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``appending a message keeps every earlier card and builds only the new one`` () =
    let r = Reconciler<int, obj>()
    let mutable calls = 0
    let mk k = Some k, (fun () -> calls <- calls + 1; obj ())
    r.Reconcile [ mk 1; mk 2 ] |> ignore
    let plan = r.Reconcile [ mk 1; mk 2; mk 3 ]
    Assert.Equal(2, plan.Keep)
    Assert.Equal(1, plan.Append.Length)
    Assert.Equal(3, calls)

[<Fact>]
let ``cards without identity are rebuilt every time and stale cache is dropped`` () =
    let r = Reconciler<int, int>()
    let mutable n = 0
    let fresh () = n <- n + 1; n
    r.Reconcile [ Some 1, fresh; None, fresh ] |> ignore
    let plan = r.Reconcile [ Some 1, fresh; None, fresh ]
    Assert.Equal(1, plan.Keep)
    Assert.Equal<int list>([ 3 ], plan.Append)
    r.Reconcile [ Some 2, fresh ] |> ignore
    Assert.Equal(1, r.CachedCount)
