# DeviceListJson

设备列表里一台设备的线上 JSON 形状（从旧 WsConnection.SendDevices 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`DeviceListJson.item id name createdAt lastSeen revoked online current desktop : JsonObject`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `设备条目形状与旧实现一致` 通过；端到端由 compose/ServerApp 迁入的 DevicesTests钉住。

# 迁入或覆盖的旧测试
- 见 compose/ServerApp 迁入的 DevicesTests。
