module HostHealthTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

[<Fact>]
let ``checks parse leniently and labels map protocol names`` () =
    let items =
        JsonNode
            .Parse("""[{"name":"config","ok":true,"detail":"d"},{"name":"x","ok":"yes"},{"ok":true},3,null]""")
            .AsArray()

    let checks = DoctorCheck.parseChecks items

    Assert.Equal<DoctorCheck list>(
        [
            {
                name = "config"
                ok = true
                detail = "d"
            }
            { name = "x"; ok = false; detail = "" }
        ],
        checks
    )

    Assert.Equal("配置文件", DoctorCheck.label "config")
    Assert.Equal("unknown-check", DoctorCheck.label "unknown-check")

[<Fact>]
let ``host health owns doctor and config-issue state`` () =
    let h = HostHealth()
    let mutable fired = 0
    let mutable requested = 0
    h.Changed.Add(fun () -> fired <- fired + 1)
    h.CheckRequested.Add(fun () -> requested <- requested + 1)
    Assert.Equal(NotRun, h.Doctor)
    h.BeginCheck()
    Assert.Equal(Running, h.Doctor)
    Assert.Equal(1, requested)
    h.Disconnected()

    match h.Doctor with
    | Unavailable r -> Assert.Contains("连接已断开", r)
    | other -> failwithf "%A" other

    h.Disconnected()
    Assert.Equal(2, fired)
    h.ApplyConfigIssue("bad", [ "e" ])
    Assert.True(h.ClearConfigIssue())
    Assert.False(h.ClearConfigIssue())
    h.ApplyReport(true, [])

    match h.Doctor with
    | Report(true, [], _) -> ()
    | other -> failwithf "%A" other

    h.Reset()
    Assert.Equal(NotRun, h.Doctor)
    Assert.Equal(None, h.ConfigIssue)
