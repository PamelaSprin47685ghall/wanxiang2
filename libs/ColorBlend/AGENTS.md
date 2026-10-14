# ColorBlend（库）

旧 `Controls/Interaction.fs` 的 `blendOver` 的分量计算迁入：`over`，颜色以 `(A, R, G, B)` 字节四元组表示。不是纯色画笔时原样返回底色的分支仍归 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一对颜色，结果与旧实现逐分量相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/colorblend.txt` 由照抄的旧计算在 20 对颜色上生成。

# 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、不透明覆盖保留底色 alpha、全透明不变。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
