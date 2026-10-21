namespace Wanxiang.Core

open System
open System.Globalization
open System.Text.Json
open System.Text.Json.Nodes

/// 事件与提交的 JSON 编解码（NDJSON 行 <-> Commit）。
module CommitCodec =

    let private tryGet (o: JsonObject) (key: string) : JsonNode option =
        let mutable n: JsonNode = null

        if o.TryGetPropertyValue(key, &n) then
            if isNull n then None else Some n
        else
            None

    let private tryGetInt (v: JsonNode) : int option =
        match v with
        | :? JsonValue as jv ->
            match jv.TryGetValue<int>() with
            | true, i -> Some i
            | _ -> None
        | _ -> None

    let private tryString (o: JsonObject) (k: string) : string option =
        match tryGet o k with
        | Some v when v.GetValueKind() = JsonValueKind.String -> Some(v.GetValue<string>())
        | _ -> None

    /// SessionConfig 的 JSON 形状，权威实现在 Wire 库。
    let configToJson (cfg: SessionConfig) : JsonObject = SessionConfig.toJson cfg
    let configFromJson (o: JsonObject) : SessionConfig = SessionConfig.fromJson o

    /// 事件的 JSON 形状，权威实现在 Conversations 库。
    let eventDataToJson (ev: EventData) : JsonObject =
        EventJson.toJson SessionConfig.toJson ev

    let tryEventFromJson (o: JsonObject) : EventData option =
        EventJson.tryFromJson SessionConfig.fromJson o

    /// 将 Commit 序列化为一条 NDJSON 行（不含换行）。
    let commitToJsonLine (commit: Events.Commit) : string =
        let o = JsonObject()
        o["formatVersion"] <- commit.formatVersion
        // 大整数陷阱（RFC 8785 附录 D）：id 与 bootId 都用字符串承载。
        o["id"] <- commit.id.ToString(CultureInfo.InvariantCulture)
        o["bootId"] <- commit.bootId.ToString("D")
        o["source"] <- commit.source

        o["committedAtUtc"] <-
            commit.committedAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

        match commit.commandId with
        | Some c -> o["commandId"] <- c
        | None -> ()

        match commit.commandType with
        | Some t -> o["commandType"] <- t
        | None -> ()

        match commit.commandHash with
        | Some h -> o["commandHash"] <- h
        | None -> ()

        let arr = JsonArray()

        for ev in commit.events do
            arr.Add(eventDataToJson ev)

        o["events"] <- arr
        o.ToJsonString(JsonSerializerOptions(JsonSerializerDefaults.General))

    /// 解析一条 NDJSON 行。结构非法返回 None。
    let tryCommitFromJsonLine (line: string) : Events.Commit option =
        try
            let node = JsonNode.Parse line

            match node with
            | :? JsonObject as o ->
                match tryGet o "formatVersion", tryGet o "id", tryGet o "committedAtUtc", tryGet o "events" with
                | Some fv, Some idNode, Some tsNode, Some evNode when
                    fv.GetValueKind() = JsonValueKind.Number
                    && idNode.GetValueKind() = JsonValueKind.String
                    && tsNode.GetValueKind() = JsonValueKind.String
                    && evNode.GetValueKind() = JsonValueKind.Array
                    ->
                    // id 以字符串承载（RFC 8785 附录 D：超出 double 精度的整数必须字符串）。
                    let idParsed =
                        match idNode with
                        | :? JsonValue as jv ->
                            match jv.TryGetValue<string>() with
                            | true, s ->
                                match UInt64.TryParse s with
                                | true, v -> Some v
                                | _ -> None
                            | _ -> None
                        | _ -> None

                    match tryGetInt fv, idParsed with
                    | Some f, Some id when f = Constants.FormatVersion ->
                        let events =
                            let mutable ok = true
                            let list = System.Collections.Generic.List<EventData>()

                            for e in evNode.AsArray() do
                                if e <> null && e.GetValueKind() = JsonValueKind.Object then
                                    match tryEventFromJson (e.AsObject()) with
                                    | Some ev -> list.Add ev
                                    | None -> ok <- false
                                else
                                    ok <- false

                            if ok then Some(List.ofSeq list) else None

                        match events with
                        | Some evs ->
                            match
                                DateTimeOffset.TryParse(
                                    tsNode.GetValue<string>(),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal
                                )
                            with
                            | true, t ->
                                let commandId = tryString o "commandId"
                                let commandType = tryString o "commandType"
                                let commandHash = tryString o "commandHash"

                                Some
                                    {
                                        formatVersion = f
                                        id = id
                                        bootId =
                                            (match tryString o "bootId" with
                                             | Some b -> Guid.Parse b
                                             | None -> Constants.DefaultBootId)
                                        source = (tryString o "source" |> Option.defaultValue Constants.DefaultSource)
                                        committedAtUtc = t
                                        commandId = commandId
                                        commandType = commandType
                                        commandHash = commandHash
                                        events = evs
                                    }
                            | _ -> None
                        | None -> None
                    | _ -> None
                | _ -> None
            | _ -> None
        with _ ->
            None
