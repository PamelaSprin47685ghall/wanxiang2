# HostCatalog

主机能力目录（`catalog.snapshot`）：ProviderInfo、ToolInfo、McpInfo、GenerationInfo、PresetInfo、Catalog 的解析，以及可用模型服务、新对话默认模型、展示名、全局默认判断、`Catalog.visionOf`（用户指定 > 模型服务信息 > 名字推断，决策 330/331；发送门控要把看图能力投影进 ComposerVS，状态库不能反向依赖组合者，故从 compose/Catalog 下沉于此），迁自旧 `Model/Catalog.fs`。对话预设（要 Wire 的 SessionConfig）、从目录列出切换选项在组合者 Catalog。命名空间沿用 `Wanxiang.UI`。只依赖 BCL（无兄弟包引用）。根规则见项目根目录 AGENTS.md。

## 看图判断（ModelVision，已并入本库）

按模型名判断能否看图（`VisionSupport` / `VisionSource`，模块 `ModelVision`），原样迁自旧 `Model/ModelVision.fs`，原为独立库 ModelVision。2026-10-20（W7-B2）按包合并先例物理并入本库：`ModelVision.fs` 落盘为本库源文件（编译序在 `HostCatalog.fs` 之前——`Catalog.visionOf` 要用 `ModelVision.classify`），原库目录（含其测试工程）已删除，`Wanxiang2.ModelVision` 包不再交付。

- 命名空间与类型名不变：仍是 `Wanxiang.UI` 下的 `ModelVision` 模块与 `VisionSupport` / `VisionSource` 类型，消费方源码零改（`ModelVision.blockedMessage`、`VisionYes` / `VisionNo` / `VisionUnknown`、`VisionFromUser` / `VisionFromProvider` / `VisionFromName` 照旧解析）。
- 交付方式变了：消费方改为引用 `Wanxiang2.HostCatalog`（本包），不再引用 `Wanxiang2.ModelVision`。消费方：`libs/WorkspaceState`（状态树投影看图判定）、`compose/Catalog`、`compose/ComposerDesk`、`compose/SettingsScreen`。
- 并入理由：`Catalog.visionOf` 的名字推断分支要用 `ModelVision.classify`，两者同属「模型能不能看图」这一件事；拆成两个包就多出一条兄弟依赖边（根 AGENTS.md《布局与工具》要求库之间零依赖边），并库后 HostCatalog 仍是零兄弟依赖。

# LEMMA
1. **目录解析与旧代码一致。** [根 AXIOM: 1] 同一份线上目录读出的记录、默认选择与展示名与旧程序集相同。
2. **宁放行不误拦。** [根 AXIOM: 6] 认不出的模型返回 Unknown，由主机和模型服务决定。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`empty catalog defaults`、`parse and query catalog properties`、`probe models keep string items only, like old AppShell`。
2. [LEMMA: 2] `tests/ModelVision.Tests.fs` 通过：`model vision classification by name`（16 个数据行）与 `blocked message names the model and both ways out`。

# 迁入或覆盖的旧测试
- UiPureLogicTests：`empty catalog defaults`、`parse and query catalog properties`
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。
- AttachmentVisionTests：`model vision classification by name`（16 个数据行）、`blocked message names the model and both ways out`——随原 ModelVision 库并入本库，落在 `tests/ModelVision.Tests.fs`（顶层模块改名 `ModelVisionTests`，与既有 `Tests` 模块不撞名）。
