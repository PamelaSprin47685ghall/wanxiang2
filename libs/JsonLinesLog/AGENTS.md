# JsonLinesLog

结构化 JSON Lines 日志：公共字段 level、event、utc 和可选的 instanceId；已登记的密钥原值会被精确替换成 ***；最近的错误行保存在有上限的缓冲里。不懂具体事件。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`JsonLinesLog(emit, now, recentLimit)`，成员 ApplyInstanceId、RegisterSecrets、ClearSecrets、Redact、Error、Info、RecentErrors。

# LEMMA
1. **只按已知的值精确替换，不猜测打码。** [根 AXIOM: 4, 5]
    - 推理：stderr 是现场的最后证据，必须如实记录；只有密钥绝对不能出现（旧决策 41/198）。
2. **长密钥先替换。** [根 AXIOM: 4]
    - 推理：短密钥是长密钥的子串时，先换短的会破坏长的匹配。
3. **输出去向和时钟由构造参数决定。** [根 AXIOM: 3]
    - 推理：这样测试能逐字节比较。

# SPEC
1. [LEMMA: 1, 2, 3] `error lines carry the envelope, typed fields and redacted text`，通过。
2. 异常带堆栈字段；info 不进最近错误；缓冲有上限：`exceptions get a stack field; info is not kept as a recent error; recent is bounded`，通过。
