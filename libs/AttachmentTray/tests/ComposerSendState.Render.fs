module ComposerSendStateRender

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore

    for i in ComposerSendStateSamples.inputs do
        let v = ComposerSendState.decide i

        line (
            sprintf
                "%A %A %A %A %A %A | %A %A %A %A %b %b %b %b %b %s %s %A"
                i.host
                i.generation
                i.text
                i.attachments
                i.vision
                i.sendKey
                v.gate
                v.primary
                v.queue
                v.vision
                v.VisionBlocked
                (v.gate = SendGate.Send)
                v.SendEnabled
                v.QueueEnabled
                v.QueueVisible
                v.SendTip
                v.SendLabel
                v.QueueTip
        )

    for e in [ HostAvailable; HostDisabled "主机忙" ] do
        for t in ComposerSendStateSamples.texts do
            for a in ComposerSendStateSamples.lists do
                line (sprintf "submit %b %A %A -> %b" (e = HostAvailable) t a (ComposerSendState.canSubmit e t a))

    for c in ComposerSendStateSamples.chipStages do
        line (
            sprintf
                "chip %A -> %s / %s"
                c
                (ComposerSendState.chipState c "1.2 KB")
                (ComposerSendState.chipTip c "1.2 KB")
        )

    sb.ToString()
