# DesktopPairing（组合）

主机与桌面同进程时本机桌面自动配对（决策 64 + Q190）。从旧 Program.fs 拆出。协作者：ConfigStore、AppConfig、ClientToken、DesktopClient、ClientTomlFile。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

公开面：`DesktopPairing.ensure configPath`、`DesktopPairing.clientTomlOf configPath`。

# LEMMA
1. **日志与错误文字、先后顺序与旧入口逐字相同。** [根 AXIOM: 5]
2. **这是协作：各协作者只做自己的事，串联次序归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] 真实组件（真实磁盘、真实 EventStore / ConfigStore）上的组合测试通过：`tests/Tests.fs` 的 `首次启动：生成默认配置、登记桌面令牌哈希、写 client.toml`、`令牌仍有效时不改写任何文件`、`client.toml 丢失时发新令牌，旧记录保留`、`令牌被吊销时发新令牌`、`配置无效时返回错误且不写 client.toml`。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试。本组合者的测试是新写的真实组合测试：
  - `首次启动：生成默认配置、登记桌面令牌哈希、写 client.toml`
  - `令牌仍有效时不改写任何文件`
  - `client.toml 丢失时发新令牌，旧记录保留`
  - `令牌被吊销时发新令牌`
  - `配置无效时返回错误且不写 client.toml`
