# ComposerSendState（库）

旧 `Views/Composer.fs` 里附件与发送门控的决定迁入：`decide` 管发送键和排队键的可用、提示、标签以及看图受阻，`canSubmit` 是提交门控，`chipState` 与 `chipTip` 是附件 chip 的状态字。附件列表本身、缩略图、上传仍归 Composer 与 UiControllers；大小格式化（AttachmentRef.formatSize）、看图说明（ModelVision.blockedMessage）、能否恢复（RestoredAttachments）都由调用方传入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，决定与旧分支相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/composersendstate.txt` 覆盖 576 种发送组合、提交门控与 chip 状态字。
2. 上传中、失败残留、看图受阻都挡住发送；只有就绪附件也能发；生成中发送键变成停止键，排队键出现。
3. 已知旧行为，原样保留：`canSubmit` 把失败残留算作未就绪；排队提示不提看图受阻。

# 迁入或覆盖的旧测试
旧 Composer 与附件相关的用例都要起窗口，留在旧仓库。本库新增：金样与 7 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
