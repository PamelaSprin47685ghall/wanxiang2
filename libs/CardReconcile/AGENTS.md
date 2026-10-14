# CardReconcile（库）

旧 `Views/ChatView.fs` 消息卡对账（1245-1290）迁入为泛型 `Reconciler<'K, 'V>`：`Reconcile(desired)` 返回 `ReconcilePlan`（保留前缀长度 `Keep`、其后依次挂上的 `Append`），`MountedKeys`、`CachedCount`、`Clear`。共同前缀只认两边都有身份且相等的项；前缀之后按身份取缓存、未命中或无身份才调工厂；每帧丢掉不再用到的缓存。面板增删、锚点恢复、跟随判断仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **追加或刷新末尾时，前面的卡不重建。** [根 AXIOM: 5] 长对话每批流式增量不重排上百张卡。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/cardreconcile.txt`；`tests/Tests.fs` 两条行为用例。

# 迁入或覆盖的旧测试
旧对账用例都经 ChatView 起窗口（如 `ExchangeNavTests` 一组），留在旧仓库；对账规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldCardReconcile.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
