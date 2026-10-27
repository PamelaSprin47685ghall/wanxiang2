# AttachmentTray

输入壳与附件托盘（原生 Button / TextBox / WrapPanel / ScrollViewer 实现）：含 `AttachmentTray`、`ComposerBox`、`ModelChip`。

单入口幂等投影：
- `AttachmentTray.Render(items: TrayItem list, compact: bool)`
- `ComposerBox.Render(vs: ComposerVS)`
- `ModelChip.Render(model: string option, compact: bool)`

废除各控件的 `*Look` 记录、`Follow.live` 属性订阅、`DispatcherTimer` 动画与 `Spot` 仿制件；上传中指示改由声明式动效类 `anim-spin` 与 `anim-breathe` 驱动。

## 接口（给宿主与组合者）

### AttachmentTray
- `AttachmentTray()`：构造附件托盘。
- `member Host: Control`：托盘根控件。
- `member Count: int`：附件数量。
- `member Render(items: TrayItem list, compact: bool): unit`：单入口投影，呈现事实（文件名 / 大小 / 状态 / 提示 / 失败 / 缩略图 / 缩小说明）与窄屏密度一次给全（AttachmentTray.fs:230）。
- `member Removed: IEvent<Guid>`：附件移除事件。
- `member OnRemove(f: Guid -> unit): unit`：移除回调。

### ComposerBox
- `ComposerBox()`：构造输入壳。
- `member Host: Control`：输入壳根控件。
- `member Input: TextBox`：输入文本框。
- `member SendButton: Button`：发送/停止按钮。
- `member QueueButton: Button`：排队发送按钮。
- `member AttachButton: Button`：附件按钮。
- `member ContextButton: Button`：会话上下文按钮。
- `member Text: string`：输入框当前文本。
- `member IsGenerating: bool`：当前是否处于生成回复态。
- `member Render(vs: ComposerVS): unit`：单入口幂等投影。
- 宿主注入的回调（Wire）：`member WirePaste(f: unit -> bool): unit`（剪贴板粘贴，ComposerBox.fs:393）、`member WireImeProbe(f: TextBox -> bool): unit`（输入法组合探针，ComposerBox.fs:419）、`member WireKeyHook(f: KeyEventArgs -> bool): unit`（按键钩子，ComposerBox.fs:421）。
- 控件插槽（Slot）：`member SlotTray(c: Control): unit`（附件托盘，ComposerBox.fs:396）、`member SlotModel(c: Control): unit`（模型芯片，ComposerBox.fs:402）、`member SlotPending(c: Control): unit`（未确认消息面板，ComposerBox.fs:408）、`member SlotAccessory(c: Control): unit`（附属视图，如审批卡，ComposerBox.fs:414）。
- 一次性命令（Apply）：`member ApplyDraftText(text: string): unit`（草稿回填，ComposerBox.fs:424）。
- `member Submitted: IEvent<string>`：发送意图。
- `member Stopped: IEvent<unit>`：停止意图。
- `member AttachAsked: IEvent<unit>`：选取附件意图。
- `member Dropped: IEvent<IStorageItem list>`：文件拖放意图。
- `member ContextOpened: IEvent<unit>`：上下文设置打开意图。

### ModelChip
- `ModelChip()`：构造模型选择芯片。
- `member Host: Button`：芯片原生按钮。
- `member Text: string`：当前模型名文本。
- `member Render(model: string option, compact: bool): unit`：单入口投影。
- `member ApplyLabel(text: string): unit`：写芯片文本与配色（空 / 「未选择模型」用 muted）。
- `member OnOpen(f: Control -> unit): unit`：点击打开模型切换器回调。

## 成员 `ComposerSendState`

### ComposerSendState（库）

输入与输出全面收敛为互斥 DU 代数（消灭碎布尔）：
- 输入侧：`HostAvailability`（HostAvailable / HostDisabled）、`GenerationStatus`（Idle / Generating of StopCapability）、`TextPresence`（HasText / EmptyText）、`VisionCapability`（VisionAllowed / VisionForbidden）、`SendKeyPreference`（EnterSends / CtrlEnterSends）、`ComposerAttachment`（AttachmentStage: Uploading / Ready / Failed / Restored 与 AttachmentKind: Image / OtherFile）；
- 决定侧：`SendGate`（Send / Queue / Stop / Blocked of BlockReason）、`PrimaryAction`（SendReady / SendBlocked / StopReady / StopWaiting）、`QueueAction`（QueueHidden / QueueReady / QueueBlocked）、`VisionNotice`（VisionClear / VisionNotice）；
- 派生：`canSubmit` / `chipState` / `chipTip` / `shouldSend`（Enter 8 键位组合矩阵）均从同一 DU 结构派生。
不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **非法组合在类型系统不可表示；决定与既有门控逻辑一致。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] 上传中、失败残留、看图受阻都挡住发送；只有就绪附件也能发；生成中发送键变成停止键，排队键出现：`tests/ComposerSendState.Tests.fs` 的 `uploading or failed attachments block sending`、`an image on a non-vision model blocks send with the given message`、`a ready attachment alone is enough; nothing at all is not`、`while generating the send key is stop and the queue key appears` 通过。
2. 已知旧行为，原样保留：`canSubmit` 把失败残留算作未就绪（`submit treats a failed attachment as not ready`）；排队提示不提看图受阻（`while generating the send key is stop and the queue key appears`）。
3. Enter 发送按键矩阵：`not shift && (enterSends <> ctrl)` 的全部 8 种排列经 `tests/ComposerSendState.Tests.fs` 的 `Enter sending key matrix: not shift && (enterSends <> ctrl) covers all 8 combinations` 行为测试锁定。

### 迁入或覆盖的旧测试
旧 Composer 与附件相关的用例都要起窗口，留在旧仓库。本包新增 7 条规则用例。

### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件 `ComposerSendState.fs` 落盘为本包源文件（编译序在 `ModelChip.fs` 之后）；测试按 MERGE.md《测试工程的合并约定》改名 `tests/ComposerSendState.<原文件名>.fs`，顶层模块改名 `ComposerSendState<模块名>`；原 `Wanxiang2.ComposerSendState` 包不再交付，消费方 `compose/ComposerDesk` 改引 `Wanxiang2.AttachmentTray`。命名空间与类型名不变，消费方源码零改。
