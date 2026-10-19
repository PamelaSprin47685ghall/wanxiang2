module HostRequestsRender
open System
open Wanxiang.UI

let private g (n: int) = Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)
let private t0 = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)
let private timeout = TimeSpan.FromSeconds 15.0

let all () =
    let lines = ResizeArray<string>()
    let probes = PendingProbes()
    probes.Track(g 1, "openai", t0)
    probes.Track(g 2, "local", t0.AddSeconds 5.0)
    probes.Track(g 3, "anthropic", t0.AddSeconds 10.0)
    probes.Resolve(g 2)
    probes.Resolve(g 9)
    for s in [ 14.0; 15.0; 24.0; 25.0; 40.0 ] do
        lines.Add(sprintf "probe %g %A" s (probes.Expire(t0.AddSeconds s, timeout)))
    probes.Track(g 1, "openai", t0)
    probes.Track(g 1, "openai2", t0.AddSeconds 20.0)
    lines.Add(sprintf "probe-retrack %A" (probes.Expire(t0.AddSeconds 34.0, timeout)))
    lines.Add(sprintf "probe-retrack %A" (probes.Expire(t0.AddSeconds 35.0, timeout)))
    let search = SearchInFlight()
    lines.Add(sprintf "search idle expire=%b accept=%b" (search.Expire(t0.AddMinutes 5.0, timeout)) (search.Accept(g 1)))
    search.Sent(g 1, t0)
    search.Sent(g 2, t0.AddSeconds 3.0)
    lines.Add(sprintf "search stale=%b latest=%A" (search.Accept(g 1)) search.Latest)
    lines.Add(sprintf "search early-expire=%b" (search.Expire(t0.AddSeconds 17.0, timeout)))
    lines.Add(sprintf "search expire=%b latest=%A" (search.Expire(t0.AddSeconds 18.0, timeout)) search.Latest)
    lines.Add(sprintf "search after-expire accept=%b" (search.Accept(g 2)))
    search.Sent(g 3, t0)
    lines.Add(sprintf "search accept=%b again=%b expire=%b latest=%A" (search.Accept(g 3)) (search.Accept(g 3)) (search.Expire(t0.AddHours 1.0, timeout)) search.Latest)
    lines |> Seq.map (fun l -> l + "\n") |> String.concat ""
