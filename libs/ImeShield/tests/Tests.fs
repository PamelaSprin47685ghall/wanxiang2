module Wanxiang2.ImeShield.Tests.Tests

open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.ImeShield

// 迁自旧 R21ImeTests。
[<Fact>]
let ``384 判定：组合中吞掉回车方向键Esc Tab，ImeProcessed 任何时候都吞，普通字符放行`` () =
    for k in [ Key.Enter; Key.Up; Key.Down; Key.Escape; Key.Tab ] do
        Assert.True(ImeShield.shouldSwallow true k)
        Assert.False(ImeShield.shouldSwallow false k)

    Assert.True(ImeShield.shouldSwallow false Key.ImeProcessed)
    Assert.False(ImeShield.shouldSwallow true Key.A)
    Assert.False(ImeShield.shouldSwallow true Key.Back)

let private presenter (tb: TextBox) =
    tb.GetVisualDescendants()
    |> Seq.pick (function
        | :? TextPresenter as p -> Some p
        | _ -> None)

let private press (tb: TextBox) k =
    tb.RaiseEvent(
        KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = KeyModifiers.None, Source = tb)
    )

[<Fact>]
let ``385 任意 TextBox 的回车处理器在组合中不触发，结束后照常触发；预编辑可见且提交不重复`` () =
    Headless.run (fun () ->
        ImeShield.install ()
        ImeShield.install ()
        let tb = TextBox()
        let mutable submits = 0

        tb.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                submits <- submits + 1
                e.Handled <- true)

        let w = Window(Width = 800.0, Height = 600.0, Content = tb)
        w.Show()
        w.GetLayoutManager().ExecuteLayoutPass()
        Dispatcher.UIThread.RunJobs()
        tb.Focus() |> ignore
        let p = presenter tb
        p.PreeditText <- "ni"
        Assert.True(ImeShield.isComposing tb)
        press tb Key.Enter
        press tb Key.Escape
        Assert.Equal(0, submits)
        Assert.Equal("ni", p.PreeditText)
        p.PreeditText <- null
        tb.RaiseEvent(TextInputEventArgs(RoutedEvent = InputElement.TextInputEvent, Text = "你", Source = tb))
        Assert.Equal("你", tb.Text)
        press tb Key.Enter
        Assert.Equal(1, submits)
        w.Close())
