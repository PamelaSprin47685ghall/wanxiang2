module Render

open System.Text
open System.Text.Json.Nodes
open Wanxiang.UI

let all (lines: string list) =
    let sb = StringBuilder()
    for line in lines do
        let o = JsonNode.Parse(line).AsObject()
        let c = Catalog.parse (o["providers"].AsArray()) (o["tools"].AsArray()) (o["generation"].AsObject())
        sb.AppendLine(sprintf "%A" c) |> ignore
        sb.AppendLine(sprintf "ready=%b usable=%A default=%A" (Catalog.isReady c) (Catalog.usableProviders c |> List.map _.id) (Catalog.defaultSelection c)) |> ignore
        for pid in [ "oa"; "a"; "b"; "off"; "none" ] do
            for m in [ ""; "a"; "zzz" ] do
                sb.AppendLine(sprintf "%s/%s: %s global=%b" pid m (Catalog.describeModel pid m c) (Catalog.isGlobalDefault pid c)) |> ignore
    sb.ToString()
