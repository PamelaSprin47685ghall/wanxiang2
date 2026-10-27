# ChatDomain

组合者：把会话领域（Conversations）、线协议（Wire）、幂等标识（CommandId）、规范化（Canonical）组装成旧仓库的 `Wanxiang.Core`。命名空间和 API 与旧仓库一致，只依赖这四个交付包。根规则见项目根目录 AGENTS.md。

内容：`Constants`、`WanxiangError`；具体类型名 `EventData`、`Conversation`、`Projection`（配置定为 `SessionConfig`）；`Events.Commit`（提交外壳）；`CommitCodec`（提交行，磁盘和线上共用）；`Projection`、`ProjectionCodec`（转调）；`CommandEngine`（`plan`、`planStamped`、`canonicalPayload`、`trailingModelMessages`）。

# LEMMA
1. **命令规划在这里，因为它同时要用线协议命令和领域投影。** [根 AXIOM: 3]
    - 推理：`CommandEngine` 读 `ClientCommand`，查 `Projection`，产出事件，再用 JCS 和 CommandId 算幂等标识。四个参与者各自独立，规划就是把它们连起来，正好是组合者该做的事。
2. **提交行、快照正文、commandId 与旧实现逐字节相同。** [根 AXIOM: 1]
    - 推理：用户磁盘上已有的日志、快照和幂等表都要能接着用，旧版本的客户端也要能读提交行。金样由旧仓库代码生成，不是由新代码生成后再钉住。
3. **`WanxiangError.ConversationDeleted` 和事件 `ConversationDeleted` 同名，事件要写 `EventData.ConversationDeleted`。** [根 AXIOM: 4]
    - 推理：两个同名 case 现在来自不同程序集，不加限定时解析结果取决于引用顺序。旧仓库本来就写成限定形式，这里沿用。

# SPEC
1. [LEMMA: 2] 提交行、快照正文、commandId 与旧仓库 `extract-ledger@1a1a4d7` 生成的金样逐字节相同：`tests/CompositionTests.fs` 的 `a conversation lives through create, send, retry, fork, regenerate and replays to the same state`、`same command id with a different payload is a conflict, not a replay`、`stale client cursor is rejected with the required watermark`  中的三个金样测试，通过。
2. [LEMMA: 1] 真实组合：命令经规划、编码成提交行、解码、折叠；重试得到原提交；从文本重放与在线折叠逐字节相同；游标陈旧时被拒绝：`CompositionTests`，通过。
3. [LEMMA: 1, 2] 旧仓库 `CoreTests`、`CodecTests`、`ForkTests` 原样搬来；`ProjectionInvariantsTests` 搬来不涉及存储的部分，并加了每一步快照往返的测试；全部通过，共 45 个。
