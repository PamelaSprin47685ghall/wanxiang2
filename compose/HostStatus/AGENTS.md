# HostStatus（组合）

旧 `HostStatus`：记「配置被拒」状态（截断、脱敏），在线程池上跑诊断。协作者：Wire、Doctor。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **线上事件与旧实现逐字节一致。** [根 AXIOM: 5]
2. **体量小、只是把状态和 Doctor 接到 Wire 上，没有可再拆的独立语义。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `golden/hoststatus.txt` 由旧 HostStatus.fs 生成（`tests/GoldenTests.fs` 的 `HostStatus behaves byte for byte like the old implementation`，覆盖截断/脱敏、清除、无诊断器、诊断器抛错）。通过。

# 迁入或覆盖的旧测试
- 旧仓库没有直接测它的测试。
