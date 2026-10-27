# ServerModel（组合）

旧 `ServerModel`：投影 → 线上视图（会话列表、消息、尾页、历史页、附件引用）。协作者：ChatDomain、Wire、AttachmentRefs、PlainText。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **线上视图与旧实现逐字节一致。** [根 AXIOM: 5]
2. **附件引用提取已是库 AttachmentRefs，这里只转发。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `golden/servermodel.txt` 由旧 ServerModel.fs 跑真实组合生成（`tests/GoldenTests.fs` 的 `ServerModel behaves byte for byte like the old implementation`，EventStore 写会话、消息、置顶、分叉、附件）。通过。

# 迁入或覆盖的旧测试
- 由金样覆盖，无整文件可删。ConversationExportTests.fs 引用 Avalonia Headless 与旧 Tests 辅助，留到后续阶段。
