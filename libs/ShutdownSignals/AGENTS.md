# ShutdownSignals

关闭信号计数：第一次优雅、之后强制（决策 101/102；从旧 Program.fs 的两份重复代码拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ShutdownStep`（RequireQualifiedAccess）、`ShutdownSignals()`、`Signal()`、`ShutdownSignals.ForceExitCode`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **对象自己拥有计数并在锁内推进；排空、退出由入口按返回的步骤去做（不注入回调）。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `第一次优雅，之后都强制`
  - `并发到达的信号恰好一次优雅`
