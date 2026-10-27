# SandboxFiles

沙箱根目录内的路径解析、文本读取、目录列举（旧 `BuiltinToolsCore`，模块名保留）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型读到的错误文字不变。

# SPEC
1. [LEMMA: 1] 测试覆盖读、列、越界、未配置、空路径：`tests/Tests.fs` 的 `files inside the roots are read and listed`、`paths outside the roots, empty roots and empty paths are refused` 通过。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/ToolRegistry/tests（逐条列在 ToolRegistry AGENTS.md）。
