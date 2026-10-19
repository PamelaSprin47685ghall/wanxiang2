module Wanxiang2.PromptPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.PromptPane
open Wanxiang2.PromptPane.Tests.Probe
open Wanxiang2.PromptPane.Tests.Fixture

let private press (w: Window) k mods =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``prompt dialog validates input and handles confirm and cancel`` () =
    Headless.run (fun () ->
        let look, _ = make ()
        let p = PromptPane(look, "重命名", "对话标题", "旧标题", "保存", fun _ -> Some(TextBlock() :> Control))
        let w = host p.View
        let sent = ResizeArray<string>()
        let errors = ResizeArray<string option>()
        let cancels = ref 0
        p.Submitted.Add sent.Add
        p.FieldError.Add errors.Add
        p.Cancelled.Add(fun () -> cancels.Value <- cancels.Value + 1)
        Assert.True p.ConfirmButton.IsEnabled
        p.Box.Text <- "   "
        Dispatcher.UIThread.RunJobs()
        Assert.False p.ConfirmButton.IsEnabled
        Assert.Equal(Some "输入内容不能全为空白字符。", Seq.last errors)
        p.Box.Text <- ""
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("内容不能为空，请输入有效文本。", textOf p.View)
        p.Box.Text <- "  新标题 "
        Dispatcher.UIThread.RunJobs()
        p.Box.Focus() |> ignore
        press w Key.Enter RawInputModifiers.None
        Assert.Equal<string list>([ "新标题" ], List.ofSeq sent)
        p.SetPending true
        Assert.Equal("正在保存…", AutomationProperties.GetName p.ConfirmButton)
        Assert.False p.CancelButton.IsEnabled
        press w Key.Enter RawInputModifiers.None
        Assert.Single sent |> ignore
        p.SetPending false
        click p.ConfirmButton
        click p.CancelButton
        Assert.Equal(2, sent.Count)
        Assert.Equal(1, cancels.Value))

[<Fact>]
let ``prompt footer arrows and row enter follow the old action row`` () =
    Headless.run (fun () ->
        let look, _ = make ()
        let p = PromptPane(look, "重命名", "对话标题", "标题", "保存", fun _ -> None)
        let w = host p.View
        let sent = ResizeArray<string>()
        let cancels = ref 0
        p.Submitted.Add sent.Add
        p.Cancelled.Add(fun () -> cancels.Value <- cancels.Value + 1)
        p.CancelButton.Focus() |> ignore
        press w Key.Right RawInputModifiers.None
        Assert.True p.ConfirmButton.IsFocused
        press w Key.Left RawInputModifiers.None
        Assert.True p.CancelButton.IsFocused
        press w Key.Space RawInputModifiers.None
        Assert.Equal(1, cancels.Value)
        Assert.Empty sent)
