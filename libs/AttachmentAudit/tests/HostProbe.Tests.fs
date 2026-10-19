module HostProbeTests

open System
open System.IO
open System.Net
open System.Net.Sockets
open Xunit
open Wanxiang.Server

[<Fact>]
let ``a listening port is in use; a closed one or bad text is not`` () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let port = (l.LocalEndpoint :?> IPEndPoint).Port
    Assert.True(HostProbe.portInUse (sprintf "127.0.0.1:%d" port))
    l.Stop()
    Assert.False(HostProbe.portInUse (sprintf "127.0.0.1:%d" port))
    Assert.False(HostProbe.portInUse "nonsense")

[<Fact>]
let ``free bytes are reported for an existing path`` () =
    Assert.True((HostProbe.freeBytes (Path.GetTempPath())).Value > 0L)

[<Fact>]
let ``only directories open to group or others are listed`` () =
    let root = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"))
    let tight, loose = Path.Combine(root, "a"), Path.Combine(root, "b")
    Directory.CreateDirectory tight |> ignore
    Directory.CreateDirectory loose |> ignore
    File.SetUnixFileMode(tight, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute)
    File.SetUnixFileMode(loose, UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute ||| UnixFileMode.OtherRead)
    Assert.Equal<string list>([ loose ], HostProbe.groupOrOtherAccessible [ tight; loose; Path.Combine(root, "missing") ])
