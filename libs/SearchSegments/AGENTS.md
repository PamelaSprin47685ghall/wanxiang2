# SearchSegments（库）

旧 `Views/Sidebar.fs` 里的 `SidebarText` 模块原样迁入：侧栏搜索的分词（terms）和命中分段（segments）。上色仍由侧栏视图做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **分词和分段结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/segments.txt` 由旧代码在 8 个查询乘 6 段文本上生成。
2. 已知旧行为，原样保留：区间合并的 fold 写成了 `(sorted, []) ||> List.fold`，状态和列表位置颠倒，实际不做任何合并；相邻命中是两段（旧测试也这样断言）。两个词的命中区间互相重叠时会重复取字，暂未修。

# 迁入或覆盖的旧测试
旧 SearchHighlightTests 第 1 节（去掉 Headless 包装）：
- `empty query yields one plain segment covering the whole text`
- `whitespace-only query is treated as no query`
- `single term highlights only the matched substring, keeping original casing`
- `segments always reassemble the original text exactly`
- `every term that matched the row is highlighted`（去掉了对 ConversationSummary.matches 的断言，那是写手 2 的模型）
- `adjacent matches cover the whole text without gaps`
- `overlapping occurrences of one term are not double counted`
- `a term that does not occur leaves the text unstyled`
- `terms helper splits on spaces and tabs and drops duplicates`

本库新增：金样、null 文本。
