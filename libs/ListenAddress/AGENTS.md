# ListenAddress

监听地址是否只在本机回环上（从旧 ServerApp.WarnIfExposedWithoutTls 拆出）。公开面：`ListenAddress.hostOf`、`ListenAddress.isLoopback`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **判定与旧实现一致。** [根 AXIOM: 5]
2. **地址判定是纯规则；何时告警、写什么是宿主的事。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `回环判定与旧实现一致` 通过。

# 迁入或覆盖的旧测试
- 无单独旧测试（旧仓库只经 ServerApp 启动路径间接覆盖）。
