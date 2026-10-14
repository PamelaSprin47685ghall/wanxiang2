/// 金标准渲染：同一份源码分别对旧程序集与新库编译。旧侧用 ParityProbe 的进程内开关 forceInProcess。
module Render

open Wanxiang.UI

#if INTERACTIVE
let private setEnabled v = ParityProbe.forceInProcess <- v
let private hit n = ParityProbe.hit n
let private count n = ParityProbe.count n
let private reset () = ParityProbe.reset ()
#else
let private probe = ProbeCounter()
let private setEnabled v = probe.Enabled <- v
let private hit (n: string) = probe.Hit n |> ignore
let private count (n: string) = probe.Count n
let private reset () = probe.Reset()
#endif

let all () =
    let lines = ResizeArray<string>()
    let show tag = lines.Add(sprintf "%s a=%d b=%d" tag (count "a") (count "b"))
    reset ()
    setEnabled false
    hit "a"
    show "off"
    setEnabled true
    hit "a"; hit "a"; hit "b"
    show "on"
    setEnabled false
    hit "b"
    show "off-again"
    reset ()
    show "reset"
    setEnabled true
    hit "b"
    show "after-reset"
    lines |> Seq.map (fun l -> l + "\n") |> String.concat ""
