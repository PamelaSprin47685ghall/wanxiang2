module Wanxiang2.ShortcutHelpPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ShortcutHelpPane
open Wanxiang2.ShortcutHelpPane.Tests.Probe

let private press (w: Window) k mods =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``shortcuts dialog displays sections and closes on button click`` () =
    Headless.run (fun () ->
        let p =
            ShortcutHelpPane([ "全局与导航", [ "Ctrl / ⌘ + N", "新建对话" ]; "对话行", [ "F2", "重命名当前对话" ] ])

        let _ = host p.View
        let text = textOf p.View

        for s in [ "键盘快捷键"; "全局与导航"; "Ctrl / ⌘ + N"; "新建对话"; "对话行"; "F2" ] do
            Assert.Contains(s, text)

        Assert.Same(p.CloseButton, p.InitialFocus)
        Assert.Equal("关闭快捷键帮助 (Esc / Enter)", AutomationProperties.GetHelpText p.CloseButton)
        let n = ref 0
        p.Closed.Add(fun () -> n.Value <- n.Value + 1)
        click p.CloseButton
        Assert.Equal(1, n.Value))
