# ConversationTransfer

单个对话的导出与导入（决策 312/313）：导出 JSON（剔除密钥类字段）、Markdown 头部说明、导入文件解析与预览、文件名与导入标题，迁自旧 `Model/ConversationTransfer.fs`。旧 `lineageOf`（从会话列表推导分叉谱系）需要 Wire 的会话摘要，移到组合者 ConversationList；旧 `parseImport` 读 Wire 的 `ImportLimits.maxMessages`，本库改为 `parseImportWithin maxMessages`，由 ConversationList 以 `parseImport` 之名接上协议上限。导出时的 commitId 用 `uint64`。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **导出与导入字节不变。** [根 AXIOM: 1] 同样的输入经本库得到的 JSON、Markdown 头部、导入预览与错误文案与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`312 载荷里出现密钥类字段一律去掉`、`312 Markdown 头部写明来源、分叉和隐私说明`、`313 不认识的文件给出可读原因，不部分导入`、`313 导出的 JSON 能解析回预览：标题、条数、分叉说明`。

# 迁入或覆盖的旧测试
- R14TransferTests：`312 载荷里出现密钥类字段一律去掉`、`312 Markdown 头部写明来源、分叉和隐私说明`、`313 不认识的文件给出可读原因，不部分导入`、`313 导出的 JSON 能解析回预览：标题、条数、分叉说明`（导出改由 toJson 直接产出）
- `312 谱系从对话列表派生：分叉、原对话已删除、子分叉` 随 lineageOf 迁往 ConversationList
