# ImeShield

输入法组合守卫（迁自旧 `Controls/ImeGuard.fs`，决策 384）：组合期间回车、上下键、Esc、Tab 归输入法；`Key.ImeProcessed` 任何时候都吞。TextBox 隧道阶段类处理器，`install` 幂等（加了锁）。根规则见项目根目录 AGENTS.md。

## 外观

无外观，没有 Look 记录；不用样式类。

## 证据与旧测试

- R21ImeTests：`384 判定：组合中吞掉回车方向键Esc Tab，ImeProcessed 任何时候都吞，普通字符放行`、`385 任意 TextBox 的回车处理器在组合中不触发，结束后照常触发；预编辑可见且提交不重复`（headless 真实 TextBox）。
- R21ImeTests 的 386（命令面板）、387（输入框）测的是具体视图，留旧仓库。
