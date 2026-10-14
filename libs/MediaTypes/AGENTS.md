# MediaTypes

由文件名推断 MIME、附件加入前检查、选择器筛选项，原样迁自旧 `Model/MediaTypes.fs`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **拒绝时说清问题与出路。** [根 AXIOM: 5] 错误文字与旧实现一致。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- AttachmentVisionTests：`validate rejects unsupported image formats and oversize images with guidance`
