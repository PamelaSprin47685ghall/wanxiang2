# BuiltinTools（组合）

旧内置工具（echo、time、file.read、file.list）的 AIFunction 与 `BuiltinTools` 模块。协作者：SandboxFiles、ToolNames、AppConfig。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **名字、描述、schema、返回文字与旧实现一致。** [根 AXIOM: 5]
2. **沙箱文件与标识换算各有独立语义，抽成库。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 由 ToolRegistry 金样钉住；本地测试 `tests/Tests.fs` 的 `file tools appear only when sandbox roots are configured` 钉目录组成。

# 迁入或覆盖的旧测试
- 旧 ProductizationTests.fs「内建工具沙箱」4 条迁在 ToolRegistry/tests/ProductizationToolTests.fs（见 ToolRegistry AGENTS.md）。
