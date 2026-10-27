# CliArgs

合并包：CliArgs、RunModes。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `CliArgs`

## CliArgs

命令行解析与默认路径（从旧 Wanxiang.App/Program.fs 的 Cli 模块拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CliOptions`、`Cli.defaults`、`Cli.usage`、`Cli.parseBool`、`Cli.parse`、`Cli.defaultHome`、`Cli.paths`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **唯一改动：`--help`/`-h` 不再在解析中途打印并结束进程，而是返回 `help = true`，由入口打印 `Cli.usage` 并以 0 退出（库不结束进程）。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] `tests/CliArgs.Tests.fs` 通过。

## 迁入或覆盖的旧测试
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

## 成员 `RunModes`

## RunModes

运行开关的优先级与模式推导（决策 50/51/60/64/291；从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`RunSwitches`、`RunModes.programDefaults`、`resolve`、`isDoctorOnly`、`runsHost`、`autoPairsDesktop`、`hostInBackground`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **推导规则与旧入口里的布尔表达式逐条相同；自动配对按 server 开关判断，不含 pwa 隐含的主机（旧行为，原样保留）。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] `tests/RunModes.Tests.fs` 通过。

## 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `命令行优先于 TOML，TOML 优先于程序默认`
  - `三个都关才是只跑 doctor`
  - `pwa 隐含主机`
  - `自动配对只看 server 开关，不含 pwa 隐含的主机`
  - `主机只在有桌面时后台启动`
