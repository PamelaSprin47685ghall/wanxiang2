# RefreshingPool

按键缓存长寿对象，配置指纹变了就停旧建新，不再需要就停掉（从旧 ToolRegistry 的 MCP 客户端缓存拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：配置热更新后新调用立即用新配置，旧对象不留孤儿（旧决策 98）。

# SPEC
1. [LEMMA: 1] 测试覆盖复用、替换、移除、Prune、StopAll。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/ToolRegistry/tests（逐条列在 ToolRegistry AGENTS.md）。
