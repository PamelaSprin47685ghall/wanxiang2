# HostLog（组合）

把 JsonLinesLog 和 ChatDomain（提交与错误）组合成旧 `Wanxiang.Server.Stderr` 模块：进程内唯一的一份 stderr 日志（最近错误上限 200），以及两种截尾记录。公开面与旧代码相同。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行格式与旧实现一致。** [根 AXIOM: 5]
    - 推理：support bundle 和运维脚本会读这些行。
2. **进程内只有一份。** [根 AXIOM: 3]
    - 推理：旧调用点都直接调用模块函数。

# SPEC
1. [LEMMA: 1] 运行时截尾记录带齐决策 40 的字段，原始提交已脱敏：`tests/Tests.fs` 的 `runtime truncation record carries the decision-40 fields with the raw commit redacted` ，通过。
2. [LEMMA: 1] 通用行格式由 `compose/Attachments` 的金样（与旧 Stderr.fs 对比）覆盖。
