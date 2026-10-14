module Tests

open System
open System.IO
open Xunit
open Wanxiang.Client
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "connectissuetext.txt"))

/// 金样由旧代码（旧 Views/Dialogs.fs 的 describeIssue 与 retryLabel）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 旧 DevicesUiTests 中不需要窗口的部分

[<Fact>]
let ``each typed connection error maps to its retry action regardless of wording`` () =
    let cases =
        [ ConnectionIssue.Rejected "refused", Some "重试连接"
          ConnectionIssue.TimedOut, Some "重试连接"
          ConnectionIssue.TokenInvalid, Some "重新配对"
          ConnectionIssue.PairingFrozen 5, None
          ConnectionIssue.PairingRejected "pairing code expired", Some "重试配对"
          ConnectionIssue.Disconnected "x", Some "重试连接" ]
    for issue, expected in cases do
        Assert.Equal(expected, ConnectionIssue.retry issue |> Option.map ConnectIssueText.retryLabel)

[<Fact>]
let ``typed error text carries none of the old retry keywords`` () =
    let text = ConnectIssueText.describe (ConnectionIssue.PairingRejected "x")
    for keyword in [ "失败"; "拒绝"; "断开"; "超时"; "错误" ] do
        Assert.DoesNotContain(keyword, text)

[<Fact>]
let ``frozen pairing names the minutes`` () =
    Assert.Contains("5 分钟", ConnectIssueText.describe (ConnectionIssue.PairingFrozen 5))
