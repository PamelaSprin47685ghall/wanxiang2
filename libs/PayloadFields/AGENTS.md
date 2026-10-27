# PayloadFields

从客户端 JSON 对象里宽容地读字段（字符串、整数、小数、布尔、字符串列表、字符串表），从旧 ConfigMutation 拆出。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **「类型不对就当没给」是独立的读取规则，与配置合并无关。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `字段读取宽容`  通过；端到端由 ConfigMutation 金样钉住。

# 迁入或覆盖的旧测试
- 无单独旧测试；由 compose/ConfigMutation 金样覆盖。
