module ModelPickerListRender

open System.Text
open Wanxiang.UI

let render () =
    let sb = StringBuilder()

    for l in ModelPickerListSamples.lists do
        for q in ModelPickerListSamples.queries do
            sb
                .Append(
                    sprintf "%A %A -> %A first=%A" l q (ModelPickerList.groups fst q l) (ModelPickerList.enterPick l)
                )
                .Append('\n')
            |> ignore

    for up in [ false; true ] do
        for n in [ 0; 1; 3 ] do
            for c in -1 .. n do
                sb.Append(sprintf "move %b %d %d -> %A" up c n (ModelPickerList.move up c n)).Append('\n')
                |> ignore

    sb
        .Append(
            sprintf
                "%b %b %s %s"
                (ModelPickerList.shouldPick true)
                (ModelPickerList.shouldPick false)
                ModelPickerList.emptyText
                ModelPickerList.searchHelp
        )
        .Append('\n')
    |> ignore

    sb.ToString()
