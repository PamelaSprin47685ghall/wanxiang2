namespace Wanxiang.Agent

open System.Text.Json.Nodes

/// 从不可信的 JSON 里按键取值：键缺失、类型不对、值为 null 一律得 None，从不抛异常。
module JsonPick =

    let private property (node: JsonNode) (key: string) : JsonNode option =
        match node with
        | :? JsonObject as o ->
            match o[key] with
            | null -> None
            | value -> Some value
        | _ -> None

    let tryString (node: JsonNode) (key: string) : string option =
        property node key
        |> Option.bind (fun value ->
            try
                if value.GetValueKind() = System.Text.Json.JsonValueKind.String then
                    Some(value.GetValue<string>())
                else
                    None
            with _ ->
                None)

    let tryInt (node: JsonNode) (key: string) : int option =
        property node key
        |> Option.bind (fun value ->
            try
                match value.GetValueKind() with
                | System.Text.Json.JsonValueKind.Number ->
                    let mutable i = 0

                    if value.AsValue().TryGetValue<int>(&i) then
                        Some i
                    else
                        let mutable l = 0L

                        if value.AsValue().TryGetValue<int64>(&l) then
                            Some(int l)
                        else
                            None
                | _ -> None
            with _ ->
                None)

    let tryBool (node: JsonNode) (key: string) : bool option =
        property node key
        |> Option.bind (fun value ->
            try
                Some(value.GetValue<bool>())
            with _ ->
                None)

    let tryObject (node: JsonNode) (key: string) : JsonNode option =
        match property node key with
        | Some(:? JsonObject as o) -> Some(o :> JsonNode)
        | _ -> None

    let tryArray (node: JsonNode) (key: string) : JsonArray option =
        match property node key with
        | Some(:? JsonArray as a) -> Some a
        | _ -> None

    /// JSON Schema 从 AITool 上取下来时是 `JsonElement`；两边协议都要它。
    let schemaOf (schema: System.Text.Json.JsonElement) : JsonNode option =
        try
            match JsonNode.Parse(schema.GetRawText()) with
            | null -> None
            | node -> Some node
        with _ ->
            None
