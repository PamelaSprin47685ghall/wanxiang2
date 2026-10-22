namespace Wanxiang.Config

open System
open System.Globalization
open System.IO
open System.Text.Json.Nodes
open Tomlyn
open Tomlyn.Model

/// TOML 编解码：AppConfig <-> TOML 文本。
/// 读取时未知字段拒绝整份配置（决策 183）；写回时按当前模型完整重写（决策 42）。
module TomlCodec =

    let private knownTopKeysV1 =
        set
            [
                "configVersion"
                "instanceId"
                "runtime"
                "network"
                "pairing"
                "providers"
                "mcp"
                "auth"
            ]

    let private knownNetworkKeysV1 =
        set [ "listen"; "maxAttachmentBytes"; "chunkSizeBytes" ]

    let private knownPairingKeysV1 =
        set [ "failureWindowMinutes"; "maxFailures"; "freezeMinutes" ]

    let private knownProviderKeysV1 =
        set [ "kind"; "baseUrl"; "apiKey"; "model"; "extra" ]

    let private knownMcpKeysV1 =
        set [ "command"; "args"; "env"; "url"; "maxConcurrency" ]

    let private knownTopKeys =
        set
            [
                "configVersion"
                "instanceId"
                "runtime"
                "network"
                "pairing"
                "generation"
                "tools"
                "providers"
                "mcp"
                "auth"
                "presets"
            ]

    let private knownPresetKeys =
        set
            [
                "name"
                "provider"
                "model"
                "instructions"
                "tools"
                "temperature"
                "topP"
                "maxTokens"
                "thinkingBudget"
            ]

    let private knownRuntimeKeys = set [ "server"; "client"; "pwa"; "fix" ]

    let private knownNetworkKeys =
        set
            [
                "listen"
                "tlsCertPath"
                "tlsKeyPath"
                "maxAttachmentBytes"
                "chunkSizeBytes"
            ]

    let private knownPairingKeys =
        set [ "failureWindowMinutes"; "maxFailures"; "freezeMinutes" ]

    let private knownGenerationKeys =
        set
            [
                "temperature"
                "topP"
                "maxTokens"
                "instructions"
                "maxContextMessages"
                "maxContextTokens"
                "autoTitle"
                "maxToolRounds"
                "thinkingBudget"
                "defaultProvider"
                "defaultModel"
            ]

    let private knownToolsKeys =
        set
            [
                "approvalTimeoutSeconds"
                "callTimeoutSeconds"
                "exposeDebug"
                "fileReadRoots"
                "policy"
            ]

    let private knownProviderKeys =
        set
            [
                "kind"
                "label"
                "baseUrl"
                "apiKey"
                "models"
                "defaultModel"
                "timeoutSeconds"
                "maxRetries"
                "enabled"
                "headers"
                "extra"
                "promptCaching"
                "visionModels"
                "textOnlyModels"
            ]

    let private knownMcpKeys =
        set
            [
                "label"
                "command"
                "args"
                "env"
                "url"
                "maxConcurrency"
                "callTimeoutSeconds"
                "enabled"
            ]

    let private knownClientKeys =
        set [ "tokenHash"; "name"; "createdAt"; "lastSeen"; "revoked" ]

    /// 支持的 provider 传输类型。扩展原生协议时在此登记。
    let supportedProviderKinds = set [ "openai"; "anthropic"; "gemini" ]

    let private asTable (v: obj) : TomlTable option =
        match v with
        | :? TomlTable as t -> Some t
        | _ -> None

    let private asString (v: obj) : string option =
        match v with
        | :? string as s -> Some s
        | _ -> None

    let private asBool (v: obj) : bool option =
        match v with
        | :? bool as b -> Some b
        | _ -> None

    /// 精确取整数，不做 int32 截断：范围校验必须看到原值，
    /// 截断后 4294967297 会变成 1，越界检查就放过了。
    let private asInt64Exact (v: obj) : int64 option =
        match v with
        | :? int64 as i -> Some i
        | :? int as i -> Some(int64 i)
        | _ -> None

    /// 取 int32 字段。写进来的整数越出 int32 就记错并返回 `None`，
    /// 不截断成「看起来合法」的值：`4294967297` 截断后是 1，
    /// 会让 `timeoutSeconds` 静默变成 1 秒、`maxRetries` 静默变成 1 次。
    let private getInt32 (errors: ResizeArray<string>) (scope: string) (t: TomlTable) (k: string) : int option =
        match t.TryGetValue k with
        | true, b ->
            match asInt64Exact b with
            | Some n when n >= int64 Int32.MinValue && n <= int64 Int32.MaxValue -> Some(int n)
            | _ ->
                errors.Add $"{scope}.{k}: must be a 32-bit integer"
                None
        | _ -> None

    /// 客户端上传附件的固定块大小（`libs/ConfigFeedback` 的 `UploadFacts.chunkSize`）。
    /// 这里写的是同一个数值而不是引用：库之间不互相依赖（根 LEMMA 5）。
    /// 改动时必须两边一起改，否则上传会被主机拒掉。
    let private clientChunkBytes = 256 * 1024

    /// 分块大小的上限。主机下载附件时按这个值 `Array.zeroCreate<byte>`，
    /// 没有上界就等于一份配置能让主机单次分配任意大的缓冲。
    /// 客户端实际发的是 256 KiB（`clientChunkBytes`），留 4 倍余量足够。
    let private maxChunkBytes = 1024 * 1024

    let private asFloat (v: obj) : float option =
        match v with
        | :? float as f -> Some f
        | :? int64 as i -> Some(float i)
        | _ -> None

    let private asGuid (v: obj) : Guid option =
        match asString v with
        | Some s ->
            match Guid.TryParse s with
            | true, g -> Some g
            | _ -> None
        | None -> None

    let private asUtc (v: obj) : DateTimeOffset option =
        match asString v with
        | Some s ->
            match DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
            | true, d -> Some d
            | _ -> None
        | None -> None

    let private asStringList (v: obj) : string list option =
        match v with
        | :? TomlArray as arr -> Some [ for item in arr -> string item ]
        | _ -> None

    let private asStringMap (v: obj) : Map<string, string> option =
        match asTable v with
        | Some t -> Some(Map.ofSeq [ for kv in t -> kv.Key, string kv.Value ])
        | None -> None

    let rec private tomlNodeToJson (node: obj) : JsonNode =
        match node with
        | :? System.Collections.Generic.IDictionary<string, obj> as t ->
            let o = JsonObject()

            for kv in t do
                o[kv.Key] <- tomlNodeToJson kv.Value

            o :> JsonNode
        | :? System.Collections.Generic.IList<obj> as arr ->
            let a = JsonArray()

            for item in arr do
                a.Add(tomlNodeToJson item)

            a :> JsonNode
        | :? string as s -> JsonValue.Create(s) :> JsonNode
        | :? int64 as i -> JsonValue.Create(i) :> JsonNode
        | :? int as i -> JsonValue.Create(i) :> JsonNode
        | :? float as f -> JsonValue.Create(f) :> JsonNode
        | :? bool as b -> JsonValue.Create(b) :> JsonNode
        | null -> null
        | other -> JsonValue.Create(string other) :> JsonNode


    let private checkKeys
        (table: System.Collections.Generic.IDictionary<string, obj>)
        (known: Set<string>)
        (path: string)
        (errors: ResizeArray<string>)
        =
        for key in table.Keys do
            if not (known.Contains key) then
                errors.Add(sprintf "%s.%s: unknown field" path key)


    /// `[tools.policy]`：`default` 是兜底，其余键是工具稳定标识；值只允许 auto / first / always。
    let private parseToolPolicy (t: TomlTable) (errors: ResizeArray<string>) : ToolPolicy * Map<string, ToolPolicy> =
        match t.TryGetValue "policy" with
        | true, v ->
            match asTable v with
            | Some pt ->
                let mutable dflt = Auto
                let mutable map = Map.empty

                for KeyValue(k, value) in pt do
                    match value with
                    | :? string as str ->
                        match ToolPolicy.tryParse str with
                        | Some p -> if k = "default" then dflt <- p else map <- Map.add k p map
                        | None -> errors.Add(sprintf "tools.policy.%s: expected \"auto\", \"first\" or \"always\"" k)
                    | _ -> errors.Add(sprintf "tools.policy.%s: expected string" k)

                dflt, map
            | None ->
                errors.Add "tools.policy: expected table"
                Auto, Map.empty
        | _ -> Auto, Map.empty

    let private parseRuntime
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : RuntimeSwitches =
        match top.TryGetValue "runtime" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownRuntimeKeys "runtime" errors

                let getBool k def =
                    match t.TryGetValue k with
                    | true, b -> asBool b |> Option.defaultValue def
                    | _ -> def

                {
                    server = getBool "server" true
                    client = getBool "client" true
                    pwa = getBool "pwa" true
                    fix = getBool "fix" false
                }
            | None ->
                errors.Add "runtime: expected table"

                {
                    server = true
                    client = true
                    pwa = true
                    fix = false
                }
        | _ ->
            {
                server = true
                client = true
                pwa = true
                fix = false
            }

    let private parsePairing
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : int * int * int =
        match top.TryGetValue "pairing" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownPairingKeys "pairing" errors

                // 省略用默认值；显式写了必须是正整数。
                // 上界取 Int32.MaxValue：三个参数都是 int32，分钟数乘 60 秒后仍远小于 TimeSpan
                // 的上限（约 153.7 亿分钟），主机侧 TimeSpan.FromMinutes 不会溢出，不需要更小的上界。
                // 旧实现把错误类型静默换成默认值、把越界整数截断，
                // 结果是配对限流要么失效、要么在主机侧崩溃，用户看不到原因。
                let getPositive k def =
                    match t.TryGetValue k with
                    | true, b ->
                        match asInt64Exact b with
                        | Some n when n >= 1L && n <= int64 Int32.MaxValue -> int n
                        | _ ->
                            errors.Add $"pairing.{k}: must be a positive integer"
                            def
                    | _ -> def

                getPositive "failureWindowMinutes" 1, getPositive "maxFailures" 5, getPositive "freezeMinutes" 5
            | None ->
                errors.Add "pairing: expected table"
                1, 5, 5
        | _ -> 1, 5, 5

    let private parseAuthClients
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : ClientAuthRecord list =
        match top.TryGetValue "auth" with
        | true, v ->
            match asTable v with
            | Some at ->
                // Q183：auth 表未知字段拒绝整份配置
                checkKeys at (set [ "clients" ]) "auth" errors

                match at.TryGetValue "clients" with
                | true, c ->
                    match c with
                    | :? TomlTableArray as arr ->
                        [
                            for item in arr do
                                checkKeys item knownClientKeys "auth.clients[]" errors

                                let getStr k =
                                    match item.TryGetValue k with
                                    | true, b -> asString b
                                    | _ -> None

                                let getBool k def =
                                    match item.TryGetValue k with
                                    | true, b -> asBool b |> Option.defaultValue def
                                    | _ -> def

                                match getStr "tokenHash" with
                                | Some hash ->
                                    yield
                                        {
                                            tokenHash = hash
                                            name = getStr "name" |> Option.defaultValue ""
                                            createdAtUtc =
                                                match getStr "createdAt" with
                                                | Some s -> asUtc s |> Option.defaultValue DateTimeOffset.UtcNow
                                                | None -> DateTimeOffset.UtcNow
                                            lastSeenUtc =
                                                match getStr "lastSeen" with
                                                | Some s -> asUtc s
                                                | None -> None
                                            revoked = getBool "revoked" false
                                        }
                                | None -> errors.Add "auth.clients[]: missing tokenHash"
                        ]
                    | _ ->
                        errors.Add "auth.clients: expected array of tables"
                        []
                | _ -> []
            | None ->
                errors.Add "auth: expected table"
                []
        | _ -> []

    let private validateAuth (authClients: ClientAuthRecord list) (errors: ResizeArray<string>) =
        for c in authClients do
            if
                c.tokenHash.Length <> 64
                || not (c.tokenHash |> Seq.forall (fun ch -> Uri.IsHexDigit ch))
            then
                errors.Add "auth.clients[]: tokenHash must be lowercase hex sha256"
            elif c.tokenHash <> c.tokenHash.ToLowerInvariant() then
                errors.Add "auth.clients[]: tokenHash must be lowercase"

    let private parseNetworkV1
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : string * int64 * int =
        match top.TryGetValue "network" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownNetworkKeysV1 "network" errors

                let getStr k def =
                    match t.TryGetValue k with
                    | true, b -> asString b |> Option.defaultValue def
                    | _ -> def

                // maxAttachmentBytes 在配置模型里是 int64，必须精确取值：
                // 走 int32 截断后 4294967360（4 GiB + 64）会变成 64，
                // 校验看到 64 > 0 就放行，运维以为给了 4 GiB、主机只收 64 字节。
                let getInt64 k def =
                    match t.TryGetValue k with
                    | true, b -> asInt64Exact b |> Option.defaultValue def
                    | _ -> def

                // chunkSizeBytes 走精确取值：截断后的值可能仍然落在下界之上，
                // 于是越界配置被当成合法配置放过去。
                // 非法值在这里就地报错，不把非法悄悄换成默认值后交给校验层。
                let getChunkSize =
                    match t.TryGetValue "chunkSizeBytes" with
                    | true, b ->
                        match asInt64Exact b with
                        | Some n when n >= 1L && n <= int64 Int32.MaxValue -> int n
                        | _ ->
                            errors.Add "network.chunkSizeBytes: must be positive"
                            256 * 1024
                    | _ -> 256 * 1024

                getStr "listen" "127.0.0.1:8765", getInt64 "maxAttachmentBytes" (64L * 1024L * 1024L), getChunkSize
            | None ->
                errors.Add "network: expected table"
                "127.0.0.1:8765", 64L * 1024L * 1024L, 256 * 1024
        | _ -> "127.0.0.1:8765", 64L * 1024L * 1024L, 256 * 1024

    let private parseProvidersV1
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : Map<string, ProviderConfig> =
        match top.TryGetValue "providers" with
        | true, v ->
            match asTable v with
            | Some t ->
                [
                    for kv in t do
                        match asTable kv.Value with
                        | Some pt ->
                            checkKeys pt knownProviderKeysV1 (sprintf "providers.%s" kv.Key) errors

                            let getStr k =
                                match pt.TryGetValue k with
                                | true, b -> asString b
                                | _ -> None

                            let kind = getStr "kind" |> Option.defaultValue "openai"
                            let baseUrl = getStr "baseUrl" |> Option.defaultValue ""
                            let model = getStr "model" |> Option.defaultValue ""

                            let extra =
                                match pt.TryGetValue "extra" with
                                | true, b -> Some(tomlNodeToJson b)
                                | _ -> None

                            let models =
                                if String.IsNullOrWhiteSpace model then
                                    []
                                else
                                    [ model.Trim() ]

                            yield
                                kv.Key,
                                {
                                    id = kv.Key
                                    kind = kind
                                    label = ""
                                    baseUrl = baseUrl
                                    apiKey = getStr "apiKey"
                                    models = models
                                    defaultModel = model.Trim()
                                    timeoutSeconds = ProviderConfig.defaultTimeoutSeconds
                                    maxRetries = ProviderConfig.defaultMaxRetries
                                    enabled = true
                                    headers = Map.empty
                                    promptCaching = false
                                    extraJson = extra
                                    visionOverrides = Map.empty
                                }
                        | None -> errors.Add(sprintf "providers.%s: expected table" kv.Key)
                ]
                |> Map.ofList
            | None ->
                errors.Add "providers: expected table"
                Map.empty
        | _ -> Map.empty

    let private parseMcpV1
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : Map<string, McpServerConfig> =
        match top.TryGetValue "mcp" with
        | true, v ->
            match asTable v with
            | Some t ->
                [
                    for kv in t do
                        match asTable kv.Value with
                        | Some mt ->
                            checkKeys mt knownMcpKeysV1 (sprintf "mcp.%s" kv.Key) errors

                            let getStr k =
                                match mt.TryGetValue k with
                                | true, b -> asString b
                                | _ -> None

                            let getInt k =
                                getInt32 errors (sprintf "mcp.%s" kv.Key) mt k

                            let args =
                                match mt.TryGetValue "args" with
                                | true, b -> asStringList b |> Option.defaultValue []
                                | _ -> []

                            let env =
                                match mt.TryGetValue "env" with
                                | true, b -> asStringMap b |> Option.defaultValue Map.empty
                                | _ -> Map.empty

                            yield
                                kv.Key,
                                {
                                    id = kv.Key
                                    label = ""
                                    command = getStr "command"
                                    args = args
                                    env = env
                                    url = getStr "url"
                                    maxConcurrency = getInt "maxConcurrency"
                                    callTimeoutSeconds = McpServerConfig.defaultCallTimeoutSeconds
                                    enabled = true
                                }
                        | None -> errors.Add(sprintf "mcp.%s: expected table" kv.Key)
                ]
                |> Map.ofList
            | None ->
                errors.Add "mcp: expected table"
                Map.empty
        | _ -> Map.empty

    let private validateV1
        (instanceId: Guid option)
        (listen: string, maxAtt: int64, chunk: int)
        (providers: Map<string, ProviderConfig>)
        (mcpServers: Map<string, McpServerConfig>)
        (authClients: ClientAuthRecord list)
        (errors: ResizeArray<string>)
        =
        match instanceId with
        | None -> errors.Add "instanceId: required UUID"
        | Some _ -> ()

        if String.IsNullOrWhiteSpace listen then
            errors.Add "network.listen: required"

        if maxAtt <= 0L then
            errors.Add "network.maxAttachmentBytes: must be positive"

        if chunk <= 0 then
            errors.Add "network.chunkSizeBytes: must be positive"
        else if chunk < clientChunkBytes then
            errors.Add $"network.chunkSizeBytes: must be at least {clientChunkBytes} to accept client uploads"
        else if chunk > maxChunkBytes then
            errors.Add $"network.chunkSizeBytes: must be at most {maxChunkBytes} to bound download buffers"

        for kv in providers do
            let p = kv.Value

            if not (supportedProviderKinds.Contains p.kind) then
                errors.Add(sprintf "providers.%s.kind: unsupported kind %s" kv.Key p.kind)

            if String.IsNullOrWhiteSpace p.baseUrl then
                errors.Add(sprintf "providers.%s.baseUrl: required" kv.Key)
            elif not (Uri.IsWellFormedUriString(p.baseUrl, UriKind.Absolute)) then
                errors.Add(sprintf "providers.%s.baseUrl: must be an absolute URL" kv.Key)

            if List.isEmpty p.models then
                errors.Add(sprintf "providers.%s.model: required" kv.Key)

        let ids = providers.Keys |> Set.ofSeq

        for kv in mcpServers do
            if ids.Contains kv.Key then
                errors.Add(sprintf "mcp.%s: id conflicts with provider id" kv.Key)

            let m = kv.Value

            match m.command, m.url with
            | None, None -> errors.Add(sprintf "mcp.%s: either command or url is required" kv.Key)
            | Some _, Some _ -> errors.Add(sprintf "mcp.%s: command and url are mutually exclusive" kv.Key)
            | _ -> ()

            match m.url with
            | Some u when not (Uri.IsWellFormedUriString(u, UriKind.Absolute)) ->
                errors.Add(sprintf "mcp.%s.url: must be an absolute URL" kv.Key)
            | _ -> ()

            match m.maxConcurrency with
            | Some c when c <= 0 -> errors.Add(sprintf "mcp.%s.maxConcurrency: must be positive" kv.Key)
            | _ -> ()

        validateAuth authClients errors

    /// 解析 v1 配置并显式升级到当前 AppConfig 模型（决策 184）。
    let private tryParseV1 (top: System.Collections.Generic.Dictionary<string, obj>) : Result<AppConfig, string list> =
        let errors = ResizeArray<string>()
        checkKeys top knownTopKeysV1 "config" errors

        let instanceId =
            match top.TryGetValue "instanceId" with
            | true, v -> asGuid v
            | _ -> None

        let runtime = parseRuntime top errors
        let network = parseNetworkV1 top errors
        let pairing = parsePairing top errors
        let providers = parseProvidersV1 top errors
        let mcpServers = parseMcpV1 top errors
        let authClients = parseAuthClients top errors

        let listen, maxAtt, chunk = network
        validateV1 instanceId network providers mcpServers authClients errors

        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            Ok
                {
                    configVersion = AppConfig.CurrentVersion
                    instanceId = instanceId.Value
                    runtime = runtime
                    listen = listen
                    tlsCertPath = ""
                    tlsKeyPath = ""
                    maxAttachmentBytes = maxAtt
                    chunkSizeBytes = chunk
                    pairingFailureWindowMinutes = let a, _, _ = pairing in a
                    pairingMaxFailures = let _, b, _ = pairing in b
                    pairingFreezeMinutes = let _, _, c = pairing in c
                    generation = GenerationDefaults.defaults
                    tools = ToolsConfig.defaults
                    providers = providers
                    mcpServers = mcpServers
                    authClients = authClients
                    presets = Map.empty
                }

    let private parseNetwork
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : string * string * string * int64 * int =
        match top.TryGetValue "network" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownNetworkKeys "network" errors

                let getStr k def =
                    match t.TryGetValue k with
                    | true, b -> asString b |> Option.defaultValue def
                    | _ -> def

                // maxAttachmentBytes 在配置模型里是 int64，必须精确取值：
                // 走 int32 截断后 4294967360（4 GiB + 64）会变成 64，
                // 校验看到 64 > 0 就放行，运维以为给了 4 GiB、主机只收 64 字节。
                let getInt64 k def =
                    match t.TryGetValue k with
                    | true, b -> asInt64Exact b |> Option.defaultValue def
                    | _ -> def

                // chunkSizeBytes 走精确取值：截断后的值可能仍然落在下界之上，
                // 于是越界配置被当成合法配置放过去。
                // 非法值在这里就地报错，不把非法悄悄换成默认值后交给校验层：
                // 校验层看到的仍是合法默认值，就不会再报这个错。
                let getChunkSize =
                    match t.TryGetValue "chunkSizeBytes" with
                    | true, b ->
                        match asInt64Exact b with
                        | Some n when n >= 1L && n <= int64 Int32.MaxValue -> int n
                        | _ ->
                            errors.Add "network.chunkSizeBytes: must be positive"
                            256 * 1024
                    | _ -> 256 * 1024

                getStr "listen" "127.0.0.1:8765",
                getStr "tlsCertPath" "",
                getStr "tlsKeyPath" "",
                getInt64 "maxAttachmentBytes" (64L * 1024L * 1024L),
                getChunkSize
            | None ->
                errors.Add "network: expected table"
                "127.0.0.1:8765", "", "", 64L * 1024L * 1024L, 256 * 1024
        | _ -> "127.0.0.1:8765", "", "", 64L * 1024L * 1024L, 256 * 1024

    let private parseGeneration
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : GenerationDefaults =
        match top.TryGetValue "generation" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownGenerationKeys "generation" errors

                let getFloatOpt k =
                    match t.TryGetValue k with
                    | true, b -> asFloat b
                    | _ -> None

                let getIntOpt k = getInt32 errors "generation" t k

                let d = GenerationDefaults.defaults

                {
                    temperature = getFloatOpt "temperature"
                    topP = getFloatOpt "topP"
                    maxTokens = getIntOpt "maxTokens"
                    instructions =
                        match t.TryGetValue "instructions" with
                        | true, b -> asString b |> Option.filter (String.IsNullOrWhiteSpace >> not)
                        | _ -> None
                    maxContextMessages = getIntOpt "maxContextMessages" |> Option.defaultValue d.maxContextMessages
                    maxContextTokens = getIntOpt "maxContextTokens" |> Option.defaultValue d.maxContextTokens
                    autoTitle =
                        (match t.TryGetValue "autoTitle" with
                         | true, b -> asBool b |> Option.defaultValue d.autoTitle
                         | _ -> d.autoTitle)
                    maxToolRounds = getIntOpt "maxToolRounds" |> Option.defaultValue d.maxToolRounds
                    thinkingBudget = getIntOpt "thinkingBudget" |> Option.defaultValue d.thinkingBudget
                    defaultProvider =
                        match t.TryGetValue "defaultProvider" with
                        | true, b -> asString b |> Option.filter (String.IsNullOrWhiteSpace >> not)
                        | _ -> None
                    defaultModel =
                        match t.TryGetValue "defaultModel" with
                        | true, b -> asString b |> Option.filter (String.IsNullOrWhiteSpace >> not)
                        | _ -> None
                }
            | None ->
                errors.Add "generation: expected table"
                GenerationDefaults.defaults
        | _ -> GenerationDefaults.defaults

    let private parseTools
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : ToolsConfig =
        match top.TryGetValue "tools" with
        | true, v ->
            match asTable v with
            | Some t ->
                checkKeys t knownToolsKeys "tools" errors
                let d = ToolsConfig.defaults
                let policyDefault, policy = parseToolPolicy t errors

                {
                    fileReadRoots =
                        (match t.TryGetValue "fileReadRoots" with
                         | true, b -> asStringList b |> Option.defaultValue []
                         | _ -> [])
                    callTimeoutSeconds =
                        getInt32 errors "tools" t "callTimeoutSeconds"
                        |> Option.defaultValue d.callTimeoutSeconds
                    policyDefault = policyDefault
                    policy = policy
                    approvalTimeoutSeconds =
                        getInt32 errors "tools" t "approvalTimeoutSeconds"
                        |> Option.defaultValue d.approvalTimeoutSeconds
                    exposeDebug =
                        (match t.TryGetValue "exposeDebug" with
                         | true, (:? bool as b) -> b
                         | _ -> d.exposeDebug)
                }
            | None ->
                errors.Add "tools: expected table"
                ToolsConfig.defaults
        | _ -> ToolsConfig.defaults

    let private parseProviderEntry (id: string) (pt: TomlTable) (errors: ResizeArray<string>) : ProviderConfig =
        checkKeys pt knownProviderKeys (sprintf "providers.%s" id) errors

        let getStr k =
            match pt.TryGetValue k with
            | true, b -> asString b
            | _ -> None

        let getInt k =
            getInt32 errors (sprintf "providers.%s" id) pt k

        let listOf k =
            match pt.TryGetValue k with
            | true, b ->
                asStringList b
                |> Option.defaultValue []
                |> List.map (fun s -> s.Trim())
                |> List.filter (String.IsNullOrWhiteSpace >> not)
            | _ -> []

        let models = listOf "models" |> List.distinct

        let defaultModel =
            match getStr "defaultModel" with
            | Some m when not (String.IsNullOrWhiteSpace m) -> m.Trim()
            | _ -> models |> List.tryHead |> Option.defaultValue ""

        {
            id = id
            kind = getStr "kind" |> Option.defaultValue "openai"
            label = getStr "label" |> Option.defaultValue ""
            baseUrl = getStr "baseUrl" |> Option.defaultValue ""
            apiKey = getStr "apiKey"
            models = models
            defaultModel = defaultModel
            timeoutSeconds =
                getInt "timeoutSeconds"
                |> Option.defaultValue ProviderConfig.defaultTimeoutSeconds
            maxRetries = getInt "maxRetries" |> Option.defaultValue ProviderConfig.defaultMaxRetries
            enabled =
                match pt.TryGetValue "enabled" with
                | true, b -> asBool b |> Option.defaultValue true
                | _ -> true
            headers =
                match pt.TryGetValue "headers" with
                | true, b -> asStringMap b |> Option.defaultValue Map.empty
                | _ -> Map.empty
            promptCaching =
                match pt.TryGetValue "promptCaching" with
                | true, b -> asBool b |> Option.defaultValue false
                | _ -> false
            extraJson =
                match pt.TryGetValue "extra" with
                | true, b -> Some(tomlNodeToJson b)
                | _ -> None
            // 同一模型两边都写时以「只吃文字」为准（保守，不误发图片）。
            visionOverrides =
                [
                    for m in listOf "visionModels" -> m, true
                    for m in listOf "textOnlyModels" -> m, false
                ]
                |> Map.ofList
        }

    let private parseProviders
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : Map<string, ProviderConfig> =
        match top.TryGetValue "providers" with
        | true, v ->
            match asTable v with
            | Some t ->
                [
                    for kv in t do
                        match asTable kv.Value with
                        | Some pt -> yield kv.Key, parseProviderEntry kv.Key pt errors
                        | None -> errors.Add(sprintf "providers.%s: expected table" kv.Key)
                ]
                |> Map.ofList
            | None ->
                errors.Add "providers: expected table"
                Map.empty
        | _ -> Map.empty

    let private parseMcp
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : Map<string, McpServerConfig> =
        match top.TryGetValue "mcp" with
        | true, v ->
            match asTable v with
            | Some t ->
                [
                    for kv in t do
                        match asTable kv.Value with
                        | Some mt ->
                            checkKeys mt knownMcpKeys (sprintf "mcp.%s" kv.Key) errors

                            let getStr k =
                                match mt.TryGetValue k with
                                | true, b -> asString b
                                | _ -> None

                            let getInt k =
                                getInt32 errors (sprintf "mcp.%s" kv.Key) mt k

                            let args =
                                match mt.TryGetValue "args" with
                                | true, b -> asStringList b |> Option.defaultValue []
                                | _ -> []

                            let env =
                                match mt.TryGetValue "env" with
                                | true, b -> asStringMap b |> Option.defaultValue Map.empty
                                | _ -> Map.empty

                            yield
                                kv.Key,
                                {
                                    id = kv.Key
                                    label = getStr "label" |> Option.defaultValue ""
                                    command = getStr "command"
                                    args = args
                                    env = env
                                    url = getStr "url"
                                    maxConcurrency = getInt "maxConcurrency"
                                    callTimeoutSeconds =
                                        getInt "callTimeoutSeconds"
                                        |> Option.defaultValue McpServerConfig.defaultCallTimeoutSeconds
                                    enabled =
                                        (match mt.TryGetValue "enabled" with
                                         | true, b -> asBool b |> Option.defaultValue true
                                         | _ -> true)
                                }
                        | None -> errors.Add(sprintf "mcp.%s: expected table" kv.Key)
                ]
                |> Map.ofList
            | None ->
                errors.Add "mcp: expected table"
                Map.empty
        | _ -> Map.empty

    /// 决策 431：对话预设。
    let private parsePresets
        (top: System.Collections.Generic.IDictionary<string, obj>)
        (errors: ResizeArray<string>)
        : Map<string, ConversationPreset> =
        match top.TryGetValue "presets" with
        | true, v ->
            match asTable v with
            | Some t ->
                [
                    for kv in t do
                        match asTable kv.Value with
                        | Some pt ->
                            checkKeys pt knownPresetKeys (sprintf "presets.%s" kv.Key) errors

                            let getStr k =
                                match pt.TryGetValue k with
                                | true, b -> asString b |> Option.filter (String.IsNullOrWhiteSpace >> not)
                                | _ -> None

                            let getInt k =
                                getInt32 errors (sprintf "presets.%s" kv.Key) pt k

                            let getFloat k =
                                match pt.TryGetValue k with
                                | true, b -> asFloat b
                                | _ -> None

                            let preset: ConversationPreset =
                                {
                                    id = kv.Key
                                    name = getStr "name" |> Option.defaultValue kv.Key
                                    provider = getStr "provider" |> Option.defaultValue ""
                                    model = getStr "model" |> Option.defaultValue ""
                                    instructions = getStr "instructions"
                                    tools =
                                        match pt.TryGetValue "tools" with
                                        | true, b -> asStringList b |> Option.defaultValue []
                                        | _ -> []
                                    temperature = getFloat "temperature"
                                    topP = getFloat "topP"
                                    maxTokens = getInt "maxTokens"
                                    thinkingBudget = getInt "thinkingBudget"
                                }

                            match preset.temperature with
                            | Some x when x < 0.0 || x > 2.0 ->
                                errors.Add(sprintf "presets.%s.temperature: must be within [0, 2]" kv.Key)
                            | _ -> ()

                            match preset.topP with
                            | Some x when x <= 0.0 || x > 1.0 ->
                                errors.Add(sprintf "presets.%s.topP: must be within (0, 1]" kv.Key)
                            | _ -> ()

                            match preset.maxTokens with
                            | Some m when m <= 0 -> errors.Add(sprintf "presets.%s.maxTokens: must be positive" kv.Key)
                            | _ -> ()

                            yield kv.Key, preset
                        | None -> errors.Add(sprintf "presets.%s: expected table" kv.Key)
                ]
                |> Map.ofList
            | None ->
                errors.Add "presets: expected table"
                Map.empty
        | _ -> Map.empty

    let private validateNetwork
        (listen: string, tlsCert: string, tlsKey: string, maxAtt: int64, chunk: int)
        (errors: ResizeArray<string>)
        =
        if String.IsNullOrWhiteSpace listen then
            errors.Add "network.listen: required"

        if maxAtt <= 0L then
            errors.Add "network.maxAttachmentBytes: must be positive"

        if chunk <= 0 then
            errors.Add "network.chunkSizeBytes: must be positive"
        else if chunk < clientChunkBytes then
            // 客户端上传的块大小是固定的 256 KiB，协议里没有通告字段。
            // 主机把它当上传块上限时，配小了就等于每一次上传都被拒。
            errors.Add $"network.chunkSizeBytes: must be at least {clientChunkBytes} to accept client uploads"
        else if chunk > maxChunkBytes then
            // 下载时按这个值 Array.zeroCreate，没有上界就是让配置决定单次分配多大。
            errors.Add $"network.chunkSizeBytes: must be at most {maxChunkBytes} to bound download buffers"
        // 只填一半是配置事故：会静默退回明文，而运维以为已经加密
        match String.IsNullOrWhiteSpace tlsCert, String.IsNullOrWhiteSpace tlsKey with
        | false, true -> errors.Add "network.tlsKeyPath: required when tlsCertPath is set"
        | true, false -> errors.Add "network.tlsCertPath: required when tlsKeyPath is set"
        | false, false ->
            if not (IO.File.Exists tlsCert) then
                errors.Add $"network.tlsCertPath: file not found: {tlsCert}"

            if not (IO.File.Exists tlsKey) then
                errors.Add $"network.tlsKeyPath: file not found: {tlsKey}"
        | true, true -> ()

    let private validateGeneration (generation: GenerationDefaults) (errors: ResizeArray<string>) =
        match generation.temperature with
        | Some t when t < 0.0 || t > 2.0 -> errors.Add "generation.temperature: must be within [0, 2]"
        | _ -> ()

        match generation.topP with
        | Some p when p <= 0.0 || p > 1.0 -> errors.Add "generation.topP: must be within (0, 1]"
        | _ -> ()

        match generation.maxTokens with
        | Some m when m <= 0 -> errors.Add "generation.maxTokens: must be positive"
        | _ -> ()

        if generation.maxContextMessages < 0 then
            errors.Add "generation.maxContextMessages: must not be negative"

        if generation.maxContextTokens < 0 then
            errors.Add "generation.maxContextTokens: must not be negative"

        if generation.maxToolRounds <= 0 then
            errors.Add "generation.maxToolRounds: must be positive"

        if generation.thinkingBudget < 0 then
            errors.Add "generation.thinkingBudget: must not be negative"

    let private validateTools (toolsCfg: ToolsConfig) (errors: ResizeArray<string>) =
        if toolsCfg.callTimeoutSeconds <= 0 then
            errors.Add "tools.callTimeoutSeconds: must be positive"

        if toolsCfg.approvalTimeoutSeconds <= 0 then
            errors.Add "tools.approvalTimeoutSeconds: must be positive"

        for root in toolsCfg.fileReadRoots do
            if not (Path.IsPathRooted root) then
                errors.Add(sprintf "tools.fileReadRoots: %s must be an absolute path" root)

    let private validateProvidersAndMcp
        (providers: Map<string, ProviderConfig>)
        (mcpServers: Map<string, McpServerConfig>)
        (errors: ResizeArray<string>)
        =
        for kv in providers do
            let p = kv.Value

            if not (supportedProviderKinds.Contains p.kind) then
                errors.Add(sprintf "providers.%s.kind: unsupported kind %s" kv.Key p.kind)

            if String.IsNullOrWhiteSpace p.baseUrl then
                errors.Add(sprintf "providers.%s.baseUrl: required" kv.Key)
            elif not (Uri.IsWellFormedUriString(p.baseUrl, UriKind.Absolute)) then
                errors.Add(sprintf "providers.%s.baseUrl: must be an absolute URL" kv.Key)

            if List.isEmpty p.models then
                errors.Add(sprintf "providers.%s.models: at least one model required" kv.Key)
            elif not (ProviderConfig.hasModel p.defaultModel p) then
                errors.Add(sprintf "providers.%s.defaultModel: %s is not in models" kv.Key p.defaultModel)

            if p.timeoutSeconds <= 0 then
                errors.Add(sprintf "providers.%s.timeoutSeconds: must be positive" kv.Key)

            if p.maxRetries < 0 then
                errors.Add(sprintf "providers.%s.maxRetries: must not be negative" kv.Key)
        // 决策 162：重复/冲突稳定标识在配置加载时直接判为无效
        let ids = providers.Keys |> Set.ofSeq

        for kv in mcpServers do
            if ids.Contains kv.Key then
                errors.Add(sprintf "mcp.%s: id conflicts with provider id" kv.Key)

            let m = kv.Value

            match m.command, m.url with
            | None, None -> errors.Add(sprintf "mcp.%s: either command or url is required" kv.Key)
            | Some _, Some _ -> errors.Add(sprintf "mcp.%s: command and url are mutually exclusive" kv.Key)
            | _ -> ()

            match m.url with
            | Some u when not (Uri.IsWellFormedUriString(u, UriKind.Absolute)) ->
                errors.Add(sprintf "mcp.%s.url: must be an absolute URL" kv.Key)
            | _ -> ()

            if m.callTimeoutSeconds <= 0 then
                errors.Add(sprintf "mcp.%s.callTimeoutSeconds: must be positive" kv.Key)

            match m.maxConcurrency with
            | Some c when c <= 0 -> errors.Add(sprintf "mcp.%s.maxConcurrency: must be positive" kv.Key)
            | _ -> ()

    /// 解析当前版本（v2）配置。
    let private tryParseV2 (top: System.Collections.Generic.Dictionary<string, obj>) : Result<AppConfig, string list> =
        let errors = ResizeArray<string>()
        checkKeys top knownTopKeys "config" errors

        let instanceId =
            match top.TryGetValue "instanceId" with
            | true, v -> asGuid v
            | _ -> None

        let runtime = parseRuntime top errors
        let network = parseNetwork top errors
        let pairing = parsePairing top errors
        let generation = parseGeneration top errors
        let toolsCfg = parseTools top errors
        let providers = parseProviders top errors
        let mcpServers = parseMcp top errors
        let authClients = parseAuthClients top errors
        let presets = parsePresets top errors

        match instanceId with
        | None -> errors.Add "instanceId: required UUID"
        | Some _ -> ()

        validateNetwork network errors
        validateGeneration generation errors
        validateTools toolsCfg errors
        validateProvidersAndMcp providers mcpServers errors
        validateAuth authClients errors
        let listen, tlsCert, tlsKey, maxAtt, chunk = network

        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            Ok
                {
                    configVersion = AppConfig.CurrentVersion
                    instanceId = instanceId.Value
                    runtime = runtime
                    listen = listen
                    tlsCertPath = tlsCert
                    tlsKeyPath = tlsKey
                    maxAttachmentBytes = maxAtt
                    chunkSizeBytes = chunk
                    pairingFailureWindowMinutes = let a, _, _ = pairing in a
                    pairingMaxFailures = let _, b, _ = pairing in b
                    pairingFreezeMinutes = let _, _, c = pairing in c
                    generation = generation
                    tools = toolsCfg
                    providers = providers
                    mcpServers = mcpServers
                    authClients = authClients
                    presets = presets
                }

    /// 解析并校验 TOML 文本。未知字段/类型错误返回错误列表。
    /// 旧版本通过显式升级函数读入（决策 184）。
    let tryParse (text: string) : Result<AppConfig, string list> =
        let parsed =
            try
                let mutable result: System.Collections.Generic.Dictionary<string, obj> = null
                let mutable options = TomlSerializerOptions()

                let ok =
                    TomlSerializer.TryDeserialize<System.Collections.Generic.Dictionary<string, obj>>(
                        text,
                        &result,
                        options
                    )

                if ok && not (isNull result) then
                    Ok result
                else
                    Error [ "config: TOML syntax error" ]
            with e ->
                Error [ sprintf "config: %s" e.Message ]

        match parsed with
        | Error errs -> Error errs
        | Ok top ->
            // configVersion 越出 int32 必须报错：截断后可能落进 1..4，
            // 于是用错版本的解析器读整份配置。
            let configVersion =
                match top.TryGetValue "configVersion" with
                | true, v ->
                    match asInt64Exact v with
                    | Some n when n >= int64 Int32.MinValue && n <= int64 Int32.MaxValue -> Some(int n)
                    | _ -> None
                | _ -> Some 1

            match configVersion with
            | None -> Error [ "configVersion: must be a 32-bit integer" ]
            | Some 1 -> tryParseV1 top
            // 3 = 2 + `[generation] defaultProvider/defaultModel`；读入后统一视为当前结构，
            // 写回时按是否设置了全局默认重新决定 2 或 3（`AppConfig.versionToWrite`）。
            // 4 = 3 + `[presets]`（决策 431），同样读入为当前结构。
            | Some(2 | 3 | 4) -> tryParseV2 top
            | Some v ->
                Error
                    [
                        sprintf "configVersion %d not supported (expected %d)" v AppConfig.CurrentVersion
                    ]

    /// 决策 272：手写的最小 config.toml 缺顶层 `instanceId` 时不再让主机退出，
    /// 而是在文件开头补一行新 UUID（其余内容原样保留）。返回 Some 补好的文本；
    /// 已有 instanceId、或 TOML 本身语法错误（交给 tryParse 报错）时返回 None。
    let ensureInstanceId (text: string) (fresh: Guid) : string option =
        try
            let mutable result: System.Collections.Generic.Dictionary<string, obj> = null
            let mutable options = TomlSerializerOptions()

            let ok =
                TomlSerializer.TryDeserialize<System.Collections.Generic.Dictionary<string, obj>>(
                    text,
                    &result,
                    options
                )

            if ok && not (isNull result) && not (result.ContainsKey "instanceId") then
                Some(sprintf "# 万象自动补上的实例标识（决策 272）\ninstanceId = \"%s\"\n%s" (fresh.ToString "D") text)
            else
                None
        with _ ->
            None

    /// 将配置完整重写为 TOML 文本（决策 42：不保留注释/未知字段/布局）。
    let serialize (cfg: AppConfig) : string =
        let top = TomlTable()
        top.Add("configVersion", int64 (AppConfig.versionToWrite cfg))
        top.Add("instanceId", cfg.instanceId.ToString("D"))

        let runtime = TomlTable()
        runtime.Add("server", cfg.runtime.server)
        runtime.Add("client", cfg.runtime.client)
        runtime.Add("pwa", cfg.runtime.pwa)
        runtime.Add("fix", cfg.runtime.fix)
        top.Add("runtime", runtime)

        let network = TomlTable()
        network.Add("listen", cfg.listen)

        if not (String.IsNullOrWhiteSpace cfg.tlsCertPath) then
            network.Add("tlsCertPath", cfg.tlsCertPath)

        if not (String.IsNullOrWhiteSpace cfg.tlsKeyPath) then
            network.Add("tlsKeyPath", cfg.tlsKeyPath)

        network.Add("maxAttachmentBytes", cfg.maxAttachmentBytes)
        network.Add("chunkSizeBytes", int64 cfg.chunkSizeBytes)
        top.Add("network", network)

        let pairing = TomlTable()
        pairing.Add("failureWindowMinutes", int64 cfg.pairingFailureWindowMinutes)
        pairing.Add("maxFailures", int64 cfg.pairingMaxFailures)
        pairing.Add("freezeMinutes", int64 cfg.pairingFreezeMinutes)
        top.Add("pairing", pairing)

        let generation = TomlTable()

        match cfg.generation.temperature with
        | Some t -> generation.Add("temperature", t)
        | None -> ()

        match cfg.generation.topP with
        | Some p -> generation.Add("topP", p)
        | None -> ()

        match cfg.generation.maxTokens with
        | Some m -> generation.Add("maxTokens", int64 m)
        | None -> ()

        match cfg.generation.instructions with
        | Some s -> generation.Add("instructions", s)
        | None -> ()

        generation.Add("maxContextMessages", int64 cfg.generation.maxContextMessages)
        generation.Add("maxContextTokens", int64 cfg.generation.maxContextTokens)
        generation.Add("autoTitle", cfg.generation.autoTitle)
        generation.Add("maxToolRounds", int64 cfg.generation.maxToolRounds)

        if cfg.generation.thinkingBudget > 0 then
            generation.Add("thinkingBudget", int64 cfg.generation.thinkingBudget)

        match cfg.generation.defaultProvider with
        | Some p -> generation.Add("defaultProvider", p)
        | None -> ()

        match cfg.generation.defaultModel with
        | Some m -> generation.Add("defaultModel", m)
        | None -> ()

        top.Add("generation", generation)

        let toolsTable = TomlTable()

        if not (List.isEmpty cfg.tools.fileReadRoots) then
            let arr = TomlArray()

            for r in cfg.tools.fileReadRoots do
                arr.Add r

            toolsTable.Add("fileReadRoots", arr)

        toolsTable.Add("callTimeoutSeconds", int64 cfg.tools.callTimeoutSeconds)

        if cfg.tools.approvalTimeoutSeconds <> ToolsConfig.defaultApprovalTimeoutSeconds then
            toolsTable.Add("approvalTimeoutSeconds", int64 cfg.tools.approvalTimeoutSeconds)

        if cfg.tools.exposeDebug then
            toolsTable.Add("exposeDebug", true)

        if cfg.tools.policyDefault <> Auto || not (Map.isEmpty cfg.tools.policy) then
            let pt = TomlTable()

            if cfg.tools.policyDefault <> Auto then
                pt.Add("default", ToolPolicy.toToml cfg.tools.policyDefault)

            for (id, p) in cfg.tools.policy |> Map.toList do
                pt.Add(id, ToolPolicy.toToml p)

            toolsTable.Add("policy", pt)

        top.Add("tools", toolsTable)

        let providers = TomlTable()
        // extra 是已识别字段（knownProviderKeys 含 "extra"）；写回必须保留，
        // 否则任何一次 Rewrite（配对落盘、吊销、配置修改）都会静默删除用户配置的 extra 内容（决策 42）
        let rec jsonToToml (node: JsonNode) : obj =
            match node with
            | :? JsonObject as o ->
                let t = TomlTable()

                for kv in o do
                    if not (isNull kv.Value) then
                        t.Add(kv.Key, jsonToToml kv.Value)

                t :> obj
            | :? JsonArray as a ->
                let arr = TomlArray()

                for item in a do
                    if not (isNull item) then
                        arr.Add(jsonToToml item)

                arr :> obj
            | :? JsonValue as v ->
                let mutable s = ""

                if v.TryGetValue<string>(&s) then
                    s :> obj
                else
                    let mutable b = false

                    if v.TryGetValue<bool>(&b) then
                        b :> obj
                    else
                        let mutable i = 0L

                        if v.TryGetValue<int64>(&i) then
                            i :> obj
                        else
                            let mutable f = 0.0

                            if v.TryGetValue<double>(&f) then
                                f :> obj
                            else
                                string v :> obj
            | _ -> string node :> obj

        for kv in cfg.providers do
            let p = TomlTable()
            p.Add("kind", kv.Value.kind)

            if not (String.IsNullOrWhiteSpace kv.Value.label) then
                p.Add("label", kv.Value.label)

            p.Add("baseUrl", kv.Value.baseUrl)

            match kv.Value.apiKey with
            | Some k -> p.Add("apiKey", k)
            | None -> ()

            let models = TomlArray()

            for m in kv.Value.models do
                models.Add m

            p.Add("models", models)
            p.Add("defaultModel", kv.Value.defaultModel)
            p.Add("timeoutSeconds", int64 kv.Value.timeoutSeconds)
            p.Add("maxRetries", int64 kv.Value.maxRetries)
            p.Add("enabled", kv.Value.enabled)

            if kv.Value.promptCaching then
                p.Add("promptCaching", true)

            let overrides = kv.Value.visionOverrides |> Map.toList
            let yes = overrides |> List.filter snd |> List.map fst
            let no = overrides |> List.filter (snd >> not) |> List.map fst

            if not (List.isEmpty yes) then
                let a = TomlArray()

                for m in yes do
                    a.Add m

                p.Add("visionModels", a)

            if not (List.isEmpty no) then
                let a = TomlArray()

                for m in no do
                    a.Add m

                p.Add("textOnlyModels", a)

            if not (Map.isEmpty kv.Value.headers) then
                let h = TomlTable()

                for e in kv.Value.headers do
                    h.Add(e.Key, e.Value)

                p.Add("headers", h)

            match kv.Value.extraJson with
            | Some n when not (isNull n) -> p.Add("extra", jsonToToml n)
            | _ -> ()

            providers.Add(kv.Key, p)

        top.Add("providers", providers)

        let mcp = TomlTable()

        for kv in cfg.mcpServers do
            let m = TomlTable()

            if not (String.IsNullOrWhiteSpace kv.Value.label) then
                m.Add("label", kv.Value.label)

            match kv.Value.command with
            | Some c -> m.Add("command", c)
            | None -> ()

            if not (List.isEmpty kv.Value.args) then
                let arr = TomlArray()

                for a in kv.Value.args do
                    arr.Add a

                m.Add("args", arr)

            if not (kv.Value.env |> Map.isEmpty) then
                let env = TomlTable()

                for e in kv.Value.env do
                    env.Add(e.Key, e.Value)

                m.Add("env", env)

            match kv.Value.url with
            | Some u -> m.Add("url", u)
            | None -> ()

            match kv.Value.maxConcurrency with
            | Some c -> m.Add("maxConcurrency", int64 c)
            | None -> ()

            m.Add("callTimeoutSeconds", int64 kv.Value.callTimeoutSeconds)
            m.Add("enabled", kv.Value.enabled)
            mcp.Add(kv.Key, m)

        top.Add("mcp", mcp)

        if not (Map.isEmpty cfg.presets) then
            let presets = TomlTable()

            for kv in cfg.presets do
                let p = kv.Value
                let t = TomlTable()
                t.Add("name", p.name)

                if not (String.IsNullOrWhiteSpace p.provider) then
                    t.Add("provider", p.provider)

                if not (String.IsNullOrWhiteSpace p.model) then
                    t.Add("model", p.model)

                match p.instructions with
                | Some i -> t.Add("instructions", i)
                | None -> ()

                if not (List.isEmpty p.tools) then
                    let arr = TomlArray()

                    for x in p.tools do
                        arr.Add x

                    t.Add("tools", arr)

                match p.temperature with
                | Some x -> t.Add("temperature", x)
                | None -> ()

                match p.topP with
                | Some x -> t.Add("topP", x)
                | None -> ()

                match p.maxTokens with
                | Some x -> t.Add("maxTokens", int64 x)
                | None -> ()

                match p.thinkingBudget with
                | Some x -> t.Add("thinkingBudget", int64 x)
                | None -> ()

                presets.Add(kv.Key, t)

            top.Add("presets", presets)

        let auth = TomlTable()
        let clients = TomlTableArray()

        for c in cfg.authClients do
            let ct = TomlTable()
            ct.Add("tokenHash", c.tokenHash)
            ct.Add("name", c.name)

            ct.Add(
                "createdAt",
                c.createdAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            )
            // lastSeen 是主机记账，不是用户配置（决策：主机只在用户明确改配置时写文件）。
            // 仍读旧文件里的 lastSeen 供一次性迁移到主机自有状态（DeviceSightings），但不再写回。
            ct.Add("revoked", c.revoked)
            clients.Add ct

        auth.Add("clients", clients)
        top.Add("auth", auth)

        TomlSerializer.Serialize(top)
