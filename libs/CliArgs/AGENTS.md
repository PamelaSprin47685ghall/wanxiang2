# CliArgs

命令行解析与默认路径（从旧 Wanxiang.App/Program.fs 的 Cli 模块拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CliOptions`、`Cli.defaults`、`Cli.usage`、`Cli.parseBool`、`Cli.parse`、`Cli.defaultHome`、`Cli.paths`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **唯一改动：`--help`/`-h` 不再在解析中途打印并结束进程，而是返回 `help = true`，由入口打印 `Cli.usage` 并以 0 退出（库不结束进程）。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `没有参数时全是默认`
  - `等号与空格两种写法都支持`
  - `开关：裸写为真，等号后按布尔词解析，认不出的词为 None`
  - `后出现的同名参数覆盖前面的`
  - `wanxiang 链接不区分大小写，作为 openUrl`
  - `未知参数与缺值抛出，文字与旧实现相同`
  - `help 立即返回，后面的参数不再解析`
  - `路径：默认配置在 home 下，数据目录在配置旁`
  - `WANXIANG_HOME 优先，空白视为未设置`
