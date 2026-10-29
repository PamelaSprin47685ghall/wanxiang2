# AttachmentRefs

从消息 payload 里取出所有附件引用（旧 `ServerModel.attachmentRefsOf`）。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **结果与旧实现一致。** [根 AXIOM: 5]
    - 推理：doctor 的缺失/孤立附件判断依赖它，旧 ServerModel 将转发到这里。

# SPEC
1. [LEMMA: 1] 测试覆盖含附件与不含附件的 payload：`tests/Tests.fs` 的 `attachment objects are found at any depth with defaults for missing fields`、`nothing is found in plain messages or null` 通过。
