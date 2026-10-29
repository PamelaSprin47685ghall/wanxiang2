module ProviderPresetsTests

open Xunit
open Wanxiang.UI

// 配置层认得的传输类型（与 AppConfig 的 supportedProviderKinds 相同；跨库核对在旧仓库的 Onboarding 用例里）。
let private supported = set [ "openai"; "anthropic"; "gemini" ]

// 迁自旧 UiPureLogicTests（ProviderPresets 部分；ProviderKind 已删除，无生产使用方）。
[<Fact>]
let ``ProviderPresets all list`` () =
    Assert.NotEmpty(ProviderPresets.all)
    Assert.True((ProviderPresets.tryFind "openai").IsSome)
    Assert.Equal("https://api.openai.com/v1", ProviderPresets.normalizeBaseUrl "https://api.openai.com/v1/")

// 迁自旧 OnboardingConfigTests（配置层核对换成固定集合）。
[<Fact>]
let ``常用 4 家置顶，原生与兼容端点合并为一个条目`` () =
    Assert.Equal<string list>([ "openai"; "claude"; "gemini"; "ollama" ], ProviderPresets.featured |> List.map _.key)
    Assert.Equal(ProviderPresets.all.Length, (ProviderPresets.featured @ ProviderPresets.more).Length)

    Assert.Empty(
        ProviderPresets.all
        |> List.filter (fun p -> p.key.EndsWith "-compat" || p.label.Contains "兼容端点（")
    )

    Assert.Equal(ProviderPresets.all.Length, ProviderPresets.all |> List.map _.key |> List.distinct |> List.length)
    let claude = ProviderPresets.tryFind "claude" |> Option.get
    Assert.Equal("anthropic", claude.kind)
    Assert.Equal(Some("openai", "https://api.anthropic.com/v1"), claude.alternate)
    let gemini = ProviderPresets.tryFind "gemini" |> Option.get
    Assert.Equal("gemini", gemini.kind)
    Assert.True(gemini.alternate.IsSome)

    Assert.Equal(
        Some "claude",
        ProviderPresets.matchByBaseUrl "https://api.anthropic.com/v1/"
        |> Option.map _.key
    )

    Assert.Equal(
        Some "gemini",
        ProviderPresets.matchByBaseUrl "https://generativelanguage.googleapis.com/v1beta/openai"
        |> Option.map _.key
    )

    for p in ProviderPresets.all do
        Assert.True(supported.Contains p.kind)

        match p.alternate with
        | Some(k, _) -> Assert.True(supported.Contains k)
        | None -> ()

// 迁自旧 NativeTransportTests。
[<Fact>]
let ``预设表里存在原生传输的选项`` () =
    let kinds =
        ProviderPresets.all |> List.map (fun p -> p.kind) |> List.distinct |> List.sort

    Assert.Contains("anthropic", kinds)
    Assert.Contains("gemini", kinds)
    Assert.Contains("openai", kinds)

[<Fact>]
let ``每个预设的传输类型都是配置层认得的`` () =
    for preset in ProviderPresets.all do
        Assert.True(supported.Contains preset.kind, $"预设 {preset.key} 的 kind={preset.kind} 不被配置层接受")

[<Fact>]
let ``presetPayload 对空模型清单不崩溃：custom 预设没有默认模型可猜`` () =
    // custom 预设的 models = []（用户只填了地址与密钥，模型名等探测回来再定）。
    // presetPayload 若对空清单 List.head，会把整个桌面进程带崩（ArgumentException: 输入列表为空）。
    let custom = ProviderPresets.tryFind "custom" |> Option.get
    Assert.Empty(custom.models)

    // 引导卡走 custom 时传入空模型清单：应安全构造载荷，defaultModel 留空，不抛异常。
    let payload = ProviderPresets.presetPayload custom "provider" (Some "sk-test") []
    Assert.Equal("", payload["defaultModel"].ToString())
    Assert.Equal("[]", payload["models"].ToJsonString())
