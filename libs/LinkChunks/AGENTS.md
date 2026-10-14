# LinkChunks（库）

旧 MarkdownRenderer 的 `SafeChunk`（按字素簇切片）与 `RebalanceLinkChunks`（行首行尾禁则挪动边界标点），原样摘成函数。超长链接分段后由 WrapPanel 在段间换行。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **切片与挪动结果与旧实现逐字相同；切片不拆字素簇，拼接回原文。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/chunks.txt` 由旧代码生成（emoji 家庭序列、组合字符、中英标点）。

# 迁入或覆盖的旧测试
旧 CraftsmanshipPolishTests（去掉 Headless 包装）：
- `MarkdownRenderer.SafeChunk does not slice surrogate pairs into separate chunks`

本库新增：金样、空输入与非正长度、代理对、拼接还原、收标点前移、开标点后移、孤立标点不动。
