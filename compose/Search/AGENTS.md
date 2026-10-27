# Search（组合）

把 Conversations（投影）、Wire（查询和结果的线上类型、预算）、TextMatch 和 MessageText 组合成旧 `Wanxiang.Server.SearchService`。命名空间和公开面与旧代码相同；`search` 改成对 `Projection<'C>` 泛型，旧的 `Projection` 别名可以直接传入。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **结果页的线上字节与旧实现一致。** [根 AXIOM: 5]
    - 推理：命中顺序、片段、下标、hasMore、truncated 都是客户端直接使用的。
2. **只读。** [根 AXIOM: 4]
    - 推理：输入是不可变的投影，不写任何文件（旧决策 201）。

# SPEC
1. [LEMMA: 1] `golden/search.txt` 由旧 SearchService.fs 跑同一 Scenario 生成（`tests/GoldenTests.fs` 的 `search result bytes equal the old implementation`），覆盖排序、大小写、删除、归档、翻页、钳制、空查询、超长查询、正文提取、片段，通过。
2. 预算触顶时明说截断，翻页不丢命中（迁自旧 LibraryTests：`tests/BudgetTests.fs` 的 `results beyond the hit budget are flagged truncated and pages never silently lose hits`），通过。
