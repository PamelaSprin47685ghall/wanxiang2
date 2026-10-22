module AppConfigTests

open System
open System.IO
open Xunit
open Wanxiang.Config

let private sampleToml =
    """
configVersion = 2
instanceId = "11111111-1111-1111-1111-111111111111"
[runtime]
server = true
client = false
pwa = true
fix = false
[network]
listen = "127.0.0.1:9999"
maxAttachmentBytes = 1048576
chunkSizeBytes = 262144
[generation]
temperature = 0.7
maxContextMessages = 120
autoTitle = true
maxToolRounds = 8
[tools]
callTimeoutSeconds = 20
[providers.openai]
kind = "openai"
label = "OpenAI"
baseUrl = "https://api.openai.com/v1"
apiKey = "sk-test"
models = ["gpt-4o-mini", "gpt-4o"]
defaultModel = "gpt-4o-mini"
timeoutSeconds = 90
maxRetries = 3
enabled = true
[mcp.fs]
command = "npx"
args = ["-y", "server"]
maxConcurrency = 2
callTimeoutSeconds = 45
[[auth.clients]]
tokenHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
name = "test"
createdAt = "2026-01-01T00:00:00Z"
revoked = false
"""

[<Theory>]
[<InlineData("0")>]
[<InlineData("-1")>]
[<InlineData("2147483648")>]
[<InlineData("4294967297")>]
[<InlineData("\"bad\"")>]
[<InlineData("true")>]
[<InlineData("1.5")>]
let ``配对限流显式非法值拒绝整份配置`` (value: string) =
    for version in [ 2; 3; 4 ] do
        for key in [ "failureWindowMinutes"; "maxFailures"; "freezeMinutes" ] do
            let toml =
                (TomlCodec.serialize (AppConfig.defaults (Guid.Parse "11111111-1111-1111-1111-111111111111")))
                    .Replace("configVersion = 4", $"configVersion = {version}")

            let line =
                match key with
                | "failureWindowMinutes" -> "failureWindowMinutes = 1"
                | "maxFailures" -> "maxFailures = 5"
                | _ -> "freezeMinutes = 5"

            match TomlCodec.tryParse (toml.Replace(line, $"{key} = {value}")) with
            | Error errors -> Assert.Contains(errors, fun error -> error.StartsWith($"pairing.{key}:"))
            | Ok _ -> failwith $"configVersion={version}: pairing.{key}={value} 不应被接受"

[<Fact>]
let ``配对三个限流参数都接受 Int32.MaxValue 这一上界`` () =
    // 三个参数都是 int32 正整数：分钟数乘 60 秒后仍远小于 TimeSpan 上限，
    // 主机侧的 TimeSpan.FromMinutes 不会溢出，没有理由再设一个更小的分钟上界。
    let baseText =
        TomlCodec.serialize (AppConfig.defaults (Guid.Parse "11111111-1111-1111-1111-111111111111"))

    for (key, original) in [ "failureWindowMinutes", 1; "maxFailures", 5; "freezeMinutes", 5 ] do
        let toml = baseText.Replace($"{key} = {original}", $"{key} = {Int32.MaxValue}")

        match TomlCodec.tryParse toml with
        | Ok cfg ->
            let parsed =
                match key with
                | "failureWindowMinutes" -> cfg.pairingFailureWindowMinutes
                | "maxFailures" -> cfg.pairingMaxFailures
                | _ -> cfg.pairingFreezeMinutes

            Assert.Equal(Int32.MaxValue, parsed)
        | Error errors -> failwith $"{key}=Int32.MaxValue 应被接受，实际报错：{errors}"

[<Fact>]
let ``分块大小小于客户端固定块时被拒绝`` () =
    for version in [ 2; 3; 4 ] do
        let toml =
            (TomlCodec.serialize (AppConfig.defaults (Guid.Parse "11111111-1111-1111-1111-111111111111")))
                .Replace("configVersion = 4", $"configVersion = {version}")
                .Replace("chunkSizeBytes = 262144", "chunkSizeBytes = 65536")

        match TomlCodec.tryParse toml with
        | Error errors -> Assert.Contains(errors, fun error -> error.Contains "network.chunkSizeBytes")
        | Ok _ -> failwith $"configVersion={version}: chunkSizeBytes=65536 小于客户端块大小，不应被接受"

[<Fact>]
let ``分块大小超出 int32 范围时按原值拒绝而不被截断`` () =
    // 4295229440 = 2^32 + 262144：截断成 int32 后正好是 262144（下界之上），
    // 只看截断值就会把一个越界配置当成合法配置放过去。
    let toml =
        (TomlCodec.serialize (AppConfig.defaults (Guid.Parse "11111111-1111-1111-1111-111111111111")))
            .Replace("chunkSizeBytes = 262144", "chunkSizeBytes = 4295229440")

    match TomlCodec.tryParse toml with
    | Error errors -> Assert.Contains(errors, fun error -> error.Contains "network.chunkSizeBytes")
    | Ok _ -> failwith "chunkSizeBytes=4295229440 超出 int32 范围，不应被接受"

[<Fact>]
let ``分块大小过大时被拒绝，避免下载时单次分配巨量缓冲`` () =
    // 主机下载附件时按这个值 Array.zeroCreate<byte>，没有上界就等于
    // 一份配置让主机单次分配任意大的缓冲（20 亿字节 ≈ 1.86 GB）。
    for value in [ "4194304"; "2000000000" ] do
        let toml =
            sampleToml.Replace("chunkSizeBytes = 262144", $"chunkSizeBytes = {value}")

        match TomlCodec.tryParse toml with
        | Error errors -> Assert.Contains(errors, fun error -> error.Contains "network.chunkSizeBytes")
        | Ok _ -> failwith $"chunkSizeBytes={value} 超过上传上限，不应被接受"

[<Fact>]
let ``附件上限超出 int32 范围时保留原值而不被截断`` () =
    // maxAttachmentBytes 在配置模型里是 int64，4294967360（4 GiB + 64）本身合法。
    // 旧实现走 int32 截断变成 64，校验看到 64 > 0 就放行：
    // 运维以为给了 4 GiB，主机实际只收 64 字节，而且不报错。
    let toml =
        sampleToml.Replace("maxAttachmentBytes = 1048576", "maxAttachmentBytes = 4294967360")

    match TomlCodec.tryParse toml with
    | Ok cfg -> Assert.Equal(4294967360L, cfg.maxAttachmentBytes)
    | Error errors -> failwith $"maxAttachmentBytes=4294967360 应被接受并保留原值，实际报错：{errors}"

[<Theory>]
[<InlineData("timeoutSeconds", "4294967297")>]
[<InlineData("maxRetries", "4294967296")>]
let ``模型服务整数参数越界时不被截断成合法值`` (key: string, value: string) =
    // 4294967297 = 2^32 + 1：截断成 int32 后是 1，正好落在合法范围内，
    // 于是越界配置被静默当成 1 秒超时 / 1 次重试。
    let line =
        match key with
        | "timeoutSeconds" -> "timeoutSeconds = 90"
        | _ -> "maxRetries = 3"

    let toml = sampleToml.Replace(line, $"{key} = {value}")

    match TomlCodec.tryParse toml with
    | Error errors -> Assert.Contains(errors, fun error -> error.Contains $"providers.openai.{key}")
    | Ok _ -> failwith $"providers.openai.{key}={value} 超出 int32 范围，不应被接受"

[<Fact>]
let ``分块大小按取值分别判定：越界与非正数报正值，低于下界报下界，恰在下界被接受`` () =
    let baseText =
        TomlCodec.serialize (AppConfig.defaults (Guid.Parse "11111111-1111-1111-1111-111111111111"))

    let parse value =
        TomlCodec.tryParse (baseText.Replace("chunkSizeBytes = 262144", $"chunkSizeBytes = {value}"))

    // 越界（2^32 + 262144）与非正数都按「不是正整数」拒绝。
    for value in [ "4295229440"; "0"; "-1" ] do
        match parse value with
        | Error errors -> Assert.Contains(errors, fun error -> error = "network.chunkSizeBytes: must be positive")
        | Ok _ -> failwith $"chunkSizeBytes={value} 不应被接受"

    // 低于客户端固定块大小：按「装不下客户端的块」拒绝。
    match parse "65536" with
    | Error errors ->
        Assert.Contains(
            errors,
            fun error -> error = "network.chunkSizeBytes: must be at least 262144 to accept client uploads"
        )
    | Ok _ -> failwith "chunkSizeBytes=65536 低于客户端块大小，不应被接受"

    // 恰在下界：被接受，且取到的就是用户写的值。
    match parse "262144" with
    | Ok cfg -> Assert.Equal(262144, cfg.chunkSizeBytes)
    | Error errors -> failwith $"chunkSizeBytes=262144 应被接受，实际报错：{errors}"

let private withNetwork (extra: string) =
    sampleToml.Replace("listen = \"127.0.0.1:9999\"", "listen = \"0.0.0.0:9999\"\n" + extra)

let private errorsOf (toml: string) =
    match TomlCodec.tryParse toml with
    | Ok _ -> []
    | Error errors -> errors

let private sampleTomlV1 =
    """
configVersion = 1
instanceId = "22222222-2222-2222-2222-222222222222"
[runtime]
server = true
client = true
pwa = true
fix = false
[network]
listen = "127.0.0.1:8765"
maxAttachmentBytes = 67108864
chunkSizeBytes = 262144
[pairing]
failureWindowMinutes = 1
maxFailures = 5
freezeMinutes = 5
[providers.openai]
kind = "openai"
baseUrl = "https://api.openai.com/v1"
apiKey = "sk-v1-key"
model = "gpt-4o"
[mcp.fs]
command = "node"
args = ["fs-server.js"]
maxConcurrency = 4
[[auth.clients]]
tokenHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
name = "v1-client"
createdAt = "2026-01-01T00:00:00Z"
revoked = false
"""

[<Theory>]
[<InlineData("0")>]
[<InlineData("-3")>]
[<InlineData("2147483648")>]
[<InlineData("\"bad\"")>]
let ``v1 配对限流显式非法值同样被拒绝`` (value: string) =
    let toml = sampleTomlV1.Replace("freezeMinutes = 5", $"freezeMinutes = {value}")

    match TomlCodec.tryParse toml with
    | Error errors -> Assert.Contains(errors, fun error -> error.StartsWith("pairing.freezeMinutes:"))
    | Ok _ -> failwith $"v1: pairing.freezeMinutes={value} 不应被接受"

[<Fact>]
let ``TOML parses every section and survives a serialize roundtrip`` () =
    match TomlCodec.tryParse sampleToml with
    | Error errs -> failwith (String.concat "; " errs)
    | Ok cfg ->
        Assert.Equal("127.0.0.1:9999", cfg.listen)
        Assert.Equal(1048576L, cfg.maxAttachmentBytes)
        Assert.True cfg.runtime.server
        Assert.False cfg.runtime.client
        Assert.True cfg.runtime.pwa
        Assert.Equal(1, cfg.providers.Count)
        Assert.Equal("sk-test", cfg.providers["openai"].apiKey |> Option.defaultValue "")
        Assert.Equal<string list>([ "gpt-4o-mini"; "gpt-4o" ], cfg.providers["openai"].models)
        Assert.Equal("gpt-4o-mini", cfg.providers["openai"].defaultModel)
        Assert.Equal(90, cfg.providers["openai"].timeoutSeconds)
        Assert.Equal(3, cfg.providers["openai"].maxRetries)
        Assert.Equal("OpenAI", ProviderConfig.displayName cfg.providers["openai"])
        Assert.Equal(Some 0.7, cfg.generation.temperature)
        Assert.Equal(120, cfg.generation.maxContextMessages)
        Assert.Equal(8, cfg.generation.maxToolRounds)
        Assert.Equal(20, cfg.tools.callTimeoutSeconds)
        Assert.Equal(2, cfg.mcpServers["fs"].maxConcurrency |> Option.defaultValue 0)
        Assert.Equal(45, cfg.mcpServers["fs"].callTimeoutSeconds)
        Assert.Equal(1, cfg.authClients.Length)
        // roundtrip
        let text = TomlCodec.serialize cfg

        match TomlCodec.tryParse text with
        | Error errs -> failwith (String.concat "; " errs)
        | Ok cfg2 ->
            Assert.Equal(cfg.listen, cfg2.listen)
            Assert.Equal(cfg.providers.Count, cfg2.providers.Count)
            Assert.Equal<string list>(cfg.providers["openai"].models, cfg2.providers["openai"].models)
            Assert.Equal(cfg.generation, cfg2.generation)
            Assert.Equal(cfg.tools, cfg2.tools)
            Assert.Equal(cfg.mcpServers["fs"], cfg2.mcpServers["fs"])
            Assert.Equal(cfg.authClients.Length, cfg2.authClients.Length)
            Assert.Equal(cfg.instanceId, cfg2.instanceId)

[<Fact>]
let ``unknown TOML field rejects the whole config`` () =
    // 注意：重复 [network] 表——TOML 允许合并；未知字段检查应命中
    match TomlCodec.tryParse (sampleToml.Replace("[network]\nlisten", "[network]\ntypo = 1\nlisten")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "unknown field")
    | Ok _ -> failwith "unknown field should be rejected"

[<Fact>]
let ``malformed token hash rejects the whole config`` () =
    let bad =
        sampleToml.Replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "short")

    match TomlCodec.tryParse bad with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "tokenHash")
    | Ok _ -> failwith "invalid token hash should be rejected"

[<Fact>]
let ``MCP id colliding with a provider id is rejected`` () =
    let bad = sampleToml.Replace("[mcp.fs]", "[mcp.openai]")

    match TomlCodec.tryParse bad with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "conflicts")
    | Ok _ -> failwith "id conflict should be rejected"

[<Fact>]
let ``只填证书或只填私钥都被拒绝`` () =
    // 半套配置会静默退回明文，而运维以为已经加密——必须在解析期就失败
    let certOnly = errorsOf (withNetwork "tlsCertPath = \"/tmp/nonexistent-cert.pem\"")
    Assert.Contains("network.tlsKeyPath: required when tlsCertPath is set", certOnly)
    let keyOnly = errorsOf (withNetwork "tlsKeyPath = \"/tmp/nonexistent-key.pem\"")
    Assert.Contains("network.tlsCertPath: required when tlsKeyPath is set", keyOnly)

[<Fact>]
let ``证书文件不存在时拒绝启动`` () =
    let errors =
        errorsOf (withNetwork "tlsCertPath = \"/tmp/does-not-exist.pem\"\ntlsKeyPath = \"/tmp/also-missing.pem\"")

    Assert.Contains("network.tlsCertPath: file not found: /tmp/does-not-exist.pem", errors)
    Assert.Contains("network.tlsKeyPath: file not found: /tmp/also-missing.pem", errors)

[<Fact>]
let ``证书路径齐备时被接受并能往返 TOML`` () =
    let dir = Path.Combine(Path.GetTempPath(), $"wanxiang-tls-{Guid.NewGuid():N}")
    Directory.CreateDirectory dir |> ignore

    try
        let cert = Path.Combine(dir, "cert.pem")
        let key = Path.Combine(dir, "key.pem")
        File.WriteAllText(cert, "-----BEGIN CERTIFICATE-----")
        File.WriteAllText(key, "-----BEGIN PRIVATE KEY-----")

        match TomlCodec.tryParse (withNetwork $"tlsCertPath = \"{cert}\"\ntlsKeyPath = \"{key}\"") with
        | Error errors -> failwith $"应当接受，却报错：{errors}"
        | Ok cfg ->
            Assert.Equal(cert, cfg.tlsCertPath)
            Assert.Equal(key, cfg.tlsKeyPath)

            match TomlCodec.tryParse (TomlCodec.serialize cfg) with
            | Error errors -> failwith $"回写后无法重新解析：{errors}"
            | Ok again ->
                Assert.Equal(cfg.tlsCertPath, again.tlsCertPath)
                Assert.Equal(cfg.tlsKeyPath, again.tlsKeyPath)
    finally
        Directory.Delete(dir, true)

[<Fact>]
let ``不配 TLS 时字段为空且配置合法`` () =
    match TomlCodec.tryParse sampleToml with
    | Error errors -> failwith $"{errors}"
    | Ok cfg ->
        Assert.Equal("", cfg.tlsCertPath)
        Assert.Equal("", cfg.tlsKeyPath)

// ---------------------------------------------------------------- 版本升级（决策 184）

[<Fact>]
let ``configVersion 1 配置能够成功解析并显式升级到当前模型`` () =
    match TomlCodec.tryParse sampleTomlV1 with
    | Error errs -> failwith (String.concat "; " errs)
    | Ok cfg ->
        // 升级后版本号为当前版本（2）
        Assert.Equal(AppConfig.CurrentVersion, cfg.configVersion)
        Assert.Equal(Guid.Parse "22222222-2222-2222-2222-222222222222", cfg.instanceId)
        Assert.Equal("127.0.0.1:8765", cfg.listen)
        Assert.Equal("", cfg.tlsCertPath)
        Assert.Equal("", cfg.tlsKeyPath)
        // v1 单 model 映射到 models 列表与 defaultModel
        Assert.True(cfg.providers.ContainsKey "openai")
        let p = cfg.providers["openai"]
        Assert.Equal<string list>([ "gpt-4o" ], p.models)
        Assert.Equal("gpt-4o", p.defaultModel)
        Assert.Equal(ProviderConfig.defaultTimeoutSeconds, p.timeoutSeconds)
        Assert.Equal(ProviderConfig.defaultMaxRetries, p.maxRetries)
        Assert.True p.enabled
        // v1 未配 generation 与 tools 时注入默认值
        Assert.Equal(GenerationDefaults.defaults, cfg.generation)
        Assert.Equal(ToolsConfig.defaults, cfg.tools)
        // v1 mcp 正确升级
        Assert.True(cfg.mcpServers.ContainsKey "fs")
        let m = cfg.mcpServers["fs"]
        Assert.Equal(Some "node", m.command)
        Assert.Equal(Some 4, m.maxConcurrency)
        Assert.Equal(McpServerConfig.defaultCallTimeoutSeconds, m.callTimeoutSeconds)
        Assert.True m.enabled
        // 写回统一输出当前版本
        let serialized = TomlCodec.serialize cfg
        Assert.Contains("configVersion = 2", serialized)

        match TomlCodec.tryParse serialized with
        | Error errs -> failwith (String.concat "; " errs)
        | Ok cfg2 ->
            Assert.Equal(cfg.instanceId, cfg2.instanceId)
            Assert.Equal(cfg.providers.Count, cfg2.providers.Count)

[<Fact>]
let ``不支持的 configVersion 返回明确错误`` () =
    let v0 = sampleToml.Replace("configVersion = 2", "configVersion = 0")

    match TomlCodec.tryParse v0 with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "configVersion 0 not supported")
    | Ok _ -> failwith "version 0 should be rejected"

    let v99 = sampleToml.Replace("configVersion = 2", "configVersion = 99")

    match TomlCodec.tryParse v99 with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "configVersion 99 not supported")
    | Ok _ -> failwith "version 99 should be rejected"

// ---------------------------------------------------------------- 默认值回归（D4/Q157，D7）

[<Fact>]
let ``provider 默认不自动重试计费调用`` () =
    Assert.Equal(0, ProviderConfig.defaultMaxRetries)

[<Fact>]
let ``TOML 语法畸形时返回错误列表`` () =
    match TomlCodec.tryParse "[[[[[bad toml" with
    | Error errs -> Assert.NotEmpty errs
    | Ok _ -> failwith "语法畸形应当报错"

[<Fact>]
let ``顶层缺少或非法 instanceId 报错`` () =
    let noId =
        sampleToml.Replace("instanceId = \"11111111-1111-1111-1111-111111111111\"", "")

    match TomlCodec.tryParse noId with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "instanceId: required UUID")
    | Ok _ -> failwith "缺少 instanceId 应当报错"

    let badId =
        sampleToml.Replace("11111111-1111-1111-1111-111111111111", "not-a-valid-guid")

    match TomlCodec.tryParse badId with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "instanceId: required UUID")
    | Ok _ -> failwith "非法 instanceId 应当报错"

[<Fact>]
let ``runtime 节非表或未知字段报错`` () =
    let notTable = sampleToml.Replace("[runtime]\nserver = true", "runtime = \"bad\"")

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "runtime: expected table")
    | Ok _ -> failwith "runtime 非表应报错"

    let unknownKey =
        sampleToml.Replace("[runtime]\nserver = true", "[runtime]\nunknown = true\nserver = true")

    match TomlCodec.tryParse unknownKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "runtime.unknown: unknown field")
    | Ok _ -> failwith "runtime 未知键应报错"

[<Fact>]
let ``network 节非表、缺少listen、非法尺寸与未知字段报错`` () =
    let notTable =
        "network = 123\n"
        + (sampleToml.Replace("[network]\nlisten", "#network\n#listen"))

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network: expected table")
    | Ok _ -> failwith "network 非表应报错"

    let emptyListen =
        sampleToml.Replace("listen = \"127.0.0.1:9999\"", "listen = \"   \"")

    match TomlCodec.tryParse emptyListen with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network.listen: required")
    | Ok _ -> failwith "空 listen 应报错"

    let badMaxAtt =
        sampleToml.Replace("maxAttachmentBytes = 1048576", "maxAttachmentBytes = 0")

    match TomlCodec.tryParse badMaxAtt with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network.maxAttachmentBytes: must be positive")
    | Ok _ -> failwith "maxAttachmentBytes <= 0 应报错"

    let badChunk = sampleToml.Replace("chunkSizeBytes = 262144", "chunkSizeBytes = -1")

    match TomlCodec.tryParse badChunk with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network.chunkSizeBytes: must be positive")
    | Ok _ -> failwith "chunkSizeBytes <= 0 应报错"

    let unknownField =
        sampleToml.Replace("[network]\nlisten", "[network]\nextraNet = 1\nlisten")

    match TomlCodec.tryParse unknownField with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network.extraNet: unknown field")
    | Ok _ -> failwith "network 未知字段应报错"

[<Fact>]
let ``pairing 节非表与未知字段报错`` () =
    let notTable =
        sampleToml.Replace("configVersion = 2\n", "configVersion = 2\npairing = false\n")

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "pairing: expected table")
    | Ok _ -> failwith "pairing 非表应报错"

    let unknownField =
        sampleToml
        + "
[pairing]
unknown = 1
"

    match TomlCodec.tryParse unknownField with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "pairing.unknown: unknown field")
    | Ok _ -> failwith "pairing 未知键应报错"

[<Fact>]
let ``generation 节各参数边界校验与未知键报错`` () =
    let notTable =
        "generation = 1\n"
        + (sampleToml.Replace("[generation]\ntemperature", "#generation\n#temperature"))

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation: expected table")
    | Ok _ -> failwith "generation 非表应报错"

    let unknownKey =
        sampleToml.Replace("[generation]\ntemperature", "[generation]\nfoo = 1\ntemperature")

    match TomlCodec.tryParse unknownKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.foo: unknown field")
    | Ok _ -> failwith "generation 未知键应报错"

    // temperature < 0.0 或 > 2.0
    let tempLow = sampleToml.Replace("temperature = 0.7", "temperature = -0.1")

    match TomlCodec.tryParse tempLow with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.temperature: must be within [0, 2]")
    | Ok _ -> failwith "temp < 0 应报错"

    let tempHigh = sampleToml.Replace("temperature = 0.7", "temperature = 2.1")

    match TomlCodec.tryParse tempHigh with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.temperature: must be within [0, 2]")
    | Ok _ -> failwith "temp > 2 应报错"

    // topP <= 0.0 或 > 1.0
    match TomlCodec.tryParse (sampleToml.Replace("[generation]\n", "[generation]\ntopP = 0.0\n")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.topP: must be within (0, 1]")
    | Ok _ -> failwith "topP <= 0 应报错"

    match TomlCodec.tryParse (sampleToml.Replace("[generation]\n", "[generation]\ntopP = 1.1\n")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.topP: must be within (0, 1]")
    | Ok _ -> failwith "topP > 1 应报错"

    // maxTokens <= 0
    match TomlCodec.tryParse (sampleToml.Replace("[generation]\n", "[generation]\nmaxTokens = 0\n")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.maxTokens: must be positive")
    | Ok _ -> failwith "maxTokens <= 0 应报错"

    // maxContextMessages < 0
    match TomlCodec.tryParse (sampleToml.Replace("maxContextMessages = 120", "maxContextMessages = -1")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.maxContextMessages: must not be negative")
    | Ok _ -> failwith "maxContextMessages < 0 应报错"

    // maxContextTokens < 0
    match TomlCodec.tryParse (sampleToml.Replace("[generation]\n", "[generation]\nmaxContextTokens = -5\n")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.maxContextTokens: must not be negative")
    | Ok _ -> failwith "maxContextTokens < 0 应报错"

    // maxToolRounds <= 0
    match TomlCodec.tryParse (sampleToml.Replace("maxToolRounds = 8", "maxToolRounds = 0")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.maxToolRounds: must be positive")
    | Ok _ -> failwith "maxToolRounds <= 0 应报错"

    // thinkingBudget < 0
    match TomlCodec.tryParse (sampleToml.Replace("[generation]\n", "[generation]\nthinkingBudget = -1\n")) with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "generation.thinkingBudget: must not be negative")
    | Ok _ -> failwith "thinkingBudget < 0 应报错"

[<Fact>]
let ``tools 节非表、未知键、超时非正及相对路径报错`` () =
    let notTable =
        "tools = 1\n"
        + (sampleToml.Replace("[tools]\ncallTimeoutSeconds", "#tools\n#callTimeoutSeconds"))

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "tools: expected table")
    | Ok _ -> failwith "tools 非表应报错"

    let unknownKey =
        sampleToml.Replace("[tools]\ncallTimeoutSeconds", "[tools]\nunknown = 1\ncallTimeoutSeconds")

    match TomlCodec.tryParse unknownKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "tools.unknown: unknown field")
    | Ok _ -> failwith "tools 未知键应报错"

    let badTimeout =
        sampleToml.Replace("callTimeoutSeconds = 20", "callTimeoutSeconds = 0")

    match TomlCodec.tryParse badTimeout with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "tools.callTimeoutSeconds: must be positive")
    | Ok _ -> failwith "callTimeoutSeconds <= 0 应报错"

    let relativeRoot =
        sampleToml.Replace("[tools]\n", "[tools]\nfileReadRoots = [\"relative/path\"]\n")

    match TomlCodec.tryParse relativeRoot with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "must be an absolute path")
    | Ok _ -> failwith "非绝对路径 fileReadRoots 应报错"

[<Fact>]
let ``providers 节非表、不支持类型、空/相对端点、空模型、默认模型不存在与参数校验`` () =
    let notTable =
        sampleToml
            .Replace("configVersion = 2\n", "configVersion = 2\nproviders = 1\n")
            .Replace(
                "[providers.openai]\nkind = \"openai\"\nlabel = \"OpenAI\"\nbaseUrl = \"https://api.openai.com/v1\"\napiKey = \"sk-test\"\nmodels = [\"gpt-4o-mini\", \"gpt-4o\"]\ndefaultModel = \"gpt-4o-mini\"\ntimeoutSeconds = 90\nmaxRetries = 3\nenabled = true\n",
                ""
            )

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers: expected table")
    | Ok _ -> failwith "providers 非表应报错"

    let providerNotTable =
        sampleToml.Replace("[providers.openai]\nkind", "[providers]\nopenai = \"bad\"\n#kind")

    match TomlCodec.tryParse providerNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai: expected table")
    | Ok _ -> failwith "provider 项非表应报错"

    let unknownField =
        sampleToml.Replace("[providers.openai]\nkind", "[providers.openai]\nunknown = 1\nkind")

    match TomlCodec.tryParse unknownField with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai.unknown: unknown field")
    | Ok _ -> failwith "provider 未知键应报错"

    let unsupportedKind =
        sampleToml.Replace("kind = \"openai\"", "kind = \"unsupported_xyz\"")

    match TomlCodec.tryParse unsupportedKind with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "unsupported kind")
    | Ok _ -> failwith "不支持 kind 应报错"

    let emptyBaseUrl =
        sampleToml.Replace("baseUrl = \"https://api.openai.com/v1\"", "baseUrl = \" \"")

    match TomlCodec.tryParse emptyBaseUrl with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai.baseUrl: required")
    | Ok _ -> failwith "空 baseUrl 应报错"

    let notAbsoluteUrl =
        sampleToml.Replace("baseUrl = \"https://api.openai.com/v1\"", "baseUrl = \"api.openai.com/v1\"")

    match TomlCodec.tryParse notAbsoluteUrl with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai.baseUrl: must be an absolute URL")
    | Ok _ -> failwith "非绝对 baseUrl 应报错"

    let emptyModels =
        sampleToml.Replace("models = [\"gpt-4o-mini\", \"gpt-4o\"]", "models = []")

    match TomlCodec.tryParse emptyModels with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "at least one model required")
    | Ok _ -> failwith "空 models 列表应报错"

    let badDefaultModel =
        sampleToml.Replace("defaultModel = \"gpt-4o-mini\"", "defaultModel = \"non-existent\"")

    match TomlCodec.tryParse badDefaultModel with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "is not in models")
    | Ok _ -> failwith "defaultModel 不在 models 里应报错"

    let badTimeout = sampleToml.Replace("timeoutSeconds = 90", "timeoutSeconds = 0")

    match TomlCodec.tryParse badTimeout with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "timeoutSeconds: must be positive")
    | Ok _ -> failwith "timeoutSeconds <= 0 应报错"

    let badRetries = sampleToml.Replace("maxRetries = 3", "maxRetries = -1")

    match TomlCodec.tryParse badRetries with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "maxRetries: must not be negative")
    | Ok _ -> failwith "maxRetries < 0 应报错"

[<Fact>]
let ``mcp 节非表、项非表、command与url互斥/均缺、url非绝对及并发超时校验`` () =
    let notTable =
        sampleToml
            .Replace("configVersion = 2\n", "configVersion = 2\nmcp = 1\n")
            .Replace(
                "[mcp.fs]\ncommand = \"npx\"\nargs = [\"-y\", \"server\"]\nmaxConcurrency = 2\ncallTimeoutSeconds = 45\n",
                ""
            )

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp: expected table")
    | Ok _ -> failwith "mcp 非表应报错"

    let mcpNotTable =
        sampleToml.Replace("[mcp.fs]\ncommand", "[mcp]\nfs = false\n#command")

    match TomlCodec.tryParse mcpNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs: expected table")
    | Ok _ -> failwith "mcp.fs 非表应报错"

    let unknownKey =
        sampleToml.Replace("[mcp.fs]\ncommand", "[mcp.fs]\nunknown = 1\ncommand")

    match TomlCodec.tryParse unknownKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.unknown: unknown field")
    | Ok _ -> failwith "mcp.fs 未知字段应报错"

    // 缺少 command 与 url
    let neither = sampleToml.Replace("command = \"npx\"", "#no command")

    match TomlCodec.tryParse neither with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "either command or url is required")
    | Ok _ -> failwith "缺少 command 和 url 应报错"

    // 同时提供 command 与 url
    let both =
        sampleToml.Replace("command = \"npx\"", "command = \"npx\"\n" + "url = \"https://mcp.local\"")

    match TomlCodec.tryParse both with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mutually exclusive")
    | Ok _ -> failwith "command 和 url 互斥应报错"

    // url 不是绝对 URL
    let badUrl = sampleToml.Replace("command = \"npx\"", "url = \"bad-url\"")

    match TomlCodec.tryParse badUrl with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.url: must be an absolute URL")
    | Ok _ -> failwith "非绝对 URL 应报错"

    // callTimeoutSeconds <= 0
    let badTimeout =
        sampleToml.Replace("callTimeoutSeconds = 45", "callTimeoutSeconds = 0")

    match TomlCodec.tryParse badTimeout with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.callTimeoutSeconds: must be positive")
    | Ok _ -> failwith "callTimeoutSeconds <= 0 应报错"

    // maxConcurrency <= 0
    let badConcurrency = sampleToml.Replace("maxConcurrency = 2", "maxConcurrency = 0")

    match TomlCodec.tryParse badConcurrency with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.maxConcurrency: must be positive")
    | Ok _ -> failwith "maxConcurrency <= 0 应报错"

[<Fact>]
let ``auth 节非表、clients非表数组、未知键、tokenHash缺失及大小写格式校验`` () =
    let notTable =
        sampleToml
            .Replace("configVersion = 2\n", "configVersion = 2\nauth = 1\n")
            .Replace(
                "[[auth.clients]]\ntokenHash = \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\nname = \"test\"\ncreatedAt = \"2026-01-01T00:00:00Z\"\nrevoked = false\n",
                ""
            )

    match TomlCodec.tryParse notTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth: expected table")
    | Ok _ -> failwith "auth 非表应报错"

    let unknownAuthKey =
        sampleToml.Replace("[[auth.clients]]", "[auth]\nunknown = true\n[[auth.clients]]")

    match TomlCodec.tryParse unknownAuthKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth.unknown: unknown field")
    | Ok _ -> failwith "auth 未知键应报错"

    let clientsNotArray =
        sampleToml.Replace(
            "[[auth.clients]]\ntokenHash = \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\nname = \"test\"\ncreatedAt = \"2026-01-01T00:00:00Z\"\nrevoked = false",
            "[auth]\nclients = \"string\""
        )

    match TomlCodec.tryParse clientsNotArray with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth.clients: expected array of tables")
    | Ok _ -> failwith "auth.clients 非表数组应报错"

    let unknownClientKey =
        sampleToml.Replace("[[auth.clients]]\ntokenHash", "[[auth.clients]]\nunknown = 1\ntokenHash")

    match TomlCodec.tryParse unknownClientKey with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth.clients[].unknown: unknown field")
    | Ok _ -> failwith "auth.clients 元素未知键应报错"

    let missingHash =
        sampleToml.Replace("tokenHash = \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"", "")

    match TomlCodec.tryParse missingHash with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth.clients[]: missing tokenHash")
    | Ok _ -> failwith "缺少 tokenHash 应报错"

    // tokenHash 大写
    let uppercaseHash =
        sampleToml.Replace(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
        )

    match TomlCodec.tryParse uppercaseHash with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "auth.clients[]: tokenHash must be lowercase")
    | Ok _ -> failwith "大写 tokenHash 应报错"

    // tokenHash 含有非 16 进制字符
    let nonHexHash =
        sampleToml.Replace(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaz"
        )

    match TomlCodec.tryParse nonHexHash with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "tokenHash must be lowercase hex sha256")
    | Ok _ -> failwith "非十六进制 tokenHash 应报错"

[<Fact>]
let ``v1 配置错误分支全面覆盖（缺少id、非表结构、字段越界、冲突校验）`` () =
    // v1 缺少 instanceId
    let noId =
        sampleTomlV1.Replace("instanceId = \"22222222-2222-2222-2222-222222222222\"", "")

    match TomlCodec.tryParse noId with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "instanceId: required UUID")
    | Ok _ -> failwith "v1 缺少 instanceId 应报错"

    // v1 network 非表
    let netNotTable =
        "network = false\n"
        + (sampleTomlV1.Replace("[network]\nlisten", "#network\n#listen"))

    match TomlCodec.tryParse netNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network: expected table")
    | Ok _ -> failwith "v1 network 非表应报错"

    // v1 network 未知键
    let netUnknown =
        sampleTomlV1.Replace("[network]\nlisten", "[network]\nunknown = 1\nlisten")

    match TomlCodec.tryParse netUnknown with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "network.unknown: unknown field")
    | Ok _ -> failwith "v1 network 未知键应报错"

    // v1 network 参数越界
    let netBad =
        sampleTomlV1
            .Replace("listen = \"127.0.0.1:8765\"", "listen = \" \"")
            .Replace("maxAttachmentBytes = 67108864", "maxAttachmentBytes = -1")
            .Replace("chunkSizeBytes = 262144", "chunkSizeBytes = 0")

    match TomlCodec.tryParse netBad with
    | Error errs ->
        Assert.Contains(errs, fun e -> e.Contains "network.listen: required")
        Assert.Contains(errs, fun e -> e.Contains "network.maxAttachmentBytes: must be positive")
        Assert.Contains(errs, fun e -> e.Contains "network.chunkSizeBytes: must be positive")
    | Ok _ -> failwith "v1 network 越界应报错"

    // v1 pairing 非表与未知键
    let pairNotTable =
        "pairing = 1\n"
        + (sampleTomlV1.Replace("[pairing]\nfailureWindowMinutes", "#pairing\n#failureWindowMinutes"))

    match TomlCodec.tryParse pairNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "pairing: expected table")
    | Ok _ -> failwith "v1 pairing 非表应报错"

    let pairUnknown =
        sampleTomlV1.Replace("[pairing]\nfailureWindowMinutes", "[pairing]\nunknown = 1\nfailureWindowMinutes")

    match TomlCodec.tryParse pairUnknown with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "pairing.unknown: unknown field")
    | Ok _ -> failwith "v1 pairing 未知键应报错"

    // v1 providers 非表、项非表与未知键
    let provNotTable =
        "providers = 1\n"
        + (sampleTomlV1.Replace("[providers.openai]", "#[providers.openai]"))

    match TomlCodec.tryParse provNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers: expected table")
    | Ok _ -> failwith "v1 providers 非表应报错"

    let provItemNotTable =
        sampleTomlV1.Replace("[providers.openai]\nkind", "[providers]\nopenai = 1\n#kind")

    match TomlCodec.tryParse provItemNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai: expected table")
    | Ok _ -> failwith "v1 providers 项非表应报错"

    let provUnknown =
        sampleTomlV1.Replace("[providers.openai]\nkind", "[providers.openai]\nunknown = 1\nkind")

    match TomlCodec.tryParse provUnknown with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "providers.openai.unknown: unknown field")
    | Ok _ -> failwith "v1 provider 未知键应报错"

    // v1 provider 错误校验
    let provBad =
        sampleTomlV1
            .Replace("kind = \"openai\"", "kind = \"bad_kind\"")
            .Replace("baseUrl = \"https://api.openai.com/v1\"", "baseUrl = \"not-url\"")
            .Replace("model = \"gpt-4o\"", "model = \" \"")

    match TomlCodec.tryParse provBad with
    | Error errs ->
        Assert.Contains(errs, fun e -> e.Contains "unsupported kind")
        Assert.Contains(errs, fun e -> e.Contains "baseUrl: must be an absolute URL")
        Assert.Contains(errs, fun e -> e.Contains "providers.openai.model: required")
    | Ok _ -> failwith "v1 provider 错误应报错"

    // v1 mcp 非表、项非表、未知键、二选一校验、url非绝对、并发非正
    let mcpNotTable = "mcp = 1\n" + (sampleTomlV1.Replace("[mcp.fs]", "#[mcp.fs]"))

    match TomlCodec.tryParse mcpNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp: expected table")
    | Ok _ -> failwith "v1 mcp 非表应报错"

    let mcpItemNotTable =
        sampleTomlV1.Replace("[mcp.fs]\ncommand", "[mcp]\nfs = 1\n#command")

    match TomlCodec.tryParse mcpItemNotTable with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs: expected table")
    | Ok _ -> failwith "v1 mcp 项非表应报错"

    let mcpUnknown =
        sampleTomlV1.Replace("[mcp.fs]\ncommand", "[mcp.fs]\nunknown = 1\ncommand")

    match TomlCodec.tryParse mcpUnknown with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.unknown: unknown field")
    | Ok _ -> failwith "v1 mcp 未知键应报错"

    let mcpNeither = sampleTomlV1.Replace("command = \"node\"", "#neither")

    match TomlCodec.tryParse mcpNeither with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "either command or url is required")
    | Ok _ -> failwith "v1 mcp 无 command/url 应报错"

    let mcpBoth =
        sampleTomlV1.Replace("command = \"node\"", "command = \"node\"\n" + "url = \"https://mcp.local\"")

    match TomlCodec.tryParse mcpBoth with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mutually exclusive")
    | Ok _ -> failwith "v1 mcp 冲突应报错"

    let mcpBadUrl = sampleTomlV1.Replace("command = \"node\"", "url = \"not-url\"")

    match TomlCodec.tryParse mcpBadUrl with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.url: must be an absolute URL")
    | Ok _ -> failwith "v1 mcp 非绝对 URL 应报错"

    let mcpBadConc = sampleTomlV1.Replace("maxConcurrency = 4", "maxConcurrency = 0")

    match TomlCodec.tryParse mcpBadConc with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "mcp.fs.maxConcurrency: must be positive")
    | Ok _ -> failwith "v1 mcp 并发 <= 0 应报错"

    let mcpIdConflict = sampleTomlV1.Replace("[mcp.fs]", "[mcp.openai]")

    match TomlCodec.tryParse mcpIdConflict with
    | Error errs -> Assert.Contains(errs, fun e -> e.Contains "id conflicts with provider id")
    | Ok _ -> failwith "v1 mcp id 冲突应报错"

[<Fact>]
let ``复杂配置往返：包含extraJson递归(对象/数组/值/数字/布尔/字符串)、环境变量、headers与自定义选项`` () =
    let tomlWithAll =
        """
configVersion = 2
instanceId = "33333333-3333-3333-3333-333333333333"
[runtime]
server = false
client = true
pwa = false
fix = true
[network]
listen = "0.0.0.0:8080"
maxAttachmentBytes = 33554432
chunkSizeBytes = 262144
[pairing]
failureWindowMinutes = 2
maxFailures = 10
freezeMinutes = 15
[generation]
temperature = 1
topP = 0.95
maxTokens = 4096
instructions = "You are a helpful assistant."
maxContextMessages = 150
maxContextTokens = 64000
autoTitle = false
maxToolRounds = 15
thinkingBudget = 1024
[tools]
fileReadRoots = ["/var/data", "/tmp/sandbox"]
callTimeoutSeconds = 40
[providers.anthropic]
kind = "anthropic"
label = "Claude"
baseUrl = "https://api.anthropic.com"
apiKey = "sk-ant-test"
models = ["claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022"]
defaultModel = "claude-3-5-sonnet-20241022"
timeoutSeconds = 180
maxRetries = 2
enabled = true
promptCaching = true
[providers.anthropic.headers]
X-Custom-Header = "custom-val"
[providers.anthropic.extra]
system_prompt = "custom"
cache_enabled = true
priority = 10
ratio = 0.75
tags = ["fast", "smart"]
nested = { depth = 2, active = false }
[mcp.remote]
label = "Remote MCP"
url = "https://mcp.remote.example.com/sse"
callTimeoutSeconds = 50
maxConcurrency = 8
enabled = false
[mcp.remote.env]
ENV_VAR_A = "val_a"
ENV_VAR_B = "val_b"
[[auth.clients]]
tokenHash = "1111111111111111111111111111111111111111111111111111111111111111"
name = "full-client"
createdAt = "2026-02-01T12:00:00Z"
lastSeen = "2026-02-02T15:30:00Z"
revoked = true
"""

    match TomlCodec.tryParse tomlWithAll with
    | Error errs -> failwith (String.concat "; " errs)
    | Ok cfg ->
        // 校验解析
        Assert.False cfg.runtime.server
        Assert.True cfg.runtime.fix
        Assert.Equal(2, cfg.pairingFailureWindowMinutes)
        Assert.Equal(10, cfg.pairingMaxFailures)
        Assert.Equal(15, cfg.pairingFreezeMinutes)
        Assert.Equal(Some 1.0, cfg.generation.temperature)
        Assert.Equal(Some 0.95, cfg.generation.topP)
        Assert.Equal(Some 4096, cfg.generation.maxTokens)
        Assert.Equal(Some "You are a helpful assistant.", cfg.generation.instructions)
        Assert.False cfg.generation.autoTitle
        Assert.Equal(1024, cfg.generation.thinkingBudget)
        Assert.Equal<string list>([ "/var/data"; "/tmp/sandbox" ], cfg.tools.fileReadRoots)
        Assert.Equal(40, cfg.tools.callTimeoutSeconds)
        Assert.True(cfg.providers.ContainsKey "anthropic")
        let p = cfg.providers["anthropic"]
        Assert.Equal("Claude", p.label)
        Assert.True p.promptCaching
        Assert.Equal("custom-val", p.headers["X-Custom-Header"])
        Assert.True p.extraJson.IsSome
        let m = cfg.mcpServers["remote"]
        Assert.Equal("Remote MCP", m.label)
        Assert.Equal(Some "https://mcp.remote.example.com/sse", m.url)
        Assert.Equal(None, m.command)
        Assert.False m.enabled
        Assert.Equal(8, m.maxConcurrency |> Option.defaultValue 0)
        Assert.Equal("val_a", m.env["ENV_VAR_A"])
        let c = cfg.authClients.Head
        Assert.True c.revoked
        Assert.True c.lastSeenUtc.IsSome

        // 序列化回写并再次解析
        let serialized = TomlCodec.serialize cfg
        Assert.Contains("promptCaching = true", serialized)
        Assert.Contains("thinkingBudget = 1024", serialized)
        Assert.Contains("X-Custom-Header", serialized)
        Assert.Contains("ENV_VAR_A", serialized)
        Assert.Contains("Remote MCP", serialized)
        // lastSeen 只读不写：主机记账不进用户配置文件。
        Assert.DoesNotContain("lastSeen", serialized)

        match TomlCodec.tryParse serialized with
        | Error errs -> failwith (String.concat "; " errs)
        | Ok cfg2 ->
            Assert.Equal(cfg.instanceId, cfg2.instanceId)
            Assert.Equal(cfg.generation.thinkingBudget, cfg2.generation.thinkingBudget)
            Assert.Equal<string list>(cfg.tools.fileReadRoots, cfg2.tools.fileReadRoots)
            Assert.Equal(cfg.providers["anthropic"].promptCaching, cfg2.providers["anthropic"].promptCaching)
            Assert.Equal<Map<string, string>>(cfg.providers["anthropic"].headers, cfg2.providers["anthropic"].headers)
            Assert.Equal(cfg.mcpServers["remote"].url, cfg2.mcpServers["remote"].url)
            Assert.Equal<Map<string, string>>(cfg.mcpServers["remote"].env, cfg2.mcpServers["remote"].env)
            Assert.Equal(cfg.authClients.Head.revoked, cfg2.authClients.Head.revoked)

// ================================================================
// ConfigModel 辅助方法测试（ProviderConfig, McpServerConfig, AppConfig）
// ================================================================

[<Fact>]
let ``ProviderConfig 辅助函数：displayName, hasModel, resolveModel 覆盖`` () =
    let pNoLabel =
        {
            id = "my-p"
            kind = "openai"
            label = ""
            baseUrl = "https://example.com"
            apiKey = None
            models = [ "gpt-4"; "gpt-3.5" ]
            defaultModel = "gpt-4"
            timeoutSeconds = 60
            maxRetries = 0
            enabled = true
            promptCaching = false
            headers = Map.empty
            extraJson = None
            visionOverrides = Map.empty
        }

    Assert.Equal("my-p", ProviderConfig.displayName pNoLabel)

    let pWithLabel =
        { pNoLabel with
            label = "My Super Provider"
        }

    Assert.Equal("My Super Provider", ProviderConfig.displayName pWithLabel)

    Assert.True(ProviderConfig.hasModel "gpt-4" pNoLabel)
    Assert.True(ProviderConfig.hasModel "gpt-3.5" pNoLabel)
    Assert.False(ProviderConfig.hasModel "claude" pNoLabel)

    // resolveModel
    // 1. requested 在列表中
    Assert.Equal("gpt-3.5", ProviderConfig.resolveModel "gpt-3.5" pNoLabel)
    // 2. requested 为空 -> 回落 defaultModel
    Assert.Equal("gpt-4", ProviderConfig.resolveModel "" pNoLabel)
    Assert.Equal("gpt-4", ProviderConfig.resolveModel "not-exist" pNoLabel)
    // 3. defaultModel 也为空 -> 回落 models.head
    let pNoDefault = { pNoLabel with defaultModel = "" }
    Assert.Equal("gpt-4", ProviderConfig.resolveModel "not-exist" pNoDefault)
    // 4. models 为空 -> 回落空字符串
    let pEmptyModels =
        { pNoLabel with
            models = []
            defaultModel = ""
        }

    Assert.Equal("", ProviderConfig.resolveModel "not-exist" pEmptyModels)

[<Fact>]
let ``McpServerConfig 辅助函数：displayName 覆盖`` () =
    let mNoLabel =
        {
            id = "mcp1"
            label = "   "
            command = Some "run"
            args = []
            env = Map.empty
            url = None
            maxConcurrency = None
            callTimeoutSeconds = 60
            enabled = true
        }

    Assert.Equal("mcp1", McpServerConfig.displayName mNoLabel)
    let mWithLabel = { mNoLabel with label = "MCP Server 1" }
    Assert.Equal("MCP Server 1", McpServerConfig.displayName mWithLabel)

[<Fact>]
let ``AppConfig usableProviders, defaultProvider, tryProvider 覆盖`` () =
    let id = Guid.NewGuid()
    let cfg = AppConfig.defaults id
    Assert.True(AppConfig.usableProviders cfg |> List.isEmpty)
    Assert.True(AppConfig.defaultProvider cfg |> Option.isNone)
    Assert.True(AppConfig.tryProvider "p1" cfg |> Option.isNone)

    let p1 =
        {
            id = "p-beta"
            kind = "openai"
            label = ""
            baseUrl = "https://b.com"
            apiKey = None
            models = [ "m1" ]
            defaultModel = "m1"
            timeoutSeconds = 60
            maxRetries = 0
            enabled = true
            promptCaching = false
            headers = Map.empty
            extraJson = None
            visionOverrides = Map.empty
        }

    let p2 = { p1 with id = "p-alpha" }

    let pDisabled =
        { p1 with
            id = "p-disabled"
            enabled = false
        }

    let pNoModels =
        { p1 with
            id = "p-nomodels"
            models = []
        }

    let cfgWithProviders =
        { cfg with
            providers = Map.ofList [ p1.id, p1; p2.id, p2; pDisabled.id, pDisabled; pNoModels.id, pNoModels ]
        }
    // usableProviders 应过滤 disabled 和 无模型的，且按 id 排序（p-alpha 在前）
    let usable = AppConfig.usableProviders cfgWithProviders
    Assert.Equal(2, usable.Length)
    Assert.Equal("p-alpha", usable.[0].id)
    Assert.Equal("p-beta", usable.[1].id)

    // defaultProvider 为首个可用者
    let defP = AppConfig.defaultProvider cfgWithProviders
    Assert.True defP.IsSome
    Assert.Equal("p-alpha", defP.Value.id)

    // tryProvider
    Assert.True((AppConfig.tryProvider "p-beta" cfgWithProviders).IsSome)
    Assert.True((AppConfig.tryProvider "non-existent" cfgWithProviders).IsNone)
