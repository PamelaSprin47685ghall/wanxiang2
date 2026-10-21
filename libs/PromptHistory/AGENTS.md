# PromptHistory（库）

旧 `Views/Composer.fs` 里发送历史的状态（`promptHistory`、`historyIndex`、`uncommittedDraft`、上限 100）与上键、下键、Esc、Submit、SetText、Text 里的对应分支迁入，成为一个对象：`Up`、`Down`、`Escape`、`Record`、`Reset`、`Draft`、`IsRecalling`。方法返回 Some 文本时，调用方把输入框换成它、光标放末尾并把按键标为已处理。输入法组合、选区读取、焦点仍归 Composer。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串按键与输入框状态，召回结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`recall then come back restores the half typed draft`、`duplicates and blanks are not recorded and capacity drops the oldest`、`single line recalls only from an empty box or with the caret at the start`、`multi line recalls only with the caret at the very start`、`a selection or an empty history leaves the keys alone`、`down only acts while recalling and with the caret at the end`、`up stops at the oldest entry`、`escape returns to the draft and leaves recall`、`only an immediate repeat is dropped`、`default capacity keeps the latest 100`、`record and reset both end recall and forget the draft`。
2. 已知旧行为，原样保留：召回中显示的是多行文本且光标不在开头时，上键不再往前翻。

# 迁入或覆盖的旧测试
旧 CraftsmanshipPolishTests 的 `Composer prompt history records submissions and recalls with Up/Down` 要在窗口里按键，**旧用例不删**（部分覆盖）。本库新增：召回后回到半截草稿、去重去空与容量、单行/多行召回的光标位置规则、上下键边界、Esc 退出召回、容量默认 100。

# 真实行为测试
`tests/Tests.fs` 按上面写明的规则直接断言新实现，规则改了要同时改这里。
