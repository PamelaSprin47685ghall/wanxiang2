module JsonLinesLogTests

open System
open Xunit
open Wanxiang.Server

let fixedNow () = DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)

[<Fact>]
let ``error lines carry the envelope, typed fields and redacted text`` () =
    let lines = ResizeArray<string>()
    let log = JsonLinesLog(lines.Add, fixedNow, 2)
    log.RegisterSecrets [ "sk-12345678"; "sk-12345678-long"; "short" ]
    log.SetInstanceId "i1"
    log.Error("e", [ "s", box "key sk-12345678-long and sk-12345678 and short"; "n", box 3; "u", box 7UL; "b", box true; "z", null ])
    Assert.Equal("""{"level":"error","event":"e","utc":"2026-01-02T03:04:05.0000000Z","instanceId":"i1","s":"key *** and *** and short","n":3,"u":7,"b":true,"z":null}""", lines[0])

[<Fact>]
let ``exceptions get a stack field; info is not kept as a recent error; recent is bounded`` () =
    let lines = ResizeArray<string>()
    let log = JsonLinesLog(lines.Add, fixedNow, 2)
    log.Error("a", [ "x", box (InvalidOperationException "boom") ])
    Assert.Contains("\"x\":\"InvalidOperationException: boom\"", lines[0])
    Assert.Contains("\"x.stack\":", lines[0])
    log.Info "hi"
    log.Error("b", [])
    log.Error("c", [])
    Assert.Equal(2, log.RecentErrors.Length)
    Assert.Contains("\"event\":\"c\"", List.last log.RecentErrors)
    Assert.Equal("""{"level":"info","event":"info","utc":"2026-01-02T03:04:05.0000000Z","message":"hi"}""", lines[1])
