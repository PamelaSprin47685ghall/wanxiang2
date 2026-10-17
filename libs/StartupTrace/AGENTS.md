# StartupTrace

启动感知性能打点（决策 290）的纯部分：`StartupTrace.modeOf`（环境变量值 → 是否打点、是否采样后退出）、`StartupTrace.line`（stderr 行格式）、`StartupMilestones.Mark`（每个里程碑只记一次），迁自旧 `Model/StartupTrace.fs`。执行侧（2026-10-17 增补，迁自旧 `Model/StartupTrace.fs`）：读 `WANXIANG_STARTUP_TRACE`、进程启动时间、stderr 输出、`mark` 去重与 `requestExit` 回调都在本库的 `StartupTrace` 模块内（同名模块自然合并）；入口只安装 `requestExit`（桌面 = `Environment.Exit 0`）。`LocalHostGate` 仍是同目录独立 fsproj。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧代码一致。** [根 AXIOM: 1] 模式、去重结果与输出行格式与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧程序集在 `WANXIANG_STARTUP_TRACE=exit` 下跑 `tests/Render.fs` 生成（毫秒数换成 N）；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧仓库没有 StartupTrace 的单元测试（只有 tools/perf_desktop.sh 采样）；本库由金标准覆盖，另有新用例 `290 未设置环境变量时不打点；值为 exit 时打点并退出`。
