# OnboardingRules（库）

旧 `Views/OnboardingCard.fs` 的 `Onboarding` 模块迁入（改名 `OnboardingRules`）：`FirstPrompt`、`parseModelIds`（OpenAI 兼容 `/models` 响应）、`discoverOllamaAt`（本机一次短超时 GET）、`canDiscoverLocal`、`pickDefaultModel`。`pickDefaultModel` 原收 ProviderPreset，现只收它的 `models`，调用方写 `OnboardingRules.pickDefaultModel preset.models probed`。`selectablePresets` 依赖 ProviderPresets，留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一输入，解析、判断与选择与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/onboardingrules.txt` 由旧函数生成（发现本机端口不进金样，由真实 HTTP 用例覆盖）。

# 迁入或覆盖的旧测试
旧 OnboardingConfigTests：
- `默认模型从探测清单里挑：优先预设推荐的，否则清单第一项`（预设改为直接给 OpenAI 预设的模型表）
- `发现本机 Ollama：有响应给出模型清单，没有响应为 None`（本机端点改用 HttpListener）
- `只有主机在本机时才探测本机端口`（部分覆盖：`ProviderPresets.OllamaProbeUrl` 那一行属于写手 2 的 ProviderPresets，**旧用例不删**）

本库新增：金样、非 2xx 视为没有。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
