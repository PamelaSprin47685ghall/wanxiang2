# CommandId

幂等命令标识：由 invocationId、命令类型和调用方给的规范化载荷算出 commandId，外加载荷哈希。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CommandId.Prefix`、`compute invocationId commandType canonicalPayload`、`sha256Hex`。

# LEMMA
1. **算法和前缀不能变。** [根 AXIOM: 1]
    - 推理：commandId 记在用户日志的幂等表里。算法一变，网络重试会被当成新命令执行第二次。
2. **规范化不归这里管。** [根 AXIOM: 3]
    - 推理：本库只对字符串做哈希。载荷怎样规范化（RFC 8785）由调用方决定，所以本库不依赖 Canonical。

# SPEC
1. [LEMMA: 1] commandId 和 sha256Hex 与用 Python hashlib 单独算出的金样一致：`tests/Tests.fs` 的 `command id matches the golden vector computed independently with Python hashlib`、`sha256Hex matches the golden vector for UTF-8 text`、`retry with the same invocation gives the same id; any changed part gives another` ，通过。
2. [LEMMA: 1] 同一 invocationId 重试得到同一个 id，改动任何一项 id 都会变；输出是 43 位无填充 base64url：`tests/Tests.fs` 的 `retry with the same invocation gives the same id; any changed part gives another`、`command id is unpadded base64url of 43 chars` 通过。
