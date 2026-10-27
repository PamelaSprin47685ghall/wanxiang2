# LocalHostGate

合并包：LocalHostGate、PwaAssets、ShutdownSignals、SingleInstance。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `LocalHostGate`

## LocalHostGate

决策 291 的同进程主机就绪门：桌面入口把「事件日志重放 + 主机启动」放到后台，窗口先画出来；客户端第一次连接前等这个门完成，不先报「连接失败」再退避重连。`LocalHostGate.ready` 是一个 `Task` 句柄，默认 `Task.CompletedTask`（PWA、纯客户端、测试都不等）。迁自旧 `Model/StartupTrace.fs` 的启动门部分（2026-10-19 包合并时并入本包）。启动感知性能打点（`StartupTrace`）不在本库，已随同批包合并迁到 `libs/ChangeWatch`。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **门默认是开的。** [根 AXIOM: 3] 不设 `ready` 的调用方（纯客户端、PWA、测试）不会被这个门挡住。

## SPEC
1. [LEMMA: 1] 桌面入口（`app/Desktop/Program.fs`）在后台起主机后把任务赋给 `LocalHostGate.ready`，客户端首次连接前 `await` 它；本包没有独立用例，行为由桌面冒烟（`tools/smoke_desktop.sh`）覆盖。

## 迁入或覆盖的旧测试
- 旧仓库没有这个门的单元测试（旧仓库宿主是同步起主机的）；本包无对应用例。

## 成员 `PwaAssets`

## PwaAssets

PWA 静态资源目录的查找顺序（从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`PwaAssets.locate appBaseDir`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **查找顺序与旧实现相同：发布包 pwa → Debug AppBundle → Release AppBundle → wwwroot。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] `tests/PwaAssets.Tests.fs` 的 `什么都没有时为 None`、`发布包内的 pwa 目录最优先`、`开发模式：Debug 产物先于 Release，先于 wwwroot`  通过。

## 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `什么都没有时为 None`
  - `发布包内的 pwa 目录最优先`
  - `开发模式：Debug 产物先于 Release，先于 wwwroot`

## 成员 `ShutdownSignals`

## ShutdownSignals

关闭信号计数：第一次优雅、之后强制（决策 101/102；从旧 Program.fs 的两份重复代码拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ShutdownStep`（RequireQualifiedAccess）、`ShutdownSignals()`、`Signal()`、`ShutdownSignals.ForceExitCode`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **对象自己拥有计数并在锁内推进；排空、退出由入口按返回的步骤去做（不注入回调）。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] `tests/ShutdownSignals.Tests.fs` 的 `第一次优雅，之后都强制`、`并发到达的信号恰好一次优雅`  通过。

## 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `第一次优雅，之后都强制`
  - `并发到达的信号恰好一次优雅`

## 成员 `SingleInstance`

## SingleInstance

决策 251：单实例转发。主实例在配置目录下监听一个本机 Unix 套接字（Windows 10+ 同样支持）；第二次启动（系统打开链接时就是这样）先连它：连上就把链接（或空行 = 只是唤起）交过去后退出，不再去抢数据锁；连不上才当主实例。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **转发成功即退出。** `tryForward` 返回 `true` 表示调用方应退出；返回 `false` 才当主实例。确认采用完整的 `ok\n` 行，允许套接字分段送达，读取总等待不超过 2 秒；断流、错误内容与超时均返回 `false`。
2. **套接字路径固定。** `socketPath configDir = Path.Combine(configDir, "ui.sock")`。

## SPEC
1. [LEMMA: 2] `tests/SingleInstance.Tests.fs` 的 `socketPath 拼接配置目录与固定文件名`、`socketPath 空目录得到相对文件名`、`socketPath 带尾分隔符的目录不重复`  覆盖 `socketPath` 的路径拼接。
2. [LEMMA: 1] `tests/SingleInstance.Tests.fs` 的 `tryForward 等待分段到达的完整确认行` 用真实 Unix 套接字覆盖分段确认；同文件用真实监听循环覆盖：`listen 收到转发的链接并回 ok；空行只当唤起；Dispose 后不再有人接`（转发链接逐字、空行只唤起不产生链接、换行被剥掉、Dispose 清掉套接字文件后 tryForward 一律 false）、`没有监听者时 tryForward 一律 false`（文件不存在与陈旧残留两种）。

## 迁入或覆盖的旧测试
无。旧仓库宿主保留单实例套接字（见根 AGENTS.md「UI 非视图余项」）；本库为 wanxiang2 新建，逻辑迁自旧宿主保留部分。
