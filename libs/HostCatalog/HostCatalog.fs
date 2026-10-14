namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 主机下发的一个模型服务（`catalog.snapshot`）。密钥永不下发，只有 `hasApiKey`。
type ProviderInfo = {
    id: string
    label: string
    kind: string
    baseUrl: string
    hasApiKey: bool
    models: string list
    defaultModel: string
    timeoutSeconds: int
    maxRetries: int
    enabled: bool
    /// 决策 331：用户手动指定的「能否看图」（模型 → 能/不能）
    visionOverrides: Map<string, bool>
    /// 决策 330：模型服务提供的「能否看图」信息（主机探测或已知清单）
    visionDetected: Map<string, bool>
}

/// 一个可用工具。
type ToolInfo = {
    id: string
    label: string
    description: string
    /// "builtin" | "mcp"
    source: string
    serverId: string option
}

/// 一个 MCP 服务配置。
type McpInfo = {
    id: string
    label: string
    command: string option
    args: string list
    url: string option
    callTimeoutSeconds: int
    enabled: bool
}

/// 生成默认值。
type GenerationInfo = {
    temperature: float option
    topP: float option
    maxTokens: int option
    instructions: string option
    maxContextMessages: int
    autoTitle: bool
    maxToolRounds: int
    fileReadRoots: string list
    /// 全局默认模型服务（`[generation] defaultProvider`，可选）
    defaultProvider: string option
    /// 全局默认模型（`[generation] defaultModel`，可选）
    defaultModel: string option
    /// 未单独配置的工具所用审批策略："auto" | "first" | "always"
    toolPolicyDefault: string
    /// 单个工具的审批策略（键是稳定标识）
    toolPolicy: Map<string, string>
    approvalTimeoutSeconds: int
    exposeDebug: bool
}

/// 对话预设（决策 431）：主机配置里的可复用起点。provider/model 为空表示跟随默认。
type PresetInfo = {
    id: string
    name: string
    provider: string
    model: string
    instructions: string option
    tools: string list
    temperature: float option
    topP: float option
    maxTokens: int option
    thinkingBudget: int option
}

/// 主机能力目录。客户端所有选择器（模型、工具）都从这里取数据，
/// 用户永远不需要手打模型服务 id 或模型名。
type Catalog = {
    providers: ProviderInfo list
    tools: ToolInfo list
    mcpServers: McpInfo list
    generation: GenerationInfo
    /// 决策 432：随目录快照下发；旧主机没有这一项 = 空列表。
    presets: PresetInfo list
}

module Catalog =

    let empty =
        { providers = []
          tools = []
          mcpServers = []
          presets = []
          generation =
            { temperature = None
              topP = None
              maxTokens = None
              instructions = None
              maxContextMessages = 200
              autoTitle = true
              maxToolRounds = 12
              fileReadRoots = []
              defaultProvider = None
              defaultModel = None
              toolPolicyDefault = "auto"
              toolPolicy = Map.empty
              approvalTimeoutSeconds = 300
              exposeDebug = false } }

    let private str (o: JsonObject) key fallback =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.String then
            n.GetValue<string>()
        else
            fallback

    let private strOpt (o: JsonObject) key =
        let value = str o key ""
        if String.IsNullOrWhiteSpace value then None else Some value

    let private intOf (o: JsonObject) key fallback =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
            n.GetValue<int>()
        else
            fallback

    let private intOpt (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
            Some(n.GetValue<int>())
        else
            None

    let private floatOpt (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
            Some(n.GetValue<double>())
        else
            None

    let private boolOf (o: JsonObject) key fallback =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) then
            match n.GetValueKind() with
            | JsonValueKind.True -> true
            | JsonValueKind.False -> false
            | _ -> fallback
        else
            fallback

    let private strings (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Array then
            [ for item in n.AsArray() do
                  if not (isNull item) && item.GetValueKind() = JsonValueKind.String then item.GetValue<string>() ]
        else
            []

    let private boolMap (o: JsonObject) (key: string) : Map<string, bool> =
        match o[key] with
        | :? JsonObject as m ->
            [ for kv in m do
                  if not (isNull kv.Value) then
                      match kv.Value.GetValueKind() with
                      | JsonValueKind.True -> yield kv.Key, true
                      | JsonValueKind.False -> yield kv.Key, false
                      | _ -> () ]
            |> Map.ofList
        | _ -> Map.empty

    let private providerOf (o: JsonObject) : ProviderInfo =
        { id = str o "id" ""
          label = (let l = str o "label" "" in if String.IsNullOrWhiteSpace l then str o "id" "" else l)
          kind = str o "kind" "openai"
          baseUrl = str o "baseUrl" ""
          hasApiKey = boolOf o "hasApiKey" false
          models = strings o "models"
          defaultModel = str o "defaultModel" ""
          timeoutSeconds = intOf o "timeoutSeconds" 120
          maxRetries = intOf o "maxRetries" 2
          enabled = boolOf o "enabled" true
          visionOverrides = boolMap o "visionOverrides"
          visionDetected = boolMap o "visionDetected" }

    let private toolOf (o: JsonObject) : ToolInfo =
        { id = str o "id" ""
          label = (let l = str o "label" "" in if String.IsNullOrWhiteSpace l then str o "id" "" else l)
          description = str o "description" ""
          source = str o "source" "builtin"
          serverId = strOpt o "serverId" }

    let private mcpOf (o: JsonObject) : McpInfo =
        { id = str o "id" ""
          label = (let l = str o "label" "" in if String.IsNullOrWhiteSpace l then str o "id" "" else l)
          command = strOpt o "command"
          args = strings o "args"
          url = strOpt o "url"
          callTimeoutSeconds = intOf o "callTimeoutSeconds" 60
          enabled = boolOf o "enabled" true }

    let private objects (array: JsonArray) =
        [ for item in array do
              match item with
              | :? JsonObject as o -> o
              | _ -> () ]

    let parse (providers: JsonArray) (tools: JsonArray) (generation: JsonObject) : Catalog =
        let mcpArray =
            let mutable n: JsonNode = null
            if generation.TryGetPropertyValue("mcpServers", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Array then
                n.AsArray()
            else
                JsonArray()
        let toolsConfig =
            let mutable n: JsonNode = null
            if generation.TryGetPropertyValue("tools", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Object then
                n.AsObject()
            else
                JsonObject()
        { providers = objects providers |> List.map providerOf |> List.filter (fun p -> p.id <> "")
          tools = objects tools |> List.map toolOf |> List.filter (fun t -> t.id <> "")
          mcpServers = objects mcpArray |> List.map mcpOf |> List.filter (fun m -> m.id <> "")
          presets =
            (let mutable n: JsonNode = null
             if generation.TryGetPropertyValue("presets", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Array then
                 objects (n.AsArray())
                 |> List.map (fun o ->
                     { id = str o "id" ""
                       name = str o "name" ""
                       provider = str o "provider" ""
                       model = str o "model" ""
                       instructions = strOpt o "instructions"
                       tools = strings o "tools"
                       temperature = floatOpt o "temperature"
                       topP = floatOpt o "topP"
                       maxTokens = intOpt o "maxTokens"
                       thinkingBudget = intOpt o "thinkingBudget" })
                 |> List.filter (fun p -> p.id <> "" && p.name <> "")
             else [])
          generation =
            { temperature = floatOpt generation "temperature"
              topP = floatOpt generation "topP"
              maxTokens = intOpt generation "maxTokens"
              instructions = strOpt generation "instructions"
              maxContextMessages = intOf generation "maxContextMessages" 200
              autoTitle = boolOf generation "autoTitle" true
              maxToolRounds = intOf generation "maxToolRounds" 12
              fileReadRoots = strings toolsConfig "fileReadRoots"
              defaultProvider = strOpt generation "defaultProvider"
              defaultModel = strOpt generation "defaultModel"
              toolPolicyDefault = str toolsConfig "policyDefault" "auto"
              toolPolicy =
                (let mutable n: JsonNode = null
                 if toolsConfig.TryGetPropertyValue("policy", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Object then
                     n.AsObject()
                     |> Seq.choose (fun kv ->
                         if not (isNull kv.Value) && kv.Value.GetValueKind() = JsonValueKind.String then Some(kv.Key, kv.Value.GetValue<string>())
                         else None)
                     |> Map.ofSeq
                 else Map.empty)
              approvalTimeoutSeconds = intOf toolsConfig "approvalTimeoutSeconds" 300
              exposeDebug = boolOf toolsConfig "exposeDebug" false } }

    /// 参与模型选择的模型服务：启用且至少一个模型。
    let usableProviders (catalog: Catalog) =
        catalog.providers |> List.filter (fun p -> p.enabled && not (List.isEmpty p.models))

    let isReady (catalog: Catalog) = not (List.isEmpty (usableProviders catalog))

    let tryProvider (id: string) (catalog: Catalog) =
        catalog.providers |> List.tryFind (fun p -> p.id = id)

    /// 展示名：「模型服务 · 模型」。找不到模型服务时退化成裸模型名。
    let describeModel (providerId: string) (model: string) (catalog: Catalog) =
        match tryProvider providerId catalog with
        | Some p when not (String.IsNullOrWhiteSpace model) -> sprintf "%s · %s" p.label model
        | Some p -> p.label
        | None when String.IsNullOrWhiteSpace model -> "未选择模型"
        | None -> model

    /// 新对话的默认 (模型服务 id, 模型)：
    /// 全局默认存在且可用 → 它（模型不在列表中则回落该服务的 defaultModel）；
    /// 否则首个可用的模型服务及其默认模型。与主机 `AppConfig.defaultSelection` 同一规则。
    let defaultSelection (catalog: Catalog) =
        let usable = usableProviders catalog
        let modelOf (p: ProviderInfo) (requested: string) =
            if not (String.IsNullOrWhiteSpace requested) && List.contains requested p.models then requested
            elif not (String.IsNullOrWhiteSpace p.defaultModel) then p.defaultModel
            else p.models |> List.tryHead |> Option.defaultValue ""
        let chosen =
            catalog.generation.defaultProvider
            |> Option.bind (fun id -> usable |> List.tryFind (fun p -> p.id = id))
        match chosen with
        | Some p -> Some(p.id, modelOf p (catalog.generation.defaultModel |> Option.defaultValue ""))
        | None -> usable |> List.tryHead |> Option.map (fun p -> p.id, modelOf p "")

    /// 该 (服务, 模型) 是否正是全局默认（设置页行内标记用）。
    let isGlobalDefault (providerId: string) (catalog: Catalog) =
        match defaultSelection catalog with
        | Some(id, _) -> id = providerId && catalog.generation.defaultProvider = Some providerId
        | None -> false
