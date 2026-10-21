module Render

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()
    let j = ExchangeJump<string * uint64>()
    let mutable mounted = Samples.win1
    let mutable hidden = false
    let mutable atBottom = false

    let first () =
        mounted |> Array.tryPick id |> Option.map snd

    let cards () =
        ExchangeJump<string * uint64>.Cards(mounted, Samples.isUser)

    let vp () =
        ExchangeJump<string * uint64>.ViewportIndex(cards (), fun i -> i >= 2)

    for act, arg in Samples.script do
        let r =
            match act with
            | "nav" -> sprintf "%A" (j.NavState(mounted, cards (), vp (), atBottom, hidden))
            | "jump" -> sprintf "%A" (j.Jump(arg, mounted, cards (), vp (), hidden, first ()))
            | "older" ->
                j.ApplyOlderAvailable((arg = 1))
                "-"
            | "hidden" ->
                hidden <- (arg = 1)
                "-"
            | "atbottom" ->
                atBottom <- (arg = 1)
                "-"
            | "fail" ->
                j.OlderFailed()
                "-"
            | "reset" ->
                j.Reset()
                "-"
            | "mount2" ->
                mounted <- Samples.win2
                sprintf "%b" (j.Mounted(mounted, first ()))
            | _ ->
                mounted <- Samples.win1
                sprintf "%b" (j.Mounted(mounted, first ()))

        sb
            .Append(
                sprintf "%s %d -> %s | anchor=%A pending=%b older=%b" act arg r j.Anchor j.PendingPrevious j.OlderOnHost
            )
            .Append('\n')
        |> ignore

    sb
        .Append(
            sprintf
                "vp %d %d %d"
                (ExchangeJump<int>.ViewportIndex([], fun _ -> true))
                (ExchangeJump<int>.ViewportIndex([ 1; 3 ], fun _ -> false))
                (ExchangeJump<int>.ViewportIndex([ 1; 3 ], fun i -> i > 1))
        )
        .Append('\n')
    |> ignore

    sb.ToString()
