# RowFocus

合并包：RowFocus、SearchHit、SearchSegments、SearchSession、SidebarSelection。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。（注：SidebarEmpty 与 SearchRequery 的布尔判定已被 WorkspaceState 的 DU 状态树和事件循环取代，已退役）。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `RowFocus`

## RowFocus

侧栏行焦点（迁自旧 `Views/Sidebar.fs` 的 `visibleRowIds`、`focusedRowId`、`focusedRowIndex`、`pendingFocusId`，方法 `NeighborIndexAfterDelete`、`NeighborAfterDelete`、`FocusAfterDelete`，行 GotFocus 的记录与 `Rebuild` 的焦点还原）。公开成员：`ApplyVisible`、`Visible`、`FocusedId`、`Focused`、`NeighborIndexAfterDelete`、`NeighborAfterDelete`、`FocusAfterDelete`、`RestoreAfterRebuild`。控件聚焦与 `GetVisibleOrder` 的字符串化归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **打开谁与焦点落谁同一个答案**（契约 C3）：下一行优先，其次上一行；只剩一行时没有邻位。
2. **删除意图只用一次**：重建后优先聚焦意图行；意图行不在了就作废，不落到别处。

## SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- KeyConvergenceTests：`neighbor after delete matches the row focus falls to`、`no neighbor when the list becomes empty`（行序直接给出；原用例经真实侧栏排序）

## 与旧实现的差异
无。意图行不在了时，旧 Rebuild 仍会还原重建前正聚焦且仍可见的行，本库相同（第四个用例末段）。

## 成员 `SearchHit`

## SearchHit

全文搜索命中：解析 `search.results`、片段切分与去 Markdown、截断说明（迁自旧 `Model/ConversationSummary.fs`）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **越界高亮收敛到片段内，绝不抛异常。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- LibraryTests：`search hit parsing clamps bad highlight ranges and drops rows without ids`
- SearchHighlightTests：`消息命中片段去掉 Markdown 记号，命中词保持原样`

## 成员 `SearchRequery`

## SearchRequery（库）

旧 `Views/Sidebar.fs` 搜索防抖到点时的判定迁入：词没变就不重发查询，否则迟到的 Tick 会清空刚到的命中（旧 LibraryTests 偶发失败的根因）。计时器仍归 Sidebar。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，判定与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/searchrequery.txt`。

## 迁入或覆盖的旧测试
旧 LibraryTests 搜索用例要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `SearchSegments`

## SearchSegments（库）

旧 `Views/Sidebar.fs` 里的 `SidebarText` 模块迁入（区间合并按 SPEC 2 修正）：侧栏搜索的分词（terms）和命中分段（segments）。上色仍由侧栏视图做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **分词和分段结果与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/segments.txt` 由旧代码在 8 个查询乘 6 段文本上生成；受 SPEC 2 影响的 3 行（`存储 模型`/`存储模型的方案`、`aa`/`aaaa`、`AB`/`abAB ab`）按新口径重写，其余 53 行与旧代码逐字节相同。
2. 有意改动（只影响屏幕显示，不涉及磁盘和网络字节）：旧实现的合并 fold 写成 `(sorted, []) ||> List.fold`，状态和列表位置颠倒，从不合并，不同词的命中互相重叠时还会重复取字。现在命中区间排序后，重叠或紧邻的合并为一段命中；拼回的文本恒等于原文，每个字只出现一次。

## 迁入或覆盖的旧测试
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

## 成员 `SearchSession`

## SearchSession

消息正文搜索的会话状态（迁自旧 `Views/Sidebar.fs` 的 `searchHits`、`searchHitsQuery`、`searchPending`、`searchHasMore`、`searchTruncated`、`searchError`，方法 `BeginMessageSearch`、`LoadMoreMessageHits`、`RetryMessageSearch`、`ApplySearchResults`、`ApplySearchFailure`、`MessageHits`，`SetConnection` 断线清在途，`Rebuild` 命中区与状态行文字）。命中类型是类型参数，不认识 SearchHit。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

与 SearchRequery 的边界：SearchRequery 只回答「防抖到点要不要重发」，本库拥有会话状态；宿主把 `Query` 当作 SearchRequery 的上次查询词。防抖计时器、发请求、行控件归宿主。

## LEMMA
1. **命中只属于发起它的词。** 词变了旧命中不显示；迟到的旧词结果与失败都丢弃。
2. **在途时不重复请求「显示更多」**；断线清掉在途标志。
3. **状态行文字与旧 Rebuild 逐字相同。**

## SPEC
1. [LEMMA: 1, 2, 3] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- LibraryTests：`sidebar shows hit snippets under message content and clicking opens the hit`（状态部分；片段着色、点击打开要起窗口，留旧仓库）

## 成员 `SidebarEmpty`

## SidebarEmpty（库）

旧 `Views/Sidebar.fs` 的 Rebuild 末段迁入：搜索无结果提示、空态卡可见，以及三种空态（未连接、加载中、无对话，`SidebarEmptyKind` 加了 RequireQualifiedAccess）各自的按钮、骨架、标题与说明。卡的建法与种类翻转才重建仍归 Sidebar：调用方记住上次的 kind 自己比较。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/sidebarempty.txt`（96 种组合）。
2. 未连接优先于加载中，加载中优先于无对话；「连接主机」按钮只在窄屏出现。

## 迁入或覆盖的旧测试
旧 Sidebar 空态用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `SidebarSelection`

## SidebarSelection（库）

旧 `Views/Sidebar.fs` 的多选状态迁入，成为一个对象：`Enter`、`Toggle`、`Exit`、`ToggleAllVisible`、`SelectedIds`、`IsSelected`，以及 `Chrome`（计数、全选与置顶文案、启用、条可见）。可见行和「是否全部置顶」由调用方传入。行视觉、按钮控件仍归 Sidebar。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，选择与呈现和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/sidebarselection.txt`（19 步脚本）。
2. 清空最后一项就退出模式；全选会清掉不可见的已选项；置顶文案和批量键只看可见的已选项；计数文案用原始计数。

## 迁入或覆盖的旧测试
旧侧栏多选用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
