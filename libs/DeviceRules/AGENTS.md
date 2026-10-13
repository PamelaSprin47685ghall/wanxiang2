# DeviceRules

设备改名与吊销的规则（从旧 WsConnection.ChangeDevice 拆出）。公开面：`DeviceChange`（RequireQualifiedAccess）、`DeviceRules.check isReservedName targetName targetIsCurrent targetRevoked change`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **错误文字与判定顺序与旧实现一致。** [根 AXIOM: 5]
2. **规则只看名字与几个布尔，独立于连接与配置。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `改名与吊销规则与旧实现一致` 通过。

# 迁入或覆盖的旧测试
- 旧设备管理测试经 ServerApp 端到端，随 8.8 列在 compose/ServerApp。
