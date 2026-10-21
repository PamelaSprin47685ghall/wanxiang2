module Render

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()

    let js (s: string) =
        System.Text.Json.JsonSerializer.Serialize s

    let shown =
        function
        | Some s -> js s
        | None -> "None"

    let h = PromptHistory()

    for op in Samples.script do
        let result =
            match op with
            | Samples.Record t ->
                h.Record t
                "()"
            | Samples.Up(t, c, s) -> shown (h.Up(t, c, s))
            | Samples.Down(t, c, s) -> shown (h.Down(t, c, s))
            | Samples.Escape -> shown (h.Escape())
            | Samples.Reset ->
                h.Reset()
                "()"
            | Samples.Draft t -> js (h.Draft t)

        sb.Append(sprintf "%A -> %s recalling=%b" op result h.IsRecalling).Append('\n')
        |> ignore

    sb.ToString()
