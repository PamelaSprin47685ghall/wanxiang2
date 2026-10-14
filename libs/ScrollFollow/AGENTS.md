# ScrollFollow（库）

旧 `Views/ChatView.fs` 的贴底跟随记账迁入，成为一个对象，管是否贴底、「回到最新」意图、未读数、上一帧条数、上一帧是否在流式、定位钉住：
- `Rendered` 返回 ScrollToEnd 或 Keep，并累计未读；
- `Scrolled` 返回是否停掉平滑滚动；
- `Wheel`、`ReturnToLatest`、`Arrived`、`LeaveForBeginning`、`Pin`、`JumpedTo`、`Reset`；
- `Button` 给「回到最新」按钮的呈现。

滚动动画、计时器、偏移与尺寸判断（是否贴底、是否超出阈值、是否为动画自己写的偏移）仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串事件，状态与决定和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/scrollfollow.txt`（32 步脚本）。
2. 贴底时渲染跟到末尾；不贴底时累计未读，但分页旧消息和流式刷新不算，流式落定记一次；「回到最新」的动画途中仍算贴底，直到用户向上滚。
3. 已知旧行为，原样保留：`Button` 在贴底时顺带把未读清零。

# 迁入或覆盖的旧测试
旧滚动、未读用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
