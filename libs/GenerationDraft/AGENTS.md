# GenerationDraft

合并包：GenerationDraft、GenerationForm、LocalDraftControls、McpForm、OnboardingFlow、ProbeFailureKind、ProbeSlot、ProviderForm、ProviderProbeText、SectionMemory、ToolsPolicy。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `GenerationDraft`

## GenerationDraft（库）

旧 `Views/SettingsGeneral.fs` 里「生成」草稿的状态（`generationDirty`、`generationSaving`、`generationSyncing`、`generationRevision`、`generationBaseline`、`generationScope`、`draftScope`、`scopeEpoch`）与状态文案迁入，成为泛型对象 `GenerationDraft<'T>`（'T 为生成设置的值，旧代码里是 Catalog.generation）：`Edited`、`CatalogArrived`、`SetScope`、`Discard`、`CanSave`、`BeginSave`、`Finish`、`Status`。表单回填由调用方传入的 fill 完成，期间的编辑不算修改。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串事件，脏、保存中、基线、能否保存与状态文案和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationdraft.txt` 由照抄的旧分支在 31 步脚本上生成。
2. 目录更新不覆盖未保存的草稿；草稿只能保存到开始编辑时的主机；切换主机作废在途保存的确认；较早的保存确认不把之后的编辑标成已保存；保存失败返回 true，调用方弹「保存失败，请重试。」。

## 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 里的生成设置用例要操作设置页，留在旧仓库。本库新增：金样与 7 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `GenerationForm`

## GenerationForm（库）

旧 `Views/SettingsGeneral.fs` 里「生成」分组的 `floatText` 与 `SaveGenerationCore` 的校验和载荷迁入：`validate`（5 个输入框文本 → 数值或按字段的错误文案）、`instructions`、`payload`。字段联合类型 `GenerationField` 用例名是泛名，加了 RequireQualifiedAccess。错误怎么挂到控件、提交与回复仍归 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，错误与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5，线上字节不变]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationform.txt` 由照抄的旧流程在 8 组表单上生成（错误按字段次序；载荷键序 temperature、topP、maxTokens、instructions、maxContextMessages、maxToolRounds、autoTitle，未设项写 null）。
2. 数字一律按不变文化解析，小数点只认点号。

## 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 的 `generation save failure surfaces a failure toast instead of silence` 等要操作设置页，留在旧仓库。本库新增：金样、空值与必填、边界、小数点、载荷形状、指令去空白。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `LocalDraftControls`

## LocalDraftControls

设置页「本机草稿」区与外壳之间的桥（迁自旧 `Model/HostHealth.fs` 末尾）。旧的可设置回调 `ClearRequested` 是端口式注入，改为事件 `ClearRequested` 加方法 `RequestClear()`：设置页调用 `RequestClear()`，宿主把 `ClearRequested <- f` 改为 `ClearRequested.Add(fun () -> f ())`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **只在概况真的变化时通知。** [根 AXIOM: 2]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- 无旧测试。

## 成员 `McpForm`

## McpForm（库）

旧 `Views/SettingsTools.fs` 里 MCP 编辑器 saveCore 的校验与 `mcpPayload` 迁入：`validate` 返回值和按字段的错误（`McpField` 加了 RequireQualifiedAccess），`payload` 组写入载荷，`parseArgs` 切参数行。挂错误、提交与回复处理仍归 SettingsTools。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，错误次序、文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/mcpform.txt`（8400 种组合）。
2. 本地命令与远程端点必须且只能填一个，出错时两栏都标；超时为 1 到 3600 秒；已占用的标识只挡新增。

## 迁入或覆盖的旧测试
旧 SettingsTools 的用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `OnboardingFlow`

## OnboardingFlow

首次运行「添加一个模型服务」卡片的步骤机（迁自旧 `Views/OnboardingCard.fs`：`step`、`attemptId`、`connected`、`useAlternate`、`discovered`、`discoveryStarted`，以及 `connect`、`applyProbed`、`fail`、`ApplyProbe`、`TakeConnected`、`StartDiscovery`、`ShowDiscovered`、一键 Ollama、`Reset` 里的转移与状态文字）。全部原文逐字保留。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

宿主仍负责：预设选择与载荷（ProviderPresets）、默认模型挑选（OnboardingRules.pickDefaultModel）、失败原因解析（ProbeFailureKind）、保存与探测命令、目录就绪判断、控件。本库不认识这些类型。

## LEMMA
1. **只在空闲时接受新动作**（选家、切传输、连接、一键本机、复位）；忙时一律不动。
2. **探测结果只认本卡片在探测阶段发起的那一次**；失败时回到空闲并交出要撤销的 id。
3. **连接成功的 (id, 模型) 只交出一次。**
4. **发现本机 Ollama 每次显示只探一次**，复位后可再探。

## SPEC
1. [LEMMA: 1–4] `tests/Tests.fs` 通过，状态文字逐字断言。

## 迁入或覆盖的旧测试（OnboardingUiTests，步骤与文字部分）
- `mock 服务上：选预设、填密钥、点连接后模型列表自动填充且默认模型被选中`
- `密钥为空时不发起任何请求并就地说明`
- `密钥无效、路径错、不通各自给出不同的人话原因，并撤销半成品`
- `路径错时改用兼容端点后重试，载荷换成兼容传输`
- `发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`
- `没有 Ollama 时不显示一键使用`

这些旧用例同时验证 mock HTTP、载荷与控件，那部分留在旧仓库；旧仓库删用例前需确认那部分另有覆盖。

## 与旧实现的差异
- `OnboardingStep` 加了 `RequireQualifiedAccess`（Idle 等是通用名字）。
- 旧 `TakeConnected` 自己查目录；这里由宿主先查目录、确认可用再调用 `TakeConnected`。

## 成员 `ProbeFailureKind`

## ProbeFailureKind

模型探测失败的类型：解析线上 `[code] 说明`，判断是否值得建议切换传输，迁自旧 `Model/Catalog.fs`。与主机 ProviderCatalog.ProbeFailure 错误码的一一对应由组合者 Catalog 的测试核对。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **没有前缀或认不出的码一律归为 ProbeUnclassified，说明原样保留。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- OnboardingConfigTests：`错误码编码解码往返，客户端类型与主机错误码一一对应` 的旧主机部分（其余在组合者 Catalog）

## 成员 `ProbeSlot`

## ProbeSlot

模型服务探活的归属与结果缓存（迁自旧 `Views/SettingsProviders.fs` 的 `activeProbe`、`probeResults`，及编辑器打开时的模型清单回填规则）。编辑器的按钮与输入框回调、提示文字（ProviderProbeText）仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **结果只交给发起它的编辑器。** 不是当前编辑器的模型服务时不夺归属，只缓存；绝不后台自动保存（W2.5）。
2. **非空缓存优先于配置**；默认模型不在清单里时取第一项。

## SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- SettingsViewTests：`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`（归属与缓存部分；输入框回填留旧仓库）

## 成员 `ProviderForm`

## ProviderForm（库）

旧 `Views/SettingsProviders.fs` 里模型服务编辑器 saveCore 的校验段与 `isLoopbackUrl` 迁入：`validate` 返回各值、按字段的错误（`ProviderField` 加了 RequireQualifiedAccess）和自动选用的默认模型（`autoPicked`，配合 `autoPickedNote`）。端点归一（ProviderPresets.normalizeBaseUrl）、载荷、是否要密钥都属 ProviderPresets（writer 2），由调用方做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/providerform.txt`（27 种编辑和 9 个地址）。
2. 默认模型留空时自动选第一个；超时与重试留空时沿用原值，否则用 120 和 2；看图两栏只能写已列出的模型，且同一模型不能两栏都写。
3. 已知旧行为，原样保留：即使有错误，`autoPicked` 也照样给出（旧代码有错时也会回填并提示）。

## 迁入或覆盖的旧测试
旧 SettingsProviders 的用例要起窗口，留在旧仓库。本库新增：金样与 5 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `ProviderProbeText`

## ProviderProbeText（库）

旧 `Views/SettingsProviders.fs` 的看图说明（visionAutoText 的 describe）与探活回执（ApplyProbe）的文案和分支迁入：`visionLine`、`visionSummary`、`feedback`。看图判断用本库自己的 `VisionVerdict` 和 `VisionWhy`（与 ModelVision 同形，组合时映射）；错误说明先由调用方用 ProbeFailureKind.parse 解析。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，文案与分支和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/providerprobetext.txt`。
2. 编辑器打开时把模型填进草稿；没打开时只缓存，绝不自动保存；看图说明最多列 12 个模型。

## 迁入或覆盖的旧测试
旧探活用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `SectionMemory`

## SectionMemory

分区的构建缓存与滚动位置（迁自旧 `Views/SettingsView.fs` 的 `builtSections`、`scrollPositions`、`current`、`Select` 里的记位/恢复规则、`Get/SetSectionScrollOffset`）。控件、滚动容器与导航按钮仍归宿主。分区与构建结果都是类型参数，不认识 SettingsSection。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **每个分区只构建一次**，失效后才重建（重复构建会把同一控件挂到两个父级上）。
2. **切换才记位与恢复**：离开时位置大于 0 或该分区还没记过才记下；切到的分区没记过就回到顶部。

## SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
旧 SettingsViewTests 的滚动位置用例要起窗口，留旧仓库。本库新增 3 条行为用例。

## 成员 `ToolsPolicy`

## ToolsPolicy（库）

旧 `Views/SettingsTools.fs` 的 `policyLabel` 与 `toolsPayload` 迁入，原样照抄（原本就是纯函数）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolspolicy.txt`。

## 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样与 2 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
