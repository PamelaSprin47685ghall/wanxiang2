module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``pairing hint differs between this machine and a remote host`` () =
    let local = PairingHint.codeHint "ws://127.0.0.1:8765/ws"
    let local2 = PairingHint.codeHint "ws://localhost:8765/ws"
    let remote = PairingHint.codeHint "wss://wanxiang.example.com/ws"
    Assert.Equal(local, local2)
    Assert.NotEqual<string>(local, remote)
    Assert.Contains("这台机器", local)
    Assert.Contains("远端", remote)
    for hint in [ local; remote ] do
        Assert.Contains("设备", hint)

[<Fact>]
let ``260 首次连接（无令牌无凭据无错误）才直接走配对码`` () =
    Assert.True(PairingHint.pairingFirst None false None)
    Assert.False(PairingHint.pairingFirst (Some "t") false None)
    Assert.False(PairingHint.pairingFirst None true None)
    Assert.False(PairingHint.pairingFirst None false (Some "连接被拒绝"))
