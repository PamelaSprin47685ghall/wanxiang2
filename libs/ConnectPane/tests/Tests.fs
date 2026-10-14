module Wanxiang2.ConnectPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Input.TextInput
open Avalonia.Threading
open Xunit
open Wanxiang2.ConnectPane
open Wanxiang2.ConnectPane.Tests.Probe
open Wanxiang2.ConnectPane.Tests.Fixture

let private rules =
    { Normalize = fun s -> String(s.ToCharArray() |> Array.filter Char.IsDigit)
      LetterSpacing = fun d -> if d = "" then 0.0 else 4.0
      Hint = fun url -> "在 " + url + " 上查看配对码" }

let private make url =
    let look, _ = make ()
    let p = ConnectPane(look, url, rules)
    let _ = host p.View
    p

[<Fact>]
let ``connect dialog validates pairing code and token connections`` () =
    Headless.run (fun () ->
        let p = make "ws://127.0.0.1:8765/ws"
        let asks = ResizeArray<ConnectAsk>()
        let codes = ResizeArray<string>()
        let short = ref 0
        p.ConnectRequested.Add asks.Add
        p.CodeSubmitted.Add codes.Add
        p.CodeIncomplete.Add(fun () -> short.Value <- short.Value + 1)
        click p.ConnectButton
        Assert.Equal("ws://127.0.0.1:8765/ws", asks[0].Url)
        Assert.False asks[0].UsePairing
        Assert.Equal(None, asks[0].Token)
        Assert.Equal("没有令牌？改用配对码", AutomationProperties.GetName p.PairingToggle)
        click p.PairingToggle
        Assert.True asks[1].UsePairing
        Assert.True p.CodeButton.IsVisible
        Assert.False p.ConnectButton.IsVisible
        Assert.Equal("已有访问令牌？改用令牌", AutomationProperties.GetName p.PairingToggle)
        p.CodeBox.Text <- "123"
        click p.CodeButton
        Assert.Empty codes
        Assert.Equal(1, short.Value)
        p.CodeBox.Text <- "123456"
        click p.CodeButton
        Assert.Equal<string list>([ "123456" ], List.ofSeq codes)
        // 再切回令牌
        click p.PairingToggle
        Assert.True p.ConnectButton.IsVisible
        Assert.False p.IsPairing)

[<Fact>]
let ``pairing code box normalizes pasted text and auto-submits at six digits`` () =
    Headless.run (fun () ->
        let p = make "ws://h/ws"
        let seen = ResizeArray<string>()
        p.CodeChanged.Add seen.Add
        p.StartPairing()
        Assert.Equal(TextInputContentType.Digits, TextInputOptions.GetContentType p.CodeBox)
        p.CodeBox.Text <- "12 3-a4"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("1234", p.CodeBox.Text)
        Assert.Equal(4.0, p.CodeBox.LetterSpacing)
        Assert.Equal("1234", Seq.last seen))

[<Theory>]
[<InlineData("", true, false)>]
[<InlineData("ws://127.0.0.1:8765/ws", false, true)>]
let ``connect focuses the first field that still needs input`` (defaultUrl: string) (expectUrl: bool) (expectToken: bool) =
    Headless.run (fun () ->
        let p = make defaultUrl
        Assert.Equal(expectUrl, obj.ReferenceEquals(p.InitialFocus, p.UrlBox))
        Assert.Equal(expectToken, obj.ReferenceEquals(p.InitialFocus, p.TokenBox)))

[<Fact>]
let ``failures show a typed retry and announcements hide it`` () =
    Headless.run (fun () ->
        let p = make "ws://h/ws"
        let retries = ref 0
        p.RetryClicked.Add(fun () -> retries.Value <- retries.Value + 1)
        p.Fail("主机拒绝了令牌。", Some "重新配对", false)
        Assert.True p.RetryButton.IsVisible
        Assert.Equal("重新配对", AutomationProperties.GetName p.RetryButton)
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting((descendants p.View |> Seq.find (fun c -> AutomationProperties.GetName c = "主机拒绝了令牌。"))))
        click p.RetryButton
        Assert.Equal(1, retries.Value)
        p.Announce "正在连接…"
        Assert.False p.RetryButton.IsVisible
        Assert.Equal("正在连接…", p.StatusText)
        p.UrlBox.Text <- ""
        click p.ConnectButton
        Assert.Equal("请填写主机地址。", p.StatusText))
