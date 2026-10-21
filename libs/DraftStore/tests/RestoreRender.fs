module RestoreRender

open System
open Wanxiang.UI

// 固定场景，旧摘录与新实现各跑一遍，逐行比对。
let g (n: int) =
    Guid(sprintf "00000000-0000-0000-0000-%012d" n)

let att =
    {
        fileName = "a.png"
        mediaType = "image/png"
        size = 3L
        sha256 = "x"
    }

let dr i c t a =
    {
        instanceId = i
        conversationId = c
        text = t
        attachments = a
    }

let un i c =
    {
        instanceId = i
        conversationId = c
        invocationId = g 99
        text = "u"
        attachments = []
        savedAt = DateTimeOffset.UnixEpoch
    }

let snapshots =
    [
        "empty", DraftSnapshot.empty
        "blank",
        {
            drafts = [ dr "h1" None "  " [] ]
            unsent = []
        }
        "text",
        {
            drafts = [ dr "h1" (Some(g 1)) "hi" []; dr "h2" (Some(g 2)) "x" [ att ] ]
            unsent = []
        }
        "att",
        {
            drafts = [ dr "h1" (Some(g 1)) "" [ att ]; dr "h1" None "s" [] ]
            unsent = [ un "h1" (g 3); un "h2" (g 4) ]
        }
        "mixed",
        {
            drafts =
                [
                    dr "h1" (Some(g 1)) "a" []
                    dr "h1" (Some(g 2)) "" [ att ]
                    dr "h2" (Some(g 1)) "b" []
                ]
            unsent = [ un "h1" (g 2); un "h1" (g 5) ]
        }
    ]

let ids (s: Set<Guid>) =
    s
    |> Seq.map (fun x -> string (x.ToString().TrimStart('0', '-')))
    |> String.concat ","

let show (s: DraftSnapshot) =
    sprintf
        "drafts=[%s] unsent=[%s]"
        (s.drafts
         |> List.map (fun d ->
             sprintf
                 "%s/%A/%s/%d"
                 d.instanceId
                 (d.conversationId |> Option.map (fun x -> x.ToString().TrimStart('0', '-')))
                 d.text
                 d.attachments.Length)
         |> String.concat ";")
        (s.unsent
         |> List.map (fun u -> sprintf "%s/%s" u.instanceId (u.conversationId.ToString().TrimStart('0', '-')))
         |> String.concat ";")

let render known notice fill counts prune knownAfter (cleared: string) =
    let sb = Text.StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore

    for name, s in snapshots do
        for inst in [ "h1"; "h2"; "h3" ] do
            line (sprintf "%s %s known=%s" name inst (ids (known s inst)))

            for n in [ 0; 1; 2 ] do
                line (sprintf "%s %s unsent=%d notice=%A" name inst n (notice s inst n))

            for gone in [ Set.empty; set [ g 1 ]; set [ g 2; g 3 ]; set [ g 5; g 4 ] ] do
                line (sprintf "%s %s prune %s -> %s" name inst (ids gone) (show (prune s inst gone)))

        line (sprintf "%s counts=%A" name (counts s))

    for c in [ ""; " "; "x" ] do
        for r in [ ""; "\n"; "y" ] do
            line (sprintf "fill %A %A = %b" c r (fill c r))

    for k in [ Set.empty; set [ g 1; g 2 ] ] do
        for gone in [ Set.empty; set [ g 2 ] ] do
            for l in [ Set.empty; set [ g 2; g 7 ] ] do
                line (sprintf "knownAfter %s - %s + %s = %s" (ids k) (ids gone) (ids l) (ids (knownAfter k gone l)))

    line ("cleared " + cleared)
    sb.ToString()
