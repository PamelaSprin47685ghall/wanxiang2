# InstanceSwitch

认证成功（auth.accepted）时壳层要做的事：`InstanceSwitch.plan 旧实例 新实例 导出框是否开着 是否浏览器 是否有令牌 是否有当前对话` 返回按旧顺序排好的 `InstanceStep` 清单；`isSwitch` 判断是否换了主机。迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `HandleEvent` 的 AuthAccepted 分支。宿主逐项执行（每一步对应旧分支里的哪几行见 `tools/OldInstanceSwitch.fs` 的行尾注）。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **步骤与次序与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由 `tools/OldInstanceSwitch.fs`（旧分支逐行摘录，副作用记成步骤）跑 `tests/Render.fs` 生成，覆盖 3 种实例变化 × 16 种组合；金标准用例通过。
2. `tools/gen-golden.fsx` 重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖该分支的单元测试；新增 `换主机时先清旧主机的状态；同一台重连不清，导出框跨重连保留`。
