module Tests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Config
open Wanxiang.Server
open Wanxiang.UI

// 迁自旧 R16ImageVisionTests。
module VisionTests =
    let private providerInfo overrides detected : ProviderInfo =
        { id = "local"; label = "Ollama"; kind = "openai"; baseUrl = "http://127.0.0.1:11434/v1"; hasApiKey = false
          models = [ "llama3.1:8b"; "my-vl"; "llava:13b" ]; defaultModel = "llama3.1:8b"; timeoutSeconds = 60; maxRetries = 0
          enabled = true; visionOverrides = overrides; visionDetected = detected }

    [<Fact>]
    let ``vision precedence is user override, then provider metadata, then name`` () =
        let catalog detected overrides = { Catalog.empty with providers = [ providerInfo overrides detected ] }
        // 只有名字：llama3.1 推断为只吃文字。
        Assert.Equal((VisionNo, VisionFromName), Catalog.visionOf "local" "llama3.1:8b" (catalog Map.empty Map.empty))
        // 服务信息说能看图：压过名字。
        Assert.Equal((VisionYes, VisionFromProvider), Catalog.visionOf "local" "llama3.1:8b" (catalog (Map.ofList [ "llama3.1:8b", true ]) Map.empty))
        // 用户指定：压过服务信息。
        Assert.Equal(
            (VisionNo, VisionFromUser),
            Catalog.visionOf "local" "llama3.1:8b" (catalog (Map.ofList [ "llama3.1:8b", true ]) (Map.ofList [ "llama3.1:8b", false ])))
        Assert.Equal((VisionUnknown, VisionFromName), Catalog.visionOf "local" "my-model" (catalog Map.empty Map.empty))

// 迁自旧 R26PresetsTests：目录快照经真实 AppConfig + ProviderCatalog.generation 生成。
module PresetTests =

    let private instance = Guid.Parse "0192b4a0-0000-7000-8000-0000000000aa"

    let private samplePreset : ConversationPreset =
        { id = "writer"
          name = "写作助手"
          provider = "openai"
          model = "gpt-4o"
          instructions = Some "你是一位严谨的中文编辑。\n只改表达，不改原意。"
          tools = [ "builtin:time" ]
          temperature = Some 0.4
          topP = None
          maxTokens = Some 2048
          thinkingBudget = Some 0 }
    let private provider id models : ProviderInfo =
        { id = id; label = id.ToUpperInvariant(); kind = "openai"; baseUrl = "http://x"; hasApiKey = true
          models = models; defaultModel = List.head models; timeoutSeconds = 60; maxRetries = 0; enabled = true
          visionOverrides = Map.empty; visionDetected = Map.empty }

    let private catalogWithPresets () =
        let cfg =
            { AppConfig.defaults instance with
                presets = Map.ofList [ "writer", samplePreset ] }
        Catalog.parse (JsonArray()) (JsonArray()) (ProviderCatalog.generation cfg)

    [<Fact>]
    let ``catalog snapshot carries presets to clients`` () =
        let catalog = catalogWithPresets ()
        let p = List.exactlyOne catalog.presets
        Assert.Equal("写作助手", p.name)
        Assert.Equal(Some 0.4, p.temperature)
        Assert.Equal<string list>([ "builtin:time" ], p.tools)
        // 旧主机没有 presets 字段 → 空列表
        Assert.Empty((Catalog.parse (JsonArray()) (JsonArray()) (JsonObject())).presets)

    [<Fact>]
    let ``applying a preset falls back when its model or tools are gone`` () =
        let preset = List.exactlyOne (catalogWithPresets ()).presets
        let tool : ToolInfo = { id = "builtin:time"; label = "time"; description = ""; source = "builtin"; serverId = None }
        let ok = { Catalog.empty with providers = [ provider "openai" [ "gpt-4o"; "gpt-4o-mini" ] ]; tools = [ tool ] }
        let config, note = Presets.toSessionConfig ok preset
        Assert.Equal(("openai", "gpt-4o"), (config.provider, config.model))
        Assert.Equal<string list>([ "builtin:time" ], config.tools)
        Assert.Equal(Some 2048, config.maxTokens)
        Assert.True(note.IsNone)
        // 模型服务被删 + 工具消失：回落默认模型，并说明原因
        let gone = { Catalog.empty with providers = [ provider "anthropic" [ "claude" ] ] }
        let config2, note2 = Presets.toSessionConfig gone preset
        Assert.Equal(("anthropic", "claude"), (config2.provider, config2.model))
        Assert.Empty(config2.tools)
        Assert.Contains("已不可用", note2.Value)
        Assert.Contains("工具 time 在主机上已没有", note2.Value)

    [<Fact>]
    let ``preset names are suggested from the title without collisions`` () =
        let existing = (catalogWithPresets ()).presets
        Assert.Equal("写作助手 2", Presets.suggestName existing "写作助手")
        Assert.Equal("我的预设", Presets.suggestName existing "新对话")
        Assert.Contains("gpt-4o", Presets.describe (catalogWithPresets ()) existing.Head)

// 迁自旧 ModelChipRateLimitTests。
module SwitcherTests =

    // ---------------------------------------------------------------- 快速切换纯逻辑

    let private provider (id: string) (label: string) (models: string list) : ProviderInfo =
        { id = id; label = label; kind = "openai"; baseUrl = ""; hasApiKey = true; models = models
          defaultModel = List.head models; timeoutSeconds = 30; maxRetries = 0; enabled = true; visionOverrides = Map.empty; visionDetected = Map.empty }

    let private catalog =
        { Catalog.empty with
            providers =
              [ provider "oa" "OpenAI" [ "gpt-4o"; "gpt-4o-mini"; "o3" ]
                provider "ds" "DeepSeek" [ "deepseek-chat"; "deepseek-reasoner" ]
                { provider "off" "Disabled" [ "x" ] with enabled = false } ]
            generation = { Catalog.empty.generation with defaultProvider = Some "ds"; defaultModel = Some "deepseek-chat" } }

    [<Fact>]
    let ``switcher lists usable models with provider, current and default marks`` () =
        let items = ModelSwitcher.options catalog (Some("oa", "o3"))
        Assert.Equal(5, items.Length)
        Assert.DoesNotContain(items, fun o -> o.providerId = "off")
        Assert.Equal<string list>([ "o3" ], items |> List.filter _.isCurrent |> List.map _.model)
        Assert.Equal<string list>([ "deepseek-chat" ], items |> List.filter _.isDefault |> List.map _.model)
        Assert.All(items, fun o -> Assert.False(String.IsNullOrWhiteSpace o.providerLabel))

    [<Fact>]
    let ``switcher search matches model or provider and ranks prefix hits first`` () =
        let items = ModelSwitcher.options catalog None
        Assert.Equal<string list>([ "deepseek-chat"; "deepseek-reasoner" ], ModelSwitcher.filter "deepseek" items |> List.map _.model)
        Assert.Equal<string list>([ "gpt-4o-mini" ], ModelSwitcher.filter "openai MINI" items |> List.map _.model)
        Assert.Equal<string list>([ "o3"; "gpt-4o"; "gpt-4o-mini" ] |> List.sort, ModelSwitcher.filter "o" items |> List.filter (fun o -> o.providerId = "oa") |> List.map _.model |> List.sort)
        Assert.Equal("o3", (ModelSwitcher.filter "o" items |> List.head).model)
        Assert.Empty(ModelSwitcher.filter "claude" items)
        Assert.Equal(items.Length, (ModelSwitcher.filter "  " items).Length)

// 迁自旧 NativeTransportTests：预设的传输类型与真实配置层核对。
[<Fact>]
let ``每个预设的传输类型都是配置层认得的`` () =
    for preset in ProviderPresets.all do
        Assert.True(
            TomlCodec.supportedProviderKinds.Contains preset.kind,
            $"预设 {preset.key} 的 kind={preset.kind} 不被配置层接受")

// 迁自旧 OnboardingConfigTests `错误码编码解码往返，客户端类型与主机错误码一一对应`（真实探测 HTTP 的后半段留在旧仓库）。
[<Fact>]
let ``错误码编码解码往返，客户端类型与主机错误码一一对应`` () =
    for f in ProviderCatalog.ProbeFailure.all do
        let wire = ProviderCatalog.ProbeFailure.encode f "说明文字"
        let decoded, text = ProviderCatalog.ProbeFailure.decode wire
        Assert.Equal(Some f, decoded)
        Assert.Equal("说明文字", text)
        let kind, uiText = ProbeFailureKind.parse wire
        Assert.NotEqual(ProbeUnclassified, kind)
        Assert.Equal("说明文字", uiText)
        Assert.Equal(ProbeFailureKind.ofCode (ProviderCatalog.ProbeFailure.code f), kind)
    let kind, text = ProbeFailureKind.parse "HTTP 401 · 凭据被拒绝"
    Assert.Equal(ProbeUnclassified, kind)
    Assert.Equal("HTTP 401 · 凭据被拒绝", text)

// 同名模块合并：库里的 Catalog / ModelSwitcher 与本组合者的同名模块在调用方一起可见。
[<Fact>]
let ``library and composer members resolve through one module name`` () =
    Assert.False(Catalog.isReady Catalog.empty)
    Assert.Equal((VisionUnknown, VisionFromName), Catalog.visionOf "x" "my-model" Catalog.empty)
    Assert.Empty(ModelSwitcher.filter "a" (ModelSwitcher.options Catalog.empty None))
    Assert.Equal("未选择模型", ModelSwitcher.chipText "x" "" Catalog.empty)
