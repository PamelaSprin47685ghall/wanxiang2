# ExchangeJump（库）

旧 `Views/ChatView.fs` 的问答跳转（上一条 / 下一条）状态机迁入，成为泛型对象 `ExchangeJump<'K>`（'K 为卡身份，旧 CardKey）。它管锚点、挂起的「上一轮」、主机上是否还有更早历史：
- `Jump` 返回 `JumpStep`（Stay、ExpandAndRetry、RequestOlder、ScrollTo）；
- `NavState` 给端点可用态；
- `Mounted` 在渲染后丢掉失效锚点，并兑现挂起的「上一轮」；
- `SetOlderAvailable`、`OlderFailed`、`Reset`；
- 静态 `Cards`、`ViewportIndex` 对应旧 exchangeIndices 与 currentExchangeIndex 的选取。

面板、卡片边界、平滑滚动、ExpandMountWindow、要更早一页的请求仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，跳转结果与状态和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/exchangejump.txt`（34 步脚本，跨两个挂载窗口）。
2. 跳过一次后跟着锚点走；贴底时没有下一轮；窗口外或主机上还有更早的，上一轮总可用；挂起的上一轮在首条提交变化时兑现，主机没有更早页或取页失败时撤掉。
3. 已知旧行为，原样保留：挂起期间再按上一轮会重新请求，并改写挂起时记下的首条提交。

# 迁入或覆盖的旧测试
旧 ChatView 跳转用例（决策 300）要起窗口，留在旧仓库。本库新增：金样与 5 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
