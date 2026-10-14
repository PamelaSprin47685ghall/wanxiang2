# BoundedCache

带上限的线程安全记忆化（从旧 RichBackend.Cache 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`BoundedCache<'K,'V>(limit)`：`TryGet`、`Set`、`GetOrAdd`、`Clear`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **类型从 `RichBackend.Cache` 改名为 `BoundedCache`：`RichBackend` 模块（后端注册）留在界面。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
迁自旧 UiPureLogicTests（RichBackendAndKatexHtmlTests）（原文，只改名字）与本库补充的测试：
- `RichBackend Cache stores and evicts by capacity`
- `GetOrAdd 命中时不再计算`
- `Clear 清空全部`
- `并发写不越过上限也不抛`
