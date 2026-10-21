# ComposerDesk（组合者）

ComposerBox + AttachmentTray + ModelChip + PendingPanel + ComposerSendState + HostCatalog（看图判断 ModelVision，已并入该库）+ ComposerDrafts（PendingAttachment、RestoredAttachments）+ MediaTypes + MessageView（AttachmentRef.formatSize）+ Theme = 旧 `Views/Composer.fs`。根规则见项目根目录 AGENTS.md。

## 接口
- 宿主入口：`ComposerDesk(actions: ComposerDeskActions)`；`ComposerDeskActions = { Submit: string -> bool; StopGeneration; PickAttachment; RemoveAttachment: Guid -> unit; OpenModelPicker: Control -> unit; DropFiles; PasteFromClipboard: unit -> bool; OpenSessionSettings }`。外观（含输入区留白）全部走主题动态资源键与样式类，不再有外观记录（`ComposerMetrics` 已随 W7 外观退役删除）。
- 成员：`Host`、`Box`、`Tray`、`Chip`、`Text`、`IsGenerating`、`ModelAnchor()`、`Pending`。
- 渲染入口：`Render(vs: ComposerVS)` —— 单入口幂等全量投影（Enabled / DisabledReason / Text / Caret / EnterSends / Send / Tray / ModelChip / Context / Compact / Vision / VisionLabel）。包内 `grep "member .*\.Set"` 归零：本组合者自己零 `Set*` 业务写入成员，转调协作者一律用新命名前缀（`box.SlotPending` / `box.SlotTray` / `box.SlotModel` / `box.WirePaste` / `box.SlotAccessory` / `box.WireImeProbe` / `box.WireKeyHook` / `box.ApplyDraftText` / `tray.Render(items, compact)` / `AutomationProperties.SetName`）。
- 例外插槽/命令（命名避开 Set*，非每帧投影）：`ApplyThumbnail`（宿主解码后的缩略图命令）、`ApplyShrinkNote`（上传管线就地生成的压缩说明）、`HasThumbnail` / `ShrinkNote`（读回）、`SlotPendingMessages`（未确认消息面板直注插槽，ComposerDesk.fs:194）、`SlotAccessory`（附属视图插槽，如审批卡，ComposerDesk.fs:199）、`ApplyDraftText`（草稿回填命令，ComposerDesk.fs:204）、`Focus`、`Submit()`（同步提交，门控通过才交宿主）、`WireImeProbe`（输入法组合探针回调，ComposerDesk.fs:208）、`WireKeyHook`（按键钩子回调，ComposerDesk.fs:209）。

## 状态归属（在途 mutable 登记）
业务事实零本地副本：12 个碎 mutable（attachments / enabled / generating / canStop / reason / enterSends / vision / visionLabel / compactMode / top / bottom / md）已清，一切从 `Render` 收到的 `ComposerVS` 投影。保留的 mutable 只有：
- `last: ComposerVS` —— 最近一帧的帧缓存，不是第二权威（门控在 TextChanged / Text 属性变化、缩略图、缩小说明、草稿回填等非每帧时机也要按最新帧重算；与 SidebarDesk.lastVS、ChatScreen.last 同一口径）。
- `paddedCompact` / `marginBinding` —— 输入区留白已套的档与当前绑定句（`None` = 还没套），只用于避免重复换绑，非业务事实；留白本身来自主题资源键。
- `thumbs` / `notes`（两个 Dictionary）—— 宿主命令在途数据：图片解码与压缩说明由宿主异步产出，VS 不承载，产出即并入 chip 呈现重渲染。
- `Pending.keys`（PendingPanel）—— 行 diff 缓存，纯呈现优化。

## 发送门控
每次文本、附件、看图、生成、启用、每帧渲染都经 `ComposerSendState.decide`（DU 代数：SendGate / HostAvailability / GenerationStatus / TextPresence / VisionCapability / SendKeyPreference / AttachmentStage）重算并 `ApplySend`。
- `generationOf`：`Send.GeneratingStop -> Generating CanStop`，其余 `Idle`。VS 表达不出 WaitingHost（工作树现状：Stop 永远可点），待 WorkspaceState 把「停止在途」入帧后一并投影（下波登记）。
- `visionOf`：`vs.Vision = VisionNo -> VisionForbidden (ModelVision.blockedMessage vs.VisionLabel)`，未知/支持放行。看图事实由 `libs/WorkspaceState` 的 Project.fs 从当前目录 + 所选模型经 `Catalog.visionOf`（已下沉 `libs/HostCatalog`）投影进 `ComposerVS.Vision/VisionLabel`——组合者不再持有看图的第二权威。
- `hostOf`：`vs.Enabled -> HostAvailable`，否则 `HostDisabled(vs.DisabledReason)`。
- `sendKey`：`vs.EnterSends -> EnterSends | CtrlEnterSends`。`EnterSends` 仍是 VS 里的碎布尔（ComposeModel 字段，改造归下一波），本组合者只做 DU 转换。
- 提交先过 `ComposerSendState.canSubmit`，宿主返回 true 才清空。

## 外观
PendingPanel 原生化为纯投影：`PendingAppearance` 记录（IObservable 字段）与 `live` 订阅助手已删除，外观全部走 DynamicResource 键（`Palette.*`、`Metric.*`、`Space.*`、`CornerRadius.RadiusMd`、`Thickness.BorderThin`、`Thickness.GapBottomMd`——后者是 ThemeManager 已有的活派生键）加 `secondary` / `button-secondary` 样式类。

输入区留白（根 Border 的 Margin / Padding）也不再订阅主题活值：主题部分走主题派生资源键 `Margin.Composer`（宽屏：顶=Space.X2l、底=Space.Xl）与 `Margin.ComposerCompact`（窄屏：左右=Space.Xl、底=Space.Md），窄屏/宽屏切换时换绑对应键；水平档随自身宽度取（`LayoutPolicy.horizontalInset`，旧 ApplyComposerPadding），落在控件的 Padding 上。

## 测试
`tests/Tests.fs` 全部以 `Render { emptyComposerVS () with ... }` 帧驱动做行为断言（上传挡发送 / 不能看图挡发送并出提示 / 模型芯片名称固定 / 失败残留 / 提交清空 / 生成中主键是停止 / 缩略图与缩小说明并入 chip / 同一帧重复渲染幂等）。整棵控件树金样及其生成器随 Set* API 退役删除——禁止金样测试，门控逐分支在 libs/AttachmentTray 有测试。

## 与旧实现的差异
- 待发送消息面板、SessionConfig 摘要拼装、IME 守护（ImeShield）在宿主/入口侧，经 `SlotPendingMessages` / `WireImeProbe` 接入。
- **发送历史召回（`PromptHistory`）由本组合者接线**（2026-10-21 恢复）：本组合者持 `PromptHistory` 实例，把 `Up`/`Down`/`Escape` 挂到 `ComposerBox` 已有的 `WireKeyHook` 钩子上（控件库不认识 `PromptHistory`）；`Record` 只在 `actions.Submit` 返回 true（已被接受）后记；`ApplyDraftText`（外部改写输入框）时 `Reset` 退出召回。该接线曾在提交 `82d5628` 里随 `compose/EntryShells/Composer.fs` 一起被删，功能静默消失；库与规则没变，只把接线放回真正的输入区所有者。回归用例见 `tests/PromptHistoryRecall.Tests.fs`（6 条）。
- `chip.Render(None)` 现在把标签重置为「未选择模型」（幂等投影），旧实现保留上一次标签；模型未选与芯片消失自此一致。
- 看图门控自此真正生效：旧 desk 的 `vision` mutable 因 `SetModelVision` 从未接线而恒为 `VisionUnknown`；现在由 VS 投影驱动，无目录（未连接）时保持未知即放行、由主机决定。
- `libs/AttachmentTray` 的有损 `Render(AttachmentVS list, compact)` 转换（丢缩略图/缩小说明）已删除；托盘唯一入口是 `Render(items: TrayItem list, compact: bool)`（items 与窄屏密度一次给全），由本组合者的 `trayItem` 投影喂全呈现事实。
