# Canonical

RFC 8785 JSON 规范化（JCS）与 SHA-256 内容哈希。纯函数，无状态，所以不是对象（根《适用范围》）。不依赖任何兄弟库。根规则见项目根目录 AGENTS.md，本文只写本库特有的部分。

公开面：`Jcs.utf8 : string -> byte[]`、`Jcs.text : string -> string`、`Jcs.sha256Hex : string -> string`。
使用方：旧仓库用它算幂等戳的 hash（`Ledger.Stamp.hash` 的取值由调用方决定，本库不认识 Ledger）。

# LEMMA
1. **「两段 JSON 是不是同一件事」只有一种判法：比较规范化后的字节。** [根 AXIOM: 1, 2]
    - 推理：幂等、去重、完整性校验都要回答这个问题；判法有多种，同一请求就会在不同地方得到不同结论，用户看到的是重复提交或误拒。RFC 8785 是现成标准，自造会多一处要证明的东西。
2. **超出 IEEE-754 double 精确范围的整数，调用方须用 JSON 字符串承载。** [LEMMA: 1; 根 AXIOM: 5]
    - 推理：RFC 8785 按 double 序列化数字，大整数会被静默改写（RFC 8785 附录 D）；哈希仍稳定，语义已变。本库判断不了数字的含义，只能把约束写明。

# SPEC
1. [LEMMA: 1] 键序、空白、等值数字写法不同的 JSON 得到相同的 `Jcs.text` 与 `Jcs.sha256Hex`。
    - 证明：`tests/Tests.fs` `key order and whitespace do not change canonical form or hash`，通过。
2. [LEMMA: 1] 键按 UTF-16 码元排序，与 RFC 8785 §3.2.3 样例一致。
    - 证明：`RFC 8785 sorting example`，通过。
3. [LEMMA: 1] `sha256Hex` 输出 64 位小写十六进制，`{}` 的值固定。
    - 证明：`hash is 64 lowercase hex chars`，通过。
4. [LEMMA: 2] [TODO] 大整数被改写这件事没有测试锁住；目前只是文档约束。
