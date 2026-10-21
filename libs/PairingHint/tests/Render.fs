module Render

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()

    let js (s: string) =
        System.Text.Json.JsonSerializer.Serialize s

    for u in Samples.urls do
        sb.Append(sprintf "isLoopbackUrl %s -> %b" (js u) (PairingHint.isLoopbackUrl u)).Append('\n')
        |> ignore

        sb.Append(sprintf "codeHint %s -> %s" (js u) (PairingHint.codeHint u)).Append('\n')
        |> ignore

    for t, h, e in Samples.firsts do
        sb.Append(sprintf "pairingFirst %A %b %A -> %b" t h e (PairingHint.pairingFirst t h e)).Append('\n')
        |> ignore

    sb.ToString()
