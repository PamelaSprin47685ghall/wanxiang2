# ToolArgsPreview（库）

旧 `Views/MessageCard.fs` 的 `prettyFormatJson`（改名 `prettyJson`）与 `formatToolArgsPreview`（改名 `preview`）原样迁入：工具卡上的参数一行摘要与展开后的缩进 JSON。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一段参数，摘要（键数与预览）和缩进结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolargspreview.txt` 由旧函数在 16 段参数上生成。

# 迁入或覆盖的旧测试
旧仓库没有直接测这两个私有函数的用例（MessageCardTests 经渲染间接覆盖，留在旧仓库）。本库新增：金样、空对象、96 字截断、非 JSON 原样。
