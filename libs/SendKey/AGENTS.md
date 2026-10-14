# SendKey（库）

旧 `Views/Composer.fs` 回车分支的判定迁入：`SendKey.sends enterSends shift ctrl`。Shift+Enter 永远换行；Enter 发送模式下 Ctrl（或 ⌘）+Enter 换行，Ctrl+Enter 发送模式下裸 Enter 换行。输入法组合中的回车由调用方先排除。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **8 种组合的结论与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/sendkey.txt` 由照抄的旧分支生成。

# 迁入或覆盖的旧测试
旧仓库的回车用例要在窗口里按键，留在旧仓库。本库新增：金样、Shift 不发送、两种模式互补。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
