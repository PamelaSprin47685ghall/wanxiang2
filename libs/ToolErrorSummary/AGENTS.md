# ToolErrorSummary（库）

旧 `Views/MessageCard.fs` 的 `tryExtractToolError` 原样迁入：从工具结果文本里认出失败并给出一句摘要（`error:` 前缀、JSON 的 error/isError/success/status、Python traceback、shell 失败、HTTP 401/403/404、500、fatal、权限不足、命令不存在）。工具卡状态与角标仍归 MessageModel。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一段结果文本，认定与摘要和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolerrorsummary.txt` 由旧函数在 38 段结果上生成。
2. 已知旧行为，原样保留：非首行的 HTTP 状态行只认整行精确匹配（`404 Not Found extra` 不算失败）；`{"error":5}` 摘要为 `5`。

# 迁入或覆盖的旧测试
旧 ToolCallStatusTests 的两个 Theory 只迁入错误摘要部分（改名为 `no error pattern yields no summary`、`error patterns yield their summary`，数据行原样）。原用例同时断言 ToolCallStatus 的状态与角标（MessageModel），**旧用例不删**：
- `resolveToolCallStatus returns Success when result has no error pattern`（部分覆盖）
- `resolveToolCallStatus returns Failed when result matches error patterns`（部分覆盖）
- `resolveToolCallStatus extracts last line from python traceback as failure summary`（部分覆盖）

本库新增：金样、无结果、JSON 嵌套 message、traceback 末行。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
