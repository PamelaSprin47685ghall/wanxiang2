# Canonical

RFC 8785 JSON 规范化（JCS）与 SHA-256 内容哈希。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

# LEMMA
1. [AXIOM: 1, 7] 「两段 JSON 是不是同一件事」只有一种判法：比较规范化后的字节。
    - 推理：幂等、去重、完整性校验都要回答这个问题；判法若有多种，同一请求会被不同地方判成不同结果，用户看到的就是重复提交或误拒。RFC 8785 是现成标准，自造会多一处要证明的东西。
2. [LEMMA: 1] 超出 IEEE-754 double 精确范围的整数，调用方必须用 JSON 字符串承载。
    - 推理：RFC 8785 按 double 序列化数字，大整数会被静默改写（RFC 8785 附录 D），哈希仍然稳定，但语义已经变了。本库无法替调用方判断数字的含义，只能把约束写在这里。

# SPEC
1. [LEMMA: 1] 键序、空白、等值数字写法不同的 JSON 得到相同的 `Jcs.text` 与 `Jcs.sha256Hex`。
    - 证明：tests/Tests.fs `key order and whitespace ...`。
2. [LEMMA: 1] 键按 UTF-16 码元排序，与 RFC 8785 §3.2.3 样例一致。
    - 证明：`RFC 8785 sorting example`。
3. [LEMMA: 1] `sha256Hex` 输出 64 位小写十六进制，`{}` 的值固定。
    - 证明：`hash is 64 lowercase hex chars`。
