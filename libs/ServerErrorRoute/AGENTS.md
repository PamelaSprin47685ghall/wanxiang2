# ServerErrorRoute

主机 `server.error` 的去向：缺失附件（标记 sha256）、消息记账失败（进当前对话错误卡，D9）、其余 toast，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `HandleEvent` 的 `ServerError` 分支。错误卡放不进去（正有生成占着槽位）时宿主退回 `toastText`。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。分支名 `Toast` 等较泛，加 RequireQualifiedAccess。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **去向与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`D9 消息记账失败进当前对话的错误卡，没有当前对话时 toast`、`缺失附件按小写 sha256 标记`。

# 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖该分支的单元测试；新增 `D9 消息记账失败进当前对话的错误卡，没有当前对话时 toast`、`缺失附件按小写 sha256 标记`。
