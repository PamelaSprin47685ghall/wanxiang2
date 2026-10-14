# HostRequests

等主机回应的两类在途请求（决策 443/444、W4），迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs 的 `pendingProbes`、`latestSearchQueryId`、`searchSentAt` 三个字段及读写它们的 `ProbeProvider`、`ExpireHostRequests`、`HandleEvent`（ProviderProbeResult / SearchResults / SearchFailed）、搜索发送：
- `PendingProbes`：`Track`、`Resolve`、`Expire(now, timeout)` 取出到点的 (requestId, providerId)。
- `SearchInFlight`：`Sent`、`Accept`（只认最近一次）、`Expire`、`Latest`。
到点后的文案（`HostFeatures.noResponseMessage`）与界面回填归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **状态转移与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由 `tools/OldHostRequests.fs`（旧字段与读写原样摘录）跑 `tests/Render.fs` 生成；金标准用例通过。
2. `tools/gen-golden.fsx` 重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧仓库这几处只在整壳 UI 测试里间接经过；新增 `W4 只认最近一次查询，迟到的旧结果丢掉`、`443 到点的探测按发出顺序收尾，已回应的不再收尾`。
