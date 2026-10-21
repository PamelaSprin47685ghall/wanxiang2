module LineProcessTests

open System
open System.Collections.Concurrent
open System.Threading
open Xunit
open Wanxiang.Server

let waitFor (cond: unit -> bool) =
    let deadline = DateTime.UtcNow.AddSeconds 5.0

    while not (cond ()) && DateTime.UtcNow < deadline do
        Thread.Sleep 20

    Assert.True(cond ())

[<Fact>]
let ``lines go in and come out; exit is reported once`` () =
    let lines = ConcurrentQueue<string>()
    let exits = ref 0

    let p =
        new LineProcess(
            "/bin/sh",
            [ "-c"; "read l; echo \"got $l\"" ],
            Map.empty,
            lines.Enqueue,
            (fun _ _ -> ()),
            (fun _ -> Interlocked.Increment exits |> ignore)
        )

    p.Start()
    p.WriteLine "hi"
    waitFor (fun () -> exits.Value = 1)
    Assert.Equal<string list>([ "got hi" ], List.ofSeq lines)

[<Fact>]
let ``child sees only the whitelist plus the given variables`` () =
    Environment.SetEnvironmentVariable("WX_PARENT_SECRET", "leak")
    let lines = ConcurrentQueue<string>()
    let exited = ref false

    let p =
        new LineProcess(
            "/bin/sh",
            [ "-c"; "echo \"s=${WX_PARENT_SECRET:-none} g=$WX_GIVEN\"" ],
            Map.ofList [ "WX_GIVEN", "yes" ],
            lines.Enqueue,
            (fun _ _ -> ()),
            (fun _ -> exited.Value <- true)
        )

    p.Start()
    waitFor (fun () -> exited.Value)
    Assert.Equal("s=none g=yes", Seq.exactlyOne lines)

[<Fact>]
let ``stderr goes to its own sink; kill ends the process tree`` () =
    let errs = ConcurrentQueue<string>()

    let p =
        new LineProcess(
            "/bin/sh",
            [ "-c"; "echo oops >&2; sleep 30" ],
            Map.empty,
            ignore,
            (fun _ t -> errs.Enqueue t),
            ignore
        )

    p.Start()
    waitFor (fun () -> Seq.contains "oops" errs)
    Assert.True p.IsRunning
    p.Kill()
    Assert.False p.IsRunning
    Assert.Throws<exn>(fun () -> p.WriteLine "x") |> ignore
