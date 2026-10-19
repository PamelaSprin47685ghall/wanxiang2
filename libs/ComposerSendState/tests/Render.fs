module Render

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    for i in Samples.inputs do
        let v = ComposerSendState.decide i
        line (sprintf "%b %b %b %A %b %A %b %b %s | %b %b %b %b %b %s %s %A"
                i.enabled i.generating i.canStop i.disabledReason i.hasText i.attachments i.visionNo i.enterSends i.visionBlockedMessage
                v.visionBlocked v.canSend v.sendEnabled v.queueEnabled v.queueVisible v.sendTip v.sendLabel v.queueTip)
    for e in [ true; false ] do
        for t in Samples.texts do
            for a in Samples.lists do
                line (sprintf "submit %b %A %A -> %b" e t a (ComposerSendState.canSubmit e t a))
    for r in [ false; true ] do
        for c in Samples.chips do
            line (sprintf "chip %b %A -> %s / %s" r c (ComposerSendState.chipState r c "1.2 KB") (ComposerSendState.chipTip r c "1.2 KB"))
    sb.ToString()
