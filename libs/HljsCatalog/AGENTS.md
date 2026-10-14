# HljsCatalog

hljs 语言目录：解析目录 JSON、把语言标注解析成规范名、判断是否要按需加载（从旧 RichAssets 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`HljsCatalog`、`HljsCatalog.empty`、`parse`、`resolve`、`requiresLoad`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **目录文本由调用方给出；本库不读资源。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
本库测试（新写，边界与失败路径）：
- `别名优先，再认基座语言，大小写与空白不敏感`
- `按需语言只经别名到达（与旧实现相同）`
- `空白与未知标注为 None`
- `目录格式不对时退化为空目录`

另由以下覆盖：
- compose/RichAssets 迁入的 `language annotations resolve through hljs aliases`、`an unknown language annotation resolves to nothing`（用真实目录）
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）
