module Tests

open System
open System.Net.Sockets
open Xunit
open Wanxiang.Client

// 迁自旧 DevicesUiTests（只改为限定名访问）。
[<Fact>]
let ``exceptions are classified into typed issues`` () =
    let refused = SocketException(int SocketError.ConnectionRefused)
    let wrapped = Net.WebSockets.WebSocketException("x", Net.Http.HttpRequestException("y", refused))
    match ConnectionIssue.ofException wrapped with
    | ConnectionIssue.Rejected _ -> ()
    | other -> failwithf "expected Rejected, got %A" other
    match ConnectionIssue.ofException (TimeoutException "t") with
    | ConnectionIssue.TimedOut -> ()
    | other -> failwithf "expected TimedOut, got %A" other
    match ConnectionIssue.ofException (SocketException(int SocketError.TimedOut)) with
    | ConnectionIssue.TimedOut -> ()
    | other -> failwithf "expected TimedOut, got %A" other
    match ConnectionIssue.ofException (InvalidOperationException "boom") with
    | ConnectionIssue.Disconnected d -> Assert.Equal("boom", d)
    | other -> failwithf "expected Disconnected, got %A" other

[<Fact>]
let ``每种问题对应的重试动作`` () =
    Assert.Equal(Some RetryConnect, ConnectionIssue.retry (ConnectionIssue.Rejected "x"))
    Assert.Equal(Some RetryConnect, ConnectionIssue.retry ConnectionIssue.TimedOut)
    Assert.Equal(Some RetryConnect, ConnectionIssue.retry (ConnectionIssue.Disconnected "x"))
    Assert.Equal(Some RePair, ConnectionIssue.retry ConnectionIssue.TokenInvalid)
    Assert.Equal(Some RetryPairing, ConnectionIssue.retry (ConnectionIssue.PairingRejected "bad"))
    Assert.Equal(None, ConnectionIssue.retry (ConnectionIssue.PairingFrozen 5))

[<Fact>]
let ``取消算超时，其他套接字错误算断线并保留最外层消息`` () =
    Assert.Equal(ConnectionIssue.TimedOut, ConnectionIssue.ofException (OperationCanceledException()))
    let reset = Exception("outer", SocketException(int SocketError.ConnectionReset))
    Assert.Equal(ConnectionIssue.Disconnected "outer", ConnectionIssue.ofException reset)

[<Fact>]
let ``套接字错误优先于外层的超时异常`` () =
    let e = TimeoutException("t", SocketException(int SocketError.ConnectionRefused))
    match ConnectionIssue.ofException e with
    | ConnectionIssue.Rejected m -> Assert.Equal("t", m)
    | other -> failwithf "expected Rejected, got %A" other

[<Fact>]
let ``深层嵌套也能找到套接字错误`` () =
    let e = Exception("a", Exception("b", Exception("c", SocketException(int SocketError.TimedOut))))
    Assert.Equal(ConnectionIssue.TimedOut, ConnectionIssue.ofException e)

// 迁自旧 AppShell.HandleEvent 的 PairingFailed 分支：与原样摘录逐例比对。
[<Fact>]
let ``pairing failure maps like old AppShell`` () =
    let old (d: {| reason: string; frozen: bool; freezeMinutes: int |}) =
        if d.frozen then ConnectionIssue.PairingFrozen d.freezeMinutes else ConnectionIssue.PairingRejected d.reason
    for frozen in [ true; false ] do
        for m in [ 0; 15 ] do
            for r in [ ""; "code expired" ] do
                Assert.Equal(old {| reason = r; frozen = frozen; freezeMinutes = m |}, ConnectionIssue.ofPairingFailure frozen m r)
    Assert.Equal("配对码已生成，请输入后提交。", ConnectionIssue.pairingStartedStatus)
    Assert.Equal("配对成功，正在认证…", ConnectionIssue.pairingSucceededStatus)
