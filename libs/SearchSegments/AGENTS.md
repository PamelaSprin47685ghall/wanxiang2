# SearchSegments（库）

旧 `Views/Sidebar.fs` 里的 `SidebarText` 模块迁入（区间合并按 SPEC 2 修正）：侧栏搜索的分词（terms）和命中分段（segments）。上色仍由侧栏视图做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **分词和分段结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/segments.txt` 由旧代码在 8 个查询乘 6 段文本上生成；受 SPEC 2 影响的 3 行（`存储 模型`/`存储模型的方案`、`aa`/`aaaa`、`AB`/`abAB ab`）按新口径重写，其余 53 行与旧代码逐字节相同。
2. 有意改动（只影响屏幕显示，不涉及磁盘和网络字节）：旧实现的合并 fold 写成 `(sorted, []) ||> List.fold`，状态和列表位置颠倒，从不合并，不同词的命中互相重叠时还会重复取字。现在命中区间排序后，重叠或紧邻的合并为一段命中；拼回的文本恒等于原文，每个字只出现一次。

# 迁入或覆盖的旧测试
旧 SearchHighlightTests 第 1 节（去掉 Headless 包装）：
- `empty query yields one plain segment covering the whole text`
- `whitespace-only query is treated as no query`
- `single term highlights only the matched substring, keeping original casing`
- `segments always reassemble the original text exactly`
- `every term that matched the row is highlighted`（去掉了对 ConversationSummary.matches 的断言，那是写手 2 的模型；期望改为一段 `模型选型`）
- `adjacent matches cover the whole text without gaps`（期望由两段改为一段）
- `overlapping occurrences of one term are not double counted`（期望由两段命中改为一段）
- `a term that does not occur leaves the text unstyled`
- `terms helper splits on spaces and tabs and drops duplicates`

本库新增：金样、null 文本、不同词重叠不重复取字、被包含的命中并入、分离的命中保持分离。
