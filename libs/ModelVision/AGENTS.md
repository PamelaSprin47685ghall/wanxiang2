# ModelVision

按模型名判断能否看图（VisionSupport / VisionSource），原样迁自旧 `Model/ModelVision.fs`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **宁放行不误拦。** [根 AXIOM: 6] 认不出的模型返回 Unknown，由主机和模型服务决定。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- AttachmentVisionTests：`model vision classification by name`（16 个数据行）
