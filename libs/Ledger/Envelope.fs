namespace Ledger

open System
open System.Globalization
open System.Text.Json.Nodes

/// 提交外壳的 JSON 形状：
/// {"v":1,"id":1,"at":"2026-10-13T00:00:00.0000000+00:00","stamp":{"id":"..","hash":".."},"events":[..]}
/// 本格式不承诺兼容任何旧格式（版本号从 1 起）。
module Envelope =
    [<Literal>]
    let Version = 1

    let encode (codec: Codec<'E>) (c: Commit<'E>) : string =
        let o = JsonObject()
        o["v"] <- JsonValue.Create Version
        // id 用字符串承载：uint64 超出 double 精确范围（见 Canonical LEMMA 2）
        o["id"] <- JsonValue.Create(c.id.ToString CultureInfo.InvariantCulture)
        o["at"] <- JsonValue.Create(c.at.ToString("o", CultureInfo.InvariantCulture))
        match c.stamp with
        | Some s ->
            let so = JsonObject()
            so["id"] <- JsonValue.Create s.commandId
            so["hash"] <- JsonValue.Create s.hash
            o["stamp"] <- so
        | None -> ()
        let arr = JsonArray()
        for e in c.events do arr.Add(codec.encode e)
        o["events"] <- arr
        o.ToJsonString()

    type Decoded<'E> =
        | Ok of Commit<'E>
        | Bad of string
        | FutureVersion of int

    let decode (codec: Codec<'E>) (text: string) : Decoded<'E> =
        try
            match JsonNode.Parse text with
            | :? JsonObject as o ->
                let v = o["v"].GetValue<int>()
                if v > Version then FutureVersion v
                elif v <> Version then Bad(sprintf "unknown version %d" v)
                else
                    let id = UInt64.Parse(o["id"].GetValue<string>(), CultureInfo.InvariantCulture)
                    let at = DateTimeOffset.Parse(o["at"].GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    let stamp =
                        match o["stamp"] with
                        | :? JsonObject as s -> Some { commandId = s["id"].GetValue<string>(); hash = s["hash"].GetValue<string>() }
                        | _ -> None
                    let evs = o["events"].AsArray() |> Seq.map (fun n -> codec.decode n) |> List.ofSeq
                    match evs |> List.tryPick (function Error e -> Some e | _ -> None) with
                    | Some e -> Bad("event decode: " + e)
                    | None -> Ok { id = id; at = at; stamp = stamp; events = evs |> List.map (function Result.Ok e -> e | _ -> failwith "unreachable") }
            | _ -> Bad "record is not a JSON object"
        with e -> Bad(e.Message)
