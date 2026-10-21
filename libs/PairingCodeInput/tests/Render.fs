module Render

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()

    for r in Samples.raws do
        let d = PairingCodeInput.normalize r

        sb.Append(sprintf "%A -> %s %g" r d (PairingCodeInput.letterSpacing d)).Append('\n')
        |> ignore

    let a = PairingAutoSubmit()

    for t in Samples.typing do
        sb.Append(sprintf "type %s -> %b" t (a.Changed t)).Append('\n') |> ignore

    sb.Append(PairingCodeInput.incomplete).Append('\n') |> ignore
    sb.ToString()
