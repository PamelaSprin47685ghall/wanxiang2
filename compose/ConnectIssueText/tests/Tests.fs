module Tests

open System
open Xunit
open Wanxiang.Client
open Wanxiang.UI

[<Fact>]
let ``describe produces informative non-empty messages for all connection issue variants`` () =
    for issue in Samples.issues do
        let desc = ConnectIssueText.describe issue
        Assert.False(String.IsNullOrWhiteSpace desc)
        match issue with
        | ConnectionIssue.Rejected _ ->
            Assert.Contains("拒绝了连接", desc)
        | ConnectionIssue.TimedOut ->
            Assert.Contains("连接超时", desc)
        | ConnectionIssue.TokenInvalid ->
            Assert.Contains("授权已失效", desc)
        | ConnectionIssue.PairingFrozen m ->
            Assert.Contains(sprintf "%d 分钟" m, desc)
        | ConnectionIssue.PairingRejected _ ->
            Assert.Contains("配对没有成功", desc)
        | ConnectionIssue.Disconnected detail ->
            Assert.Contains("连接已断开", desc)
            if not (String.IsNullOrWhiteSpace detail) then
                Assert.Contains(detail, desc)

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
