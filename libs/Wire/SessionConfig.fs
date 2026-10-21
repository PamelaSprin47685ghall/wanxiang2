namespace Wanxiang.Core

open System
open System.Text.Json.Nodes

/// 会话级配置快照：创建/修改会话时整体固化，不记录 patch。
type SessionConfig =
    {
        /// Provider 稳定标识（TOML `[providers.<id>]` 的 id）
        provider: string
        /// 模型名称。必须是该 provider `models` 列表中的一项；
        /// 越界或留空时服务端回落到 provider 的 defaultModel。
        model: string
        /// 系统指令
        instructions: string option
        /// 启用的工具稳定标识（builtin:... / mcp:<id>/<name>）
        tools: string list
        /// 生成参数（可选，透传）
        temperature: float option
        topP: float option
        maxTokens: int option
        /// 思维链预算（token）。None = 跟随 [generation] 默认值，0 = 本会话不开。
        thinkingBudget: int option
        /// 其余生成参数，原样透传给 Provider（reasoning_effort 等模型专属项）
        extraJson: JsonNode option
    }

module SessionConfig =

    let empty: SessionConfig =
        {
            provider = ""
            model = ""
            instructions = None
            tools = []
            temperature = None
            topP = None
            maxTokens = None
            thinkingBudget = None
            extraJson = None
        }

    /// 逐字段校验，返回全部问题（供 UI 精确指出哪一项不合法）。
    let validate (cfg: SessionConfig) : string list =
        [
            if String.IsNullOrWhiteSpace cfg.provider then
                "provider: 必填"
            if String.IsNullOrWhiteSpace cfg.model then
                "model: 必填"
            match cfg.temperature with
            | Some t when t < 0.0 || t > 2.0 -> "temperature: 需在 0–2 之间"
            | _ -> ()
            match cfg.topP with
            | Some p when p <= 0.0 || p > 1.0 -> "topP: 需在 0–1 之间"
            | _ -> ()
            match cfg.maxTokens with
            | Some m when m <= 0 -> "maxTokens: 需为正整数"
            | _ -> ()
        ]

    let isValid (cfg: SessionConfig) : bool = validate cfg |> List.isEmpty

    /// SessionConfig 的 JSON 形状（线上与日志共用，字段名不可改）。
    let toJson (cfg: SessionConfig) : JsonObject =
        let o = JsonObject()
        o["provider"] <- cfg.provider
        o["model"] <- cfg.model

        match cfg.instructions with
        | Some s -> o["instructions"] <- s
        | None -> ()

        if not (List.isEmpty cfg.tools) then
            o["tools"] <- JsonArray([| for t in cfg.tools -> JsonNode.op_Implicit t |])

        match cfg.temperature with
        | Some t -> o["temperature"] <- t
        | None -> ()

        match cfg.topP with
        | Some p -> o["topP"] <- p
        | None -> ()

        match cfg.maxTokens with
        | Some m -> o["maxTokens"] <- m
        | None -> ()

        match cfg.thinkingBudget with
        | Some b -> o["thinkingBudget"] <- b
        | None -> ()

        match cfg.extraJson with
        | Some n -> o["extra"] <- n.DeepClone()
        | None -> ()

        o

    /// 宽松读取：缺失或类型不对的字段取默认值（与旧 CommitCodec.configFromJson 相同）。
    let fromJson (o: JsonObject) : SessionConfig =
        let get (k: string) : JsonNode option =
            let mutable v: JsonNode = null

            if o.TryGetPropertyValue(k, &v) && not (isNull v) then
                Some v
            else
                None

        let getStr k =
            match get k with
            | Some v when v.GetValueKind() = Text.Json.JsonValueKind.String -> Some(v.GetValue<string>())
            | _ -> None

        let getFloat k =
            match get k with
            | Some(:? JsonValue as jv) ->
                (match jv.TryGetValue<float>() with
                 | true, d -> Some d
                 | _ -> None)
            | _ -> None

        let getInt k =
            match get k with
            | Some(:? JsonValue as jv) ->
                (match jv.TryGetValue<int>() with
                 | true, i -> Some i
                 | _ -> None)
            | _ -> None

        let tools =
            match get "tools" with
            | Some v when v.GetValueKind() = Text.Json.JsonValueKind.Array ->
                [
                    for item in v.AsArray() do
                        if not (isNull item) && item.GetValueKind() = Text.Json.JsonValueKind.String then
                            item.GetValue<string>()
                ]
            | _ -> []

        {
            provider = getStr "provider" |> Option.defaultValue ""
            model = getStr "model" |> Option.defaultValue ""
            instructions = getStr "instructions"
            tools = tools
            temperature = getFloat "temperature"
            topP = getFloat "topP"
            maxTokens = getInt "maxTokens"
            thinkingBudget = getInt "thinkingBudget"
            extraJson = get "extra" |> Option.map _.DeepClone()
        }
