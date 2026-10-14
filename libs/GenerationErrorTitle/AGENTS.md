# GenerationErrorTitle（库）

旧 `Views/MessageCard.fs` 的 `errorTitle` 迁入（改名 `ofMessage`，参数由 GenerationError 改为它的 message 字符串）：原始异常文本降级为固定标题「生成失败」。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一 message，标题与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationerrortitle.txt` 由旧函数（外包一个只有 message 的记录）生成。

# 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、异常文本降级、人话去首尾空白。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
