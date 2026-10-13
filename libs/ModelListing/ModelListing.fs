namespace Wanxiang.Server

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 读模型服务的清单响应：模型名（OpenAI/Anthropic 的 data[].id、Gemini 的 models[].name）、
/// 各家能力字段里的「能否看图」、Ollama 的 /api/show 与 /api/version，以及 Anthropic 的已知清单。
/// 只依赖 BCL；不联网、不缓存。从旧 ProviderCatalog / ModelCapabilities 拆出，行为不变。
module ModelListing =

    /// Anthropic 原生接口不返回能力字段；Claude 3 起全系能看图，更早的只吃文字。
    let anthropicKnown (model: string) : bool option =
        let id = if isNull model then "" else model.Trim().ToLowerInvariant()
        if id.StartsWith "claude-3" || id.StartsWith "claude-sonnet-4" || id.StartsWith "claude-opus-4"
           || id.StartsWith "claude-haiku-4" || id.StartsWith "claude-4" || id.StartsWith "claude-sonnet-5"
           || id.StartsWith "claude-opus-5" then Some true
        elif id.StartsWith "claude-2" || id.StartsWith "claude-instant" then Some false
        else None

    let private strings (n: JsonNode) =
        match n with
        | :? JsonArray as a ->
            [ for x in a do
                  if not (isNull x) && x.GetValueKind() = JsonValueKind.String then yield x.GetValue<string>().ToLowerInvariant() ]
        | _ -> []

    /// 从 `/models` 的单个条目读能力字段；没有任何能力字段时返回 None（交给名字推断）。
    let ofModelEntry (o: JsonObject) : bool option =
        let field (name: string) = match o[name] with null -> None | v -> Some v
        let fromCaps =
            match field "capabilities" with
            | Some(:? JsonArray as a) ->
                let caps = strings a
                Some(caps |> List.exists (fun c -> c = "vision" || c = "image" || c = "image_input" || c = "images"))
            | Some(:? JsonObject as c) ->
                match c["vision"] with
                | null -> None
                | v when v.GetValueKind() = JsonValueKind.True -> Some true
                | v when v.GetValueKind() = JsonValueKind.False -> Some false
                | _ -> None
            | _ -> None
        let fromModalities (n: JsonNode) =
            match n with
            | :? JsonArray as a -> Some(strings a |> List.contains "image")
            | _ -> None
        let fromArchitecture =
            match field "architecture" with
            | Some(:? JsonObject as a) ->
                match a["input_modalities"] with
                | null ->
                    match a["modality"] with
                    | null -> None
                    | m when m.GetValueKind() = JsonValueKind.String ->
                        let input = m.GetValue<string>().Split("->").[0].ToLowerInvariant()
                        Some(input.Contains "image")
                    | _ -> None
                | m -> fromModalities m
            | _ -> None
        fromCaps
        |> Option.orElse fromArchitecture
        |> Option.orElse (field "input_modalities" |> Option.bind fromModalities)
        |> Option.orElse (field "modalities" |> Option.bind fromModalities)

    /// 整份 `/models` 响应 → (模型, 能否看图)，只列出带能力字段的条目。
    let ofModelsBody (body: string) : (string * bool) list =
        try
            match JsonNode.Parse body with
            | :? JsonObject as root ->
                match root["data"] with
                | :? JsonArray as items ->
                    [ for item in items do
                          match item with
                          | :? JsonObject as o ->
                              match o["id"] with
                              | null -> ()
                              | id when id.GetValueKind() = JsonValueKind.String ->
                                  match ofModelEntry o with
                                  | Some v -> yield id.GetValue<string>(), v
                                  | None -> ()
                              | _ -> ()
                          | _ -> () ]
                | _ -> []
            | _ -> []
        with _ -> []

    /// Ollama `/api/show` 响应 → 能否看图（新版 Ollama 才有 capabilities）。
    let ofOllamaShow (body: string) : bool option =
        try
            match JsonNode.Parse body with
            | :? JsonObject as root ->
                match root["capabilities"] with
                | :? JsonArray as a -> Some(strings a |> List.contains "vision")
                | _ -> None
            | _ -> None
        with _ -> None

    /// `/api/version` 响应是否是 Ollama 的（`{"version":"0.x.y"}`）。
    let ofOllamaVersion (body: string) : bool =
        try
            match JsonNode.Parse body with
            | :? JsonObject as root ->
                match root["version"] with
                | null -> false
                | v -> v.GetValueKind() = JsonValueKind.String && root.Count <= 3
            | _ -> false
        with _ -> false

    let private stringField (name: string) (item: JsonNode) =
        match item with
        | :? JsonObject as o ->
            match o[name] with
            | null -> None
            | value ->
                if value.GetValueKind() = JsonValueKind.String then Some(value.GetValue<string>()) else None
        | _ -> None

    let private arrayField (root: JsonObject) (key: string) =
        match root[key] with
        | :? JsonArray as items -> List.ofSeq (Seq.cast<JsonNode> items)
        | _ -> []

    /// 从探活响应里取模型名。
    ///
    /// OpenAI 与 Anthropic 都返回 `{data:[{id}]}`；Gemini 返回
    /// `{models:[{name:"models/gemini-2.5-pro"}]}`，那层 `models/` 前缀要去掉，
    /// 否则填回模型列表后每个模型名都是错的。
    let modelIdsFromProbe (body: string) : Result<string list, string> =
        try
            match JsonNode.Parse body with
            | :? JsonObject as root ->
                let fromData = arrayField root "data" |> List.choose (stringField "id")
                let fromModels =
                    arrayField root "models"
                    |> List.choose (stringField "name")
                    |> List.map (fun name ->
                        if name.StartsWith("models/", StringComparison.Ordinal) then name.Substring 7 else name)
                match fromData @ fromModels with
                | [] -> Error "响应里没有模型清单（既无 data 也无 models）"
                | ids -> Ok(ids |> List.distinct |> List.sort)
            | _ -> Error "响应不是 JSON 对象"
        with _ -> Error "响应不是合法 JSON"
