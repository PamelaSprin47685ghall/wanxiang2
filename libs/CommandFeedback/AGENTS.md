# CommandFeedback

命令的 request → committed/rejected 台账，迁自旧 `Model/UiControllers.fs`。本库按 invocationId 记账（`TrackId`、`TrackIdWithCompletion`），「由命令得出 id、消息投递类命令不在这里超时」要 Wire 的 ClientCommand，在组合者 Outbox（同名成员 `Track(cmd, …)`、`TrackWithCompletion(cmd, …)` 以扩展成员提供，旧调用方不用改）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **不可超时的命令不会被 Expire 取出。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `command tracker expires newer commands but leaves message delivery to outbox`、`commit and reject hand back the feedback once and reject all completes with false`  通过。

# 迁入或覆盖的旧测试
- R27HostCapabilitiesTests：`command tracker expires newer commands but leaves message delivery to outbox`（这里用 id；组合者 Outbox 用真实命令再核对）
