# LaunchPlan（组合）

启动计划：命令行 + 主机 TOML 的 [runtime] 开关 → 路径与模式。从旧 Program.fs 拆出。协作者：CliArgs、RunModes、AppConfig。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

公开面：`LaunchPlan`、`LaunchPlan.tomlSwitches`、`LaunchPlan.ofOptions options home`。

# LEMMA
1. **日志与错误文字、先后顺序与旧入口逐字相同。** [根 AXIOM: 5]
2. **这是协作：各协作者只做自己的事，串联次序归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] 真实组件（真实磁盘、真实 EventStore / ConfigStore）上的组合测试通过：`tests/Tests.fs` 的 `没有 TOML 时用程序默认`、`TOML 的 runtime 开关生效，命令行覆盖它`、`TOML 解析失败时退回程序默认，不抛`。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试。本组合者的测试是新写的真实组合测试：
  - `没有 TOML 时用程序默认`
  - `TOML 的 runtime 开关生效，命令行覆盖它`
  - `TOML 解析失败时退回程序默认，不抛`
