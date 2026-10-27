# StoreLaunch（组合）

入口处打开事件存储：`doctor --fix`（持锁、先建证据包再截断）与正常启动的只读检查（损坏时只读降级，不自动截断）。从旧 Wanxiang.App/Program.fs 拆出。协作者：EventStore、LedgerStore。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

公开面：`StoreLaunch.fix dataDir logInfo`、`StoreLaunch.fixWithMaxStamps maxStamps dataDir logInfo`、`StoreLaunch.openForStartup dataDir logInfo`、`StoreLaunch.openForStartupWithMaxStamps maxStamps dataDir logInfo`；错误是要原样打到 stderr 的整行文字，入口以 1 退出。

# LEMMA
1. **日志与错误文字、先后顺序与旧入口逐字相同。** [根 AXIOM: 5]
2. **这是协作：各协作者只做自己的事，串联次序归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] 真实组件（真实磁盘、真实 EventStore / ConfigStore）上的组合测试通过。
2. [LEMMA: 1, 2; Ledger LEMMA 6, 7] `openForStartupWithMaxStamps` 与 `fixWithMaxStamps` 透传有界幂等保留上限，无快照下淘汰旧幂等戳并重发同 `commandId` 后，重启与 `doctor --fix` 均完整重放并保持健康：`tests/Tests.fs` 的 `有界 maxStamps 启动：淘汰旧幂等戳后重启与 doctor --fix 均保持健康` 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试。本组合者的测试是新写的真实组合测试：
  - `空数据目录：正常启动，完整重放`
  - `损坏日志：只读降级启动并提示 doctor --fix，不截断`
  - `doctor --fix：先建证据包再截断，之后健康`
  - `doctor --fix：健康时什么都不做`
  - `数据目录被占用时两条路径都报 locked`
