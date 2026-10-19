module OnboardingFlowTests
open Xunit
open Wanxiang.UI

// 以下用例迁自旧 OnboardingUiTests 的步骤与文字部分；mock HTTP 服务、预设载荷与控件留旧仓库。

[<Fact>]
let ``mock 服务上：选预设、填密钥、点连接后模型列表自动填充且默认模型被选中`` () =
    let f = OnboardingFlow()
    Assert.Equal(OnboardingInput.Ready, f.Check(false, "https://api.openai.com/v1", true, "sk-x"))
    Assert.True(f.Begin "openai")
    Assert.True f.IsBusy
    Assert.Equal("正在保存模型服务…", f.Status.text)
    Assert.True(f.Saved(true, "OpenAI"))
    Assert.Equal(OnboardingStep.Probing, f.Step)
    Assert.Equal("正在向 OpenAI 获取模型列表…", f.Status.text)
    Assert.Equal(OnboardingProbe.Apply "openai", f.Probed("openai", true, ""))
    f.BeginApply()
    Assert.Equal(OnboardingStep.Applying, f.Step)
    f.Applied(true, "openai", "gpt-4o", "OpenAI")
    Assert.Equal(OnboardingStep.Idle, f.Step)
    Assert.Equal("已连接 OpenAI，默认模型 gpt-4o。", f.Status.text)
    Assert.Equal(Some("openai", "gpt-4o"), f.TakeConnected())
    Assert.Equal(None, f.TakeConnected())

[<Fact>]
let ``密钥为空时不发起任何请求并就地说明`` () =
    let f = OnboardingFlow()
    Assert.Equal(OnboardingInput.MissingKey, f.Check(false, "https://x/v1", true, "  "))
    Assert.Equal({ text = "还没有填写密钥。请先粘贴 API Key，再点「连接」。"; isError = true }, f.Status)
    Assert.Equal(OnboardingInput.MissingUrl, f.Check(true, "", true, "k"))
    Assert.Equal(OnboardingStep.Idle, f.Step)

[<Fact>]
let ``密钥无效、路径错、不通各自给出不同的人话原因，并撤销半成品`` () =
    let f = OnboardingFlow()
    f.Begin "p" |> ignore
    f.Saved(true, "P") |> ignore
    Assert.Equal(OnboardingProbe.NotMine, f.Probed("other", false, "x"))
    Assert.Equal(OnboardingProbe.Failed "p", f.Probed("p", false, "密钥无效"))
    Assert.Equal({ text = "密钥无效"; isError = true }, f.Status)
    Assert.Equal(None, f.Attempt)
    Assert.False f.IsBusy
    // 回到空闲后再来的同 id 结果不归本卡片
    Assert.Equal(OnboardingProbe.NotMine, f.Probed("p", true, ""))

[<Fact>]
let ``路径错时改用兼容端点后重试，载荷换成兼容传输`` () =
    let f = OnboardingFlow()
    Assert.Equal("改用兼容端点", f.SwitchLabel)
    Assert.True(f.ToggleAlternate())
    Assert.True f.UseAlternate
    Assert.Equal("已切换传输，请再点「连接」重试。", f.Status.text)
    Assert.Equal("改回原生接口", f.SwitchLabel)
    f.Begin "p" |> ignore
    Assert.False(f.ToggleAlternate())
    Assert.False(f.Select())
    Assert.True f.UseAlternate

[<Fact>]
let ``发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`` () =
    let f = OnboardingFlow()
    Assert.True(f.StartDiscovery())
    Assert.False(f.StartDiscovery())
    Assert.True(f.ShowDiscovered(Some [ "qwen3:8b"; "llama3" ]))
    Assert.Equal("发现本机 Ollama 正在运行，有 2 个模型。", OnboardingFlow.DiscoveredText 2)
    Assert.True(f.BeginLocal "ollama")
    Assert.Equal("正在保存本机 Ollama…", f.Status.text)
    f.LocalSaved(true, "ollama", "qwen3:8b")
    Assert.Equal("已连接本机 Ollama，默认模型 qwen3:8b。", f.Status.text)
    Assert.Equal(Some("ollama", "qwen3:8b"), f.Connected)

[<Fact>]
let ``没有 Ollama 时不显示一键使用`` () =
    let f = OnboardingFlow()
    Assert.False(f.ShowDiscovered None)
    Assert.False(f.ShowDiscovered(Some []))

[<Fact>]
let ``保存失败各自说明；复位只在空闲时`` () =
    let f = OnboardingFlow()
    f.Begin "p" |> ignore
    Assert.False(f.Reset())
    Assert.False(f.Saved(false, "P"))
    Assert.Equal("保存失败：配置没有写入。请检查密钥后重试，或到设置里查看详情。", f.Status.text)
    f.Begin "p" |> ignore
    f.Saved(true, "P") |> ignore
    f.Probed("p", true, "") |> ignore
    f.BeginApply()
    f.Applied(false, "p", "m", "P")
    Assert.Equal("保存模型列表失败：配置没有写入。请重试一次，仍不行就到设置里添加。", f.Status.text)
    f.BeginLocal "o" |> ignore
    f.LocalSaved(false, "o", "m")
    Assert.Equal("保存失败：配置没有写入。请重试，或到设置里添加。", f.Status.text)
    f.StartDiscovery() |> ignore
    Assert.True(f.Reset())
    Assert.Equal("", f.Status.text)
    Assert.True(f.StartDiscovery())
