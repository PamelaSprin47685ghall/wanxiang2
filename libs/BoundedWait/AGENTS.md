# BoundedWait

合并包：BoundedWait、HostHealth、OnboardingRules、ProviderPresets。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BoundedWait`

## BoundedWait

请求→回应的有界等待（迁自旧 `Views/SettingsDevices.fs` 的 `BeginLoad`/`NotifyLoadTimeout`/`loading`，与旧 `AppShell.fs` 「检查主机」的 30 秒超时）。计时器与界面提示仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **只有仍在等待时，超时才生效。** [根 AXIOM: 5] 回复先到则超时什么也不做；重新请求重新计时。
2. **上限与说明文字和旧代码逐字相同**（设备 8 秒，检查主机 30 秒）。

## SPEC
1. [LEMMA: 1, 2] `tests/BoundedWait.Tests.fs` 的 `device list read without a reply times out with the older host hint`、`回复先到，超时不再生效；重新请求重新计时`、`上限与旧代码相同`  通过。

## 迁入或覆盖的旧测试
- DevicesUiTests：`device list read without a reply times out with the older host hint`（等待状态与说明；原用例的窗口渲染部分留旧仓库）

## 与旧实现的差异
旧「检查主机」的超时看的是 `settings.Health.Doctor = Running`，不是独立的等待标志。改用本库时，宿主在报告到达时调用 `Settle`；两者在报告到达前后给出的结果相同。

## 成员 `HostHealth`

## HostHealth

主机可见状态：doctor 检查项解析与用户词、HostHealth 对象（配置有误、检查结果），迁自旧 `Model/HostHealth.fs`。HostHealth 自己拥有状态并发 Changed；旧的可设置回调 `RequestCheck` 是端口式注入，改为事件 `CheckRequested`（与 Changed 同一机制，对象只宣告「要检查」，谁去发请求由订阅方决定）。现状：`GeneralDesk` 订阅 `health.CheckRequested`，转成 `SettingsHooks.RequestDoctorCheck`，由 AppShell 发 `doctor.request`；报告与配置问题经 `ApplyReport` / `ApplyConfigIssue` 回填。写侧成员是 `ApplyConfigIssue` / `ApplyReport` / `ApplyUnavailable`（幂等单槽，只存最新一份）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **断线时进行中的检查转成说明，不留在「检查中」。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/HostHealth.Tests.fs` 的 `checks parse leniently and labels map protocol names`、`host health owns doctor and config-issue state`  通过。

## 迁入或覆盖的旧测试
- 无纯逻辑旧测试；ComposerReliabilityTests 里的 DoctorCheck 用例要 Avalonia，留在旧仓库。

## 成员 `OnboardingRules`

## OnboardingRules（库）

旧 `Views/OnboardingCard.fs` 的 `Onboarding` 模块迁入（改名 `OnboardingRules`）：`FirstPrompt`、`parseModelIds`（OpenAI 兼容 `/models` 响应）、`discoverOllamaAt`（本机一次短超时 GET）、`canDiscoverLocal`、`pickDefaultModel`。`pickDefaultModel` 原收 ProviderPreset，现只收它的 `models`，调用方写 `OnboardingRules.pickDefaultModel preset.models probed`。`selectablePresets` 依赖 ProviderPresets，留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一输入，解析、判断与选择与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/OnboardingRules.Tests.fs` 通过：`默认模型从探测清单里挑：优先预设推荐的，否则清单第一项`、`发现本机 Ollama：有响应给出模型清单，没有响应为 None`、`只有主机在本机时才探测本机端口`、`non success status is no discovery`。

## 迁入或覆盖的旧测试
旧 OnboardingConfigTests：
- `默认模型从探测清单里挑：优先预设推荐的，否则清单第一项`（预设改为直接给 OpenAI 预设的模型表）
- `发现本机 Ollama：有响应给出模型清单，没有响应为 None`（本机端点改用 HttpListener）
- `只有主机在本机时才探测本机端口`（部分覆盖：`ProviderPresets.OllamaProbeUrl` 那一行属于写手 2 的 ProviderPresets，**旧用例不删**）

本库新增：非 2xx 视为没有。

## 成员 `ProviderPresets`

## ProviderPresets

模型服务预设表、按端点反查、端点归一化（迁自旧 `Model/ProviderPresets.fs`）。旧的 ProviderKind 与 kindOf 没有生产使用方，按 LEMMA 8 删除，不迁。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **每个预设的传输类型都被配置层接受。** [根 AXIOM: 1] 本库不引用 AppConfig，测试用同一固定集合核对；与真实配置层 `TomlCodec.supportedProviderKinds` 的核对在组合者 Catalog。
2. **预设载荷构造不猜不存在的模型名。** [根 AXIOM: 1, 5; 根 SPEC: 6] custom 预设没有预设模型清单（模型名要等探测回来再定）；`presetPayload` 对空模型清单把 `defaultModel` 留空，不 `List.head` 空列表把进程带崩（2026-10-29 实测崩溃：引导卡选「自定义地址」点「连接」，`ArgumentException: 输入列表为空` 未捕获整进程退出）。端到端回归（真实 OnboardingDesk + chip 点击 + 连接）在组合者 SettingsScreen。

## SPEC
1. [LEMMA: 1] `tests/ProviderPresets.Tests.fs` 的 `ProviderPresets all list`、`常用 4 家置顶，原生与兼容端点合并为一个条目`、`预设表里存在原生传输的选项`  通过。
2. [LEMMA: 2] `tests/ProviderPresets.Tests.fs` 的 `presetPayload 对空模型清单不崩溃：custom 预设没有默认模型可猜` 通过；端到端回归（真实 OnboardingDesk + chip 点击 + 连接）在组合者 SettingsScreen 的 `compose/SettingsScreen/tests/Tests.fs`。

## 迁入或覆盖的旧测试
- UiPureLogicTests：`ProviderKind conversions and ProviderPresets all list`（只迁 ProviderPresets 部分，测试名改为 `ProviderPresets all list`）
- OnboardingConfigTests：`常用 4 家置顶，原生与兼容端点合并为一个条目`
- NativeTransportTests：`预设表里存在原生传输的选项`（`每个预设的传输类型都是配置层认得的` 迁到组合者 Catalog）
