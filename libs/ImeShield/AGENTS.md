# ImeShield

输入法组合守卫（迁自旧 `Controls/ImeGuard.fs`，决策 384）：组合期间回车、上下键、Esc、Tab 归输入法。TextBox 隧道阶段类处理器，`install` 幂等（加了锁）。根规则见项目根目录 AGENTS.md。

2026-10-28 修正（macOS 桌面实测）：`Key.ImeProcessed` 不再无条件吞。macOS 平台把英文普通字符键也标成 `Key.ImeProcessed`，旧判定把所有字符键吞掉，导致全部输入框无法录入（诊断日志：绕过守卫后搜索框与消息框恢复输入）。现在只在组合态（`PreeditText` 非空）吞归输入法的键；非组合期的 `ImeProcessed` 是普通字符键的平台标记，放行给 TextBox 产生文本。

## 外观

无外观，没有 Look 记录；不用样式类。

## 证据与旧测试

- `384 判定：组合中吞掉回车方向键Esc Tab，ImeProcessed 任何时候都吞，普通字符放行`（断言已随 2026-10-28 修正更新：非组合期 `ImeProcessed` 放行）、`385 任意 TextBox 的回车处理器在组合中不触发，结束后照常触发；预编辑可见且提交不重复`（headless 真实 TextBox）。
- macOS 桌面真机证据：修复前用户实测所有输入框无法录入（含 ABC 英文）；绕过守卫的对照实验中搜索框与消息框恢复输入；修复后待真机复验。
- R21ImeTests 的 386（命令面板）、387（输入框）测的是具体视图，留旧仓库。
