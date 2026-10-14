# RunModes

运行开关的优先级与模式推导（决策 50/51/60/64/291；从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`RunSwitches`、`RunModes.programDefaults`、`resolve`、`isDoctorOnly`、`runsHost`、`autoPairsDesktop`、`hostInBackground`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **推导规则与旧入口里的布尔表达式逐条相同；自动配对按 server 开关判断，不含 pwa 隐含的主机（旧行为，原样保留）。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `命令行优先于 TOML，TOML 优先于程序默认`
  - `三个都关才是只跑 doctor`
  - `pwa 隐含主机`
  - `自动配对只看 server 开关，不含 pwa 隐含的主机`
  - `主机只在有桌面时后台启动`
