module Render

open System
open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()
    let s = ScrollTween()
    let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    for act, ms, offset, target in Samples.script do
        let now = t0.AddMilliseconds ms
        let r =
            match act with
            | "start" -> string (s.Start(now, offset, target, false))
            | "startReduced" -> string (s.Start(now, offset, target, true))
            | "frame" ->
                match s.Frame(now, target) with
                | ScrollFrame.Offset y -> sprintf "offset %.6f" y
                | ScrollFrame.Done y -> sprintf "done %.6f" y
            | "cancel" -> s.Cancel(); "-"
            | "pin" -> s.Pin offset; "-"
            | _ -> string (s.IsOwnOffset offset)
        sb.Append(sprintf "%s %g %g %g -> %s running=%b" act ms offset target r s.IsRunning).Append('\n') |> ignore
    for t in Samples.curve do
        sb.Append(sprintf "ease %g = %.9f" t (EaseOutCubic.ease t)).Append('\n') |> ignore
    sb.Append(sprintf "%g %g %g" ScrollTween.DurationMs ScrollTween.FrameMs ScrollTween.NearPx).Append('\n') |> ignore
    sb.ToString()
