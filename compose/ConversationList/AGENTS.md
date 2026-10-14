# ConversationList（组合者）

对话列表行（ConversationSummary）与分叉谱系（ForkLineage）、待删批次（PendingDeletion）的关联：`ForkLineage.breadcrumb`、`ForkLineage.siblings`、`PendingDeletion.visible`。模块名与库里相同，F# 按名合并同名模块，旧调用方不用改。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# SPEC
1. 真实组合：测试用 ChatDomain 投影 + ServerModel 生成真实的列表快照，再经三个库的交付包解析、对照（`tests/Tests.fs`）。

# 迁入或覆盖的旧测试
- LibraryTests：`list snapshot carries fork lineage derived from existing facts`、`breadcrumb names the parent and falls back to deleted-original text`、`three versions of one message show one-of-three and switching lands on the matching conversation`、`editing the first message makes siblings at the start of the conversation`、`when the original conversation is deleted the fork has no switch control`
