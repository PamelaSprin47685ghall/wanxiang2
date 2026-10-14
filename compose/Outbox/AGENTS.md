# Outbox（组合者）

发件箱（DeliveryState、PendingMessage、MessageOutbox）、按命令登记的确认台账（`CommandExpiry.expirable`，以及 CommandFeedbackTracker 的扩展成员 `Track(cmd, …)`、`TrackWithCompletion(cmd, …)`）、草稿内存 ⇄ 快照（`DraftPolicy.snapshotOf`、`restoreInto`），迁自旧 `Model/UiControllers.fs` 与 `Model/DraftStore.fs`。协作者：Wire（ClientCommand）、ComposerDrafts、CommandFeedback、DraftStore。DraftPolicy 与 DraftStore 库的同名模块合并。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# SPEC
1. 旧用例原样迁入，经四个库的交付包编译运行；重试命令载荷用 Wire 的 WireCodec 逐字比较（`tests/Tests.fs`）。

# 迁入或覆盖的旧测试
- ComposerReliabilityTests：`a send whose conversation was never confirmed falls back into the start draft`、`an unconfirmed send comes back as retryable with the original invocation id`、`drafts are isolated per conversation and per host`、`saving while connected to one host keeps another host's stored drafts`
- R27HostCapabilitiesTests：`command tracker expires newer commands but leaves message delivery to outbox`（真实命令；CommandFeedback 库里另有按 id 的同名用例）
