namespace Ledger

open System
open System.Globalization
open System.Text.Json.Nodes

/// Checkpoint 的 JSON 形状（版本 1）：
/// {"v":1,"throughId":"3","origin":0,"ends":[10,20,30],"stamps":{"cmd":{"hash":"..","id":"2"}},"state":<调用方的 JSON>}
/// 与 Envelope 同理：只处理 Checkpoint<JsonNode>；领域状态与 JSON 的互转由调用方配合 Checkpoint.map 完成。
[<RequireQualifiedAccess>]
module CheckpointJson =
    [<Literal>]
    let Version = 1

    /// 偏移用 JSON 数字：2^53 字节（8 PiB）以内精确，足够任何单个日志。
    [<Literal>]
    let private MaxExactOffset = 9007199254740992L

    let write (cp: Checkpoint<JsonNode>) : string =
        let o = JsonObject()
        o["v"] <- JsonValue.Create Version
        o["throughId"] <- JsonValue.Create(cp.throughId.ToString CultureInfo.InvariantCulture)
        o["origin"] <- JsonValue.Create cp.origin
        let ends = JsonArray()

        for e in cp.ends do
            ends.Add(JsonValue.Create e)

        o["ends"] <- ends
        let st = JsonObject()

        for KeyValue(cmd, (hash, id)) in cp.stamps do
            let x = JsonObject()
            x["hash"] <- JsonValue.Create hash
            x["id"] <- JsonValue.Create(id.ToString CultureInfo.InvariantCulture)
            st[cmd] <- x

        o["stamps"] <- st

        (match cp.maxStamps with
         | Some n -> o["maxStamps"] <- JsonValue.Create n
         | None -> ())

        o["state"] <-
            (match cp.state with
             | null -> null
             | s -> s.DeepClone())

        o.ToJsonString()

    let read (text: string) : Result<Checkpoint<JsonNode>, string> =
        try
            match JsonNode.Parse text with
            | :? JsonObject as o ->
                let v = o["v"].GetValue<int>()

                if v <> Version then
                    Error(sprintf "unsupported checkpoint version %d" v)
                else
                    let ends = o["ends"].AsArray() |> Seq.map _.GetValue<int64>() |> Array.ofSeq
                    let origin = o["origin"].GetValue<int64>()

                    if origin < 0L || ends |> Array.exists (fun e -> e > MaxExactOffset) then
                        Error "offset out of range"
                    else
                        let stamps =
                            o["stamps"].AsObject()
                            |> Seq.map (fun kv ->
                                let x = kv.Value.AsObject()

                                kv.Key,
                                (x["hash"].GetValue<string>(),
                                 UInt64.Parse(x["id"].GetValue<string>(), CultureInfo.InvariantCulture)))
                            |> Map.ofSeq

                        let maxStamps =
                            match o["maxStamps"] with
                            | null -> None
                            | n -> Some(n.GetValue<int>())

                        Ok
                            {
                                state =
                                    (match o["state"] with
                                     | null -> null
                                     | s -> s.DeepClone())
                                throughId =
                                    UInt64.Parse(o["throughId"].GetValue<string>(), CultureInfo.InvariantCulture)
                                stamps = stamps
                                origin = origin
                                ends = ends
                                maxStamps = maxStamps
                            }
            | _ -> Error "checkpoint is not a JSON object"
        with e ->
            Error e.Message
