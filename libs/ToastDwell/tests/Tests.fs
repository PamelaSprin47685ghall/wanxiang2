module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "toastdwell.txt"))

/// 金样由旧代码（从 旧 Controls/OverlayHost.fs 的计时与暂停，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

let private ticksUntilExpired (d: ToastDwell) =
    let mutable n = 1
    while not (d.Tick 200.0) do n <- n + 1
    n

[<Fact>]
let ``dwell length decides when it expires`` () =
    Assert.Equal(20, ticksUntilExpired (ToastDwell(4000.0, false)))
    Assert.Equal(45, ticksUntilExpired (ToastDwell(9000.0, false)))

[<Fact>]
let ``an inline action stays at least eight seconds`` () =
    Assert.Equal(40, ticksUntilExpired (ToastDwell(4000.0, true)))
    Assert.Equal(45, ticksUntilExpired (ToastDwell(9000.0, true)))

[<Fact>]
let ``hover and a focused action pause the countdown`` () =
    let d = ToastDwell(400.0, false)
    d.PointerEntered()
    for _ in 1 .. 10 do Assert.False(d.Tick 200.0)
    d.ActionFocused()
    d.PointerExited true
    Assert.False(d.Tick 200.0)
    Assert.Equal(400.0, d.RemainingMs)
    d.PointerExited false
    Assert.False(d.Tick 200.0)
    Assert.True(d.Tick 200.0)

[<Fact>]
let ``losing action focus resumes even while hovered`` () =
    let d = ToastDwell(200.0, false)
    d.PointerEntered()
    d.ActionFocused()
    d.ActionUnfocused()
    Assert.True(d.Tick 200.0)
