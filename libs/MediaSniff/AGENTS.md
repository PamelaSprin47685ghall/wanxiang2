# MediaSniff

文件头嗅探、媒体类型取舍和文件名清理。不碰文件系统。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`MediaSniff.sniff`、`resolve`、`sanitizeFileName`。

# LEMMA
1. **只看前 512 字节的魔数和字符种类。** [根 AXIOM: 3]
    - 推理：沿用旧决策 Q176，够用就行，不引入完整的类型库。
2. **声明值优先；只有声明为空或 octet-stream 时才采用嗅探结果。** [根 AXIOM: 5]
3. **文件名只用于显示，绝不用作路径。** [根 AXIOM: 4]

# SPEC
1. [LEMMA: 1, 2, 3] `tests/Tests.fs`，通过。
