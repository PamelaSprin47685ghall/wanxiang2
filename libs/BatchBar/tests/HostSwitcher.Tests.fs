module Wanxiang2.HostSwitcher.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.HostSwitcher
open Wanxiang2.WorkspaceState

open Wanxiang2.HostSwitcher.Tests.Probe

let private make () =
    let h = Wanxiang2.HostSwitcher.HostSwitcher()
    let w = host h.View
    h, w

[<Fact>]
let ``host switcher shows authority, connection text and exposes reconnect click`` () =
    Headless.run (fun () ->
        let h, _ = make ()
        h.Render({
            ServerUrl = "https://example.com:8443/path"
            Connected = true
            StatusText = "已连接"
        })
        Assert.Contains("example.com:8443", textOf h.View)
        Assert.Equal("已连接", h.ConnectionText)
        Assert.True h.IsConnected
        let n = ref 0
        h.ReconnectClicked.Add(fun () -> n.Value <- n.Value + 1)
        click h.StatusRow
        Assert.Equal(1, n.Value)
        h.Render({
            ServerUrl = "not-a-url"
            Connected = true
            StatusText = "已连接"
        })
        Assert.Contains("选择主机", textOf h.View))

[<Fact>]
let ``settings and palette buttons fire and render VS works`` () =
    Headless.run (fun () ->
        let h, _ = make ()
        h.Render({
            ServerUrl = "http://localhost:8080"
            Connected = true
            StatusText = "已就绪"
        })
        // 命令面板入口走插槽，不在每帧投影里
        Assert.False h.PaletteButton.IsVisible
        h.SlotPalette true
        Assert.True h.PaletteButton.IsVisible
        Assert.Contains("localhost:8080", textOf h.View)
        Assert.Equal("已就绪", h.ConnectionText)
        let log = ResizeArray<string>()
        h.SettingsClicked.Add(fun () -> log.Add "settings")
        h.PaletteClicked.Add(fun () -> log.Add "palette")
        click h.SettingsButton
        click h.PaletteButton
        Assert.Equal<string list>([ "settings"; "palette" ], List.ofSeq log))
