# ExportFileName（库）

旧 `Views/Brand.fs` 的 `Export.safeFileName` 原样迁入（改名 `ofTitle`）：对话标题变成安全的导出文件名（替换分隔符与控制字符、去首尾点与空白、按字素截到 220 字节、加 `.md`，空则为「对话.md」）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一标题，文件名与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/exportfilename.txt` 由旧函数在 14 个标题上生成。

# 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装）：
- `export filenames stay a bounded filename including long emoji titles`

本库新增：金样。
