# ComposerSendState（库）

输入与输出全面收敛为互斥 DU 代数（消灭碎布尔）：
- 输入侧：`HostAvailability`（HostAvailable / HostDisabled）、`GenerationStatus`（Idle / Generating of StopCapability）、`TextPresence`（HasText / EmptyText）、`VisionCapability`（VisionAllowed / VisionForbidden）、`SendKeyPreference`（EnterSends / CtrlEnterSends）、`ComposerAttachment`（AttachmentStage: Uploading / Ready / Failed / Restored 与 AttachmentKind: Image / OtherFile）；
- 决定侧：`SendGate`（Send / Queue / Stop / Blocked of BlockReason）、`PrimaryAction`（SendReady / SendBlocked / StopReady / StopWaiting）、`QueueAction`（QueueHidden / QueueReady / QueueBlocked）、`VisionNotice`（VisionClear / VisionNotice）；
- 派生：`canSubmit` / `chipState` / `chipTip` / `shouldSend`（Enter 8 键位组合矩阵）均从同一 DU 结构派生。
不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **非法组合在类型系统不可表示；决定与既有门控逻辑一致。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 上传中、失败残留、看图受阻都挡住发送；只有就绪附件也能发；生成中发送键变成停止键，排队键出现。
2. 已知旧行为，原样保留：`canSubmit` 把失败残留算作未就绪；排队提示不提看图受阻。
3. Enter 发送按键矩阵：`not shift && (enterSends <> ctrl)` 的全部 8 种排列经 `shouldSend` 行为测试锁定。

# 迁入或覆盖的旧测试
旧 Composer 与附件相关的用例都要起窗口，留在旧仓库。本库新增：金样与 7 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
