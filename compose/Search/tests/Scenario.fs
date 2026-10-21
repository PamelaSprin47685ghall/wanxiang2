module Scenario

// 金样场景：一份固定投影上的多次查询，结果编成线上字节。
// 同一份源码既编进新组合者的测试，也编进旧 SearchService.fs 生成金样。

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

let gid (n: int) =
    Guid(sprintf "00000000-0000-0000-0000-%012d" n)

let msg conv (id: uint64) (json: string) deleted =
    {
        commitId = id
        conversationId = conv
        payloadJson = JsonNode.Parse json
        committedAtUtc = t0
        deletedAtCommitId = deleted
    }

let text role (s: string) =
    sprintf """{"role":"%s","contents":[{"$type":"text","text":%s}]}""" role (Text.Json.JsonSerializer.Serialize s)

let conv n title archived deleted last (messages: MessageRecord list) : Conversation<SessionConfig> =
    {
        conversationId = gid n
        title = title
        createdAtUtc = t0
        lastActivityAtUtc = t0
        deleted = deleted
        pinned = false
        archived = archived
        parent = None
        forkBaseCommitId = None
        config = SessionConfig.empty
        messages = messages
        lastCommitId = Some last
    }

let projection: Projection<SessionConfig> =
    let long = String.replicate 80 "前文" + "\nNeedle 在这里\n" + String.replicate 80 "后文"

    let cs =
        [
            conv
                1
                "旧对话"
                false
                false
                10UL
                [
                    msg (gid 1) 2UL (text "user" "needle one") None
                    msg (gid 1) 3UL (text "assistant" "Needle two 😀 haystack") None
                ]
            conv
                2
                "新对话"
                false
                false
                20UL
                [
                    msg (gid 2) 11UL (text "user" long) None
                    msg
                        (gid 2)
                        12UL
                        """{"role":"assistant","contents":[{"$type":"reasoning","text":"needle 想"},{"$type":"functionCall","name":"needle"}]}"""
                        None
                    msg (gid 2) 13UL (text "user" "needle deleted") (Some 14UL)
                    msg
                        (gid 2)
                        15UL
                        """{"role":"assistant","text":"top needle","contents":[{"$type":"text","text":"and hay"}]}"""
                        None
                ]
            conv 3 "归档" true false 30UL [ msg (gid 3) 21UL (text "user" "needle archived") None ]
            conv 4 "已删" false true 40UL [ msg (gid 4) 31UL (text "user" "needle gone") None ]
        ]

    {
        conversations = cs |> List.map (fun c -> c.conversationId, c) |> Map.ofList
        latestCommitId = 40UL
    }

let q (text: string) archived offset limit : SearchQueryData =
    {
        queryId = gid 99
        query = text
        includeArchived = archived
        offset = offset
        limit = limit
    }

let all () =
    let run (x: SearchQueryData) =
        match SearchService.search projection x with
        | Ok page -> WireCodec.encode (SearchResults page)
        | Error e -> "error " + e

    String.concat
        "\n"
        [
            run (q "needle" false 0 20)
            run (q "NEEDLE hay" true 0 20)
            run (q "needle" true 1 2)
            run (q "needle" true 0 0)
            run (q "needle" false -5 500)
            run (q "  " false 0 20)
            run (q "zzz" false 0 20)
            run (q (String('a', 201)) false 0 20)
            SearchService.textOf (
                JsonNode.Parse
                    """{"contents":[{"$type":"text","text":"a"},{"type":"reasoning","text":"b"},{"text":"c"}]}"""
            )
            sprintf "%A" (SearchService.terms " a b\ta ")
            sprintf
                "%A"
                (SearchService.snippetAround (String.replicate 200 "x" + "HIT" + String.replicate 200 "y") 200 3)
        ]
