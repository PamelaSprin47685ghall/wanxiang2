# LocalHostGate

合并包：LocalHostGate、PwaAssets、ShutdownSignals、SingleInstance。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `LocalHostGate`

## StartupTrace

启动感知性能打点（决策 290）的纯部分：`StartupTrace.modeOf`（环境变量值 → 是否打点、是否采样后退出）、`StartupTrace.line`（stderr 行格式）、`StartupMilestones.Mark`（每个里程碑只记一次），迁自旧 `Model/StartupTrace.fs`。执行侧（2026-10-17 增补，迁自旧 `Model/StartupTrace.fs`）：读 `WANXIANG_STARTUP_TRACE`、进程启动时间、stderr 输出、`mark` 去重与 `requestExit` 回调都在本库的 `StartupTrace` 模块内（同名模块自然合并）；入口只安装 `requestExit`（桌面 = `Environment.Exit 0`）。`LocalHostGate` 仍是同目录独立 fsproj。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧代码一致。** [根 AXIOM: 1] 模式、去重结果与输出行格式与旧程序集相同。

## SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧程序集在 `WANXIANG_STARTUP_TRACE=exit` 下跑 `tests/Render.fs` 生成（毫秒数换成 N）；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`；重跑后 `tests/golden` 逐字节不变。

## 迁入或覆盖的旧测试
- 旧仓库没有 StartupTrace 的单元测试（只有 tools/perf_desktop.sh 采样）；本库由金标准覆盖，另有新用例 `290 未设置环境变量时不打点；值为 exit 时打点并退出`。

## 成员 `PwaAssets`

## PwaAssets

PWA 静态资源目录的查找顺序（从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`PwaAssets.locate appBaseDir`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **查找顺序与旧实现相同：发布包 pwa → Debug AppBundle → Release AppBundle → wwwroot。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

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
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

## 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `第一次优雅，之后都强制`
  - `并发到达的信号恰好一次优雅`

## 成员 `SingleInstance`

## SingleInstance

决策 251：单实例转发。主实例在配置目录下监听一个本机 Unix 套接字（Windows 10+ 同样支持）；第二次启动（系统打开链接时就是这样）先连它：连上就把链接（或空行 = 只是唤起）交过去后退出，不再去抢数据锁；连不上才当主实例。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **转发成功即退出。** `tryForward` 返回 `true` 表示调用方应退出；返回 `false` 才当主实例。
2. **套接字路径固定。** `socketPath configDir = Path.Combine(configDir, "ui.sock")`。

## SPEC
1. [LEMMA: 2] `tests/Tests.fs` 覆盖 `socketPath` 的路径拼接。
2. [LEMMA: 1] `tests/Tests.fs` 用真实 Unix 套接字与真实监听循环覆盖：`listen 收到转发的链接并回 ok；空行只当唤起；Dispose 后不再有人接`（转发链接逐字、空行只唤起不产生链接、换行被剥掉、Dispose 清掉套接字文件后 tryForward 一律 false）、`没有监听者时 tryForward 一律 false`（文件不存在与陈旧残留两种）。

## 迁入或覆盖的旧测试
无。旧仓库宿主保留单实例套接字（见根 AGENTS.md「UI 非视图余项」）；本库为 wanxiang2 新建，逻辑迁自旧宿主保留部分。
