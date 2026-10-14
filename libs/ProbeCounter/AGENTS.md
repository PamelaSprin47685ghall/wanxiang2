# ProbeCounter

生命周期一致性探针（决策 450）的命中计数：`ProbeCounter`，`Enabled` 开关、`Hit`（返回是否首次命中）、`Count`、`Reset`，迁自旧 `Model/ParityProbe.fs` 的计数部分。打开条件（`WANXIANG_PARITY_PROBE=1` / 页面 `wxprobe=1`）与输出（首次命中写 stderr `parity.<名字>`；浏览器 `BrowserProbe.Hit`）碰平台，归宿主：宿主持有一个 ProbeCounter，`ParityProbe.hit` 调 `Hit` 后按返回值输出；旧 `forceInProcess` 对应设 `Enabled`。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **计数与旧代码一致。** [根 AXIOM: 1] 同一串开关、命中、清零之后的计数与旧 ParityProbe 相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧程序集（forceInProcess 打开进程内计数）跑 `tests/Render.fs` 生成；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- R28PwaParityTests：`probe counts only when enabled`
