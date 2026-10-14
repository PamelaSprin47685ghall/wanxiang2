# PromptHistory（库）

旧 `Views/Composer.fs` 里发送历史的状态（`promptHistory`、`historyIndex`、`uncommittedDraft`、上限 100）与上键、下键、Esc、Submit、SetText、Text 里的对应分支迁入，成为一个对象：`Up`、`Down`、`Escape`、`Record`、`Reset`、`Draft`、`IsRecalling`。方法返回 Some 文本时，调用方把输入框换成它、光标放末尾并把按键标为已处理。输入法组合、选区读取、焦点仍归 Composer。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串按键与输入框状态，召回结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/prompthistory.txt` 由照抄旧分支的模拟对象在 245 步按键脚本上生成（含容量溢出与多行文本）。
2. 已知旧行为，原样保留：召回中显示的是多行文本且光标不在开头时，上键不再往前翻。

# 迁入或覆盖的旧测试
旧 CraftsmanshipPolishTests 的 `Composer prompt history records submissions and recalls with Up/Down` 要在窗口里按键，**旧用例不删**（部分覆盖）。本库新增：金样、召回后回到半截草稿、去重去空与容量。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
