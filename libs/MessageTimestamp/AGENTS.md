# MessageTimestamp（库）

旧 `Views/MessageCard.fs` 的 `formatTimestamp` 迁入（改名 `format`）：今天只给时刻，昨天加前缀，今年给月日，更早给年月日。旧函数自己读 `DateTimeOffset.Now` 并 `ToLocalTime()`；新函数由调用方传入本地的 now 与本地化后的时刻，调用方写 `MessageTimestamp.format DateTimeOffset.Now (at.ToLocalTime())`，结果与旧实现相同。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一 now 与时刻，文案与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/messagetimestamp.txt` 由旧函数（把 Now 换成参数）在不变文化下生成。格式串沿用当前文化（`:` 是文化的时间分隔符），与旧实现一致。

# 迁入或覆盖的旧测试
旧 OperabilityTests 的 `footer timestamp carries the full timestamp for hover and screen readers` 要渲染消息卡，留在旧仓库。本库新增：金样、四档文案。
