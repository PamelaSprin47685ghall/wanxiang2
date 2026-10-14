# OnboardingFlow

首次运行「添加一个模型服务」卡片的步骤机（迁自旧 `Views/OnboardingCard.fs`：`step`、`attemptId`、`connected`、`useAlternate`、`discovered`、`discoveryStarted`，以及 `connect`、`applyProbed`、`fail`、`ApplyProbe`、`TakeConnected`、`StartDiscovery`、`ShowDiscovered`、一键 Ollama、`Reset` 里的转移与状态文字）。全部原文逐字保留。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

宿主仍负责：预设选择与载荷（ProviderPresets）、默认模型挑选（OnboardingRules.pickDefaultModel）、失败原因解析（ProbeFailureKind）、保存与探测命令、目录就绪判断、控件。本库不认识这些类型。

# LEMMA
1. **只在空闲时接受新动作**（选家、切传输、连接、一键本机、复位）；忙时一律不动。
2. **探测结果只认本卡片在探测阶段发起的那一次**；失败时回到空闲并交出要撤销的 id。
3. **连接成功的 (id, 模型) 只交出一次。**
4. **发现本机 Ollama 每次显示只探一次**，复位后可再探。

# SPEC
1. [LEMMA: 1–4] `tests/Tests.fs` 通过，状态文字逐字断言。

# 迁入或覆盖的旧测试（OnboardingUiTests，步骤与文字部分）
- `mock 服务上：选预设、填密钥、点连接后模型列表自动填充且默认模型被选中`
- `密钥为空时不发起任何请求并就地说明`
- `密钥无效、路径错、不通各自给出不同的人话原因，并撤销半成品`
- `路径错时改用兼容端点后重试，载荷换成兼容传输`
- `发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`
- `没有 Ollama 时不显示一键使用`

这些旧用例同时验证 mock HTTP、载荷与控件，那部分留在旧仓库；旧仓库删用例前需确认那部分另有覆盖。

# 与旧实现的差异
- `OnboardingStep` 加了 `RequireQualifiedAccess`（Idle 等是通用名字）。
- 旧 `TakeConnected` 自己查目录；这里由宿主先查目录、确认可用再调用 `TakeConnected`。
