module Render

open System
open Wanxiang.UI

let private g (n: int) =
    Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)

let all () =
    let order = [ string (g 3); "bad"; string (g 2); string (g 1) ]

    [
        for active in [ None; Some(g 1); Some(g 2); Some(g 3); Some(g 9) ] do
            for removed in [ []; [ g 2 ]; [ g 3; g 2 ]; [ g 1; g 2; g 3 ]; [ g 3 ]; [ g 9 ] ] ->
                sprintf "%A %A => %A" active removed (PendingDeletion.neighborAfter active removed order)
        for n in [ 1; 2; 7 ] -> PendingDeletion.restoredText n
    ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
