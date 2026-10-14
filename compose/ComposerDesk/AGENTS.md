# ComposerDesk（组合者）

ComposerBox + AttachmentTray + ModelChip + BusyRing（上传中转圈，旧 Ui.spinner）+ ComposerSendState + ModelVision + ComposerDrafts（PendingAttachment、RestoredAttachments）+ MediaTypes + MessageView（AttachmentRef.formatSize）+ Theme = 旧 `Views/Composer.fs`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ComposerDesk(ComposerLooks.ofTheme manager styles, actions: ComposerDeskActions)`；`ComposerDeskActions = { Submit: string -> bool; StopGeneration; PickAttachment; RemoveAttachment: Guid -> unit; OpenModelPicker: Control -> unit; DropFiles; PasteFromClipboard: unit -> bool; OpenSessionSettings }`。
- 成员：`Host`、`Box`、`Tray`、`Chip`、`Text`、`IsGenerating`、`ModelAnchor()`；`SetAttachments`、`SetThumbnail(id, IImage)`、`HasThumbnail`、`SetShrinkNote`、`ShrinkNote`、`SetModelVision(support, label)`、`SetCanStop`、`SetGenerating`、`SetEnabled(value, reason)`、`SetModelLabel`、`SetEnterSends`、`SetCompactMode`、`SetContext((label, detail) option, canEdit)`、`SetAccessory`、`SetText`、`Focus`、`SetImeProbe`、`SetKeyHook`。
- 发送门控每次文本、附件、看图、生成、启用变化都经 `ComposerSendState.decide` 重算并 `ApplySend`；提交先过 `canSubmit`，宿主返回 true 才清空。
- 外观全部取主题活实例（ThemeManager 的笔刷、度量、间距、减弱动效开关，ThemeStyles 的表面类），常量只来自 DesignRhythm 的 LayoutPolicy / ReadingRhythm。

## 证据与旧测试
- ComposerTests：`上传中挡住发送，就绪后可以发送`、`不能看图的模型带图时挡住发送`、`附件状态字同旧 chipState`（失败残留）、`模型芯片名称固定为切换模型`、`提交走宿主动作并清空输入`
- 门控与状态字的逐分支金样在 libs/ComposerSendState。

## 与旧实现的差异
- 待发送消息面板（SetPendingMessages）、SessionConfig 摘要文案的拼装、发送历史回溯（PromptHistory）、IME 守护（ImeShield）还在宿主侧，经 SetContext / SetKeyHook / SetImeProbe 接入。
- 整棵控件树金样：tests/golden/composer.txt 由 tools/composergold 从旧 Composer（extract-ledger 533b85b）以内嵌字体渲染生成，七态（闲置、有字、附件、生成中、禁用、视觉受阻、窄屏）逐行一致。
- 未确认消息区（旧 SetPendingMessages）在 Pending.fs；根留白随自身宽度取档、窄屏固定收紧（旧 ApplyComposerPadding）。
- 门控在 Text 属性变化时同步重算；Submit() 为同步提交，宿主异常照常抛出。
