# ComposerDrafts

输入框草稿：附件草稿状态机（PendingAttachment、AttachmentUpload、AttachmentDraftController）、恢复引用登记（RestoredAttachments）、按主机与对话隔离的草稿表（ComposerDrafts），迁自旧 `Model/UiControllers.fs`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **还有上传中的附件时发送拒绝消费草稿；断连把上传中的翻成可移除的失败残留。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `AttachmentDraftController begin draft, complete and remove lifecycle`、`AttachmentDraftController upload and draft state transitions`、`drafts are keyed per host and conversation and moving keeps the owner key in step`  通过。

# 迁入或覆盖的旧测试
- UiControllersTests：`AttachmentDraftController begin draft, complete and remove lifecycle`
- UiPureLogicTests：`AttachmentDraftController upload and draft state transitions`
