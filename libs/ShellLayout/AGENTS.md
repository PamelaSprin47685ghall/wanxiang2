# ShellLayout

主窗口外壳布局（迁自旧 `Views/MainLayout.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ShellState { Compact; DrawerOpen; SidebarCollapsed }`：一帧布局状态，替代旧 NavigationSnapshot 中与布局有关的部分。
- `ShellColumns.Apply state`：按状态投影侧栏列宽、分隔条、抽屉遮罩；窄屏抽屉打开时遮罩可见并可点；`NoteUserSidebarWidth` 记住用户拖出的宽度并夹在最小最大之间。
- 事件：`CompactApplied`、`ScrimPressed`（遮罩按下，组合者关抽屉）、`FocusHomeRequested`。
- `WorkspaceStack(chat, composer, look)`：开始页与续聊共用同一个输入区；`StartModeChanged` 告知组合者。
- Look 中尺寸变化时按上次状态重新投影。

## 外观

- `ShellLayoutLook`：`Scrim` 画笔、`ScrimFade` 时长、侧栏最小最大宽、分隔条宽、聊天区与开始页最小高，均为活实例。

## 证据与旧测试

- UiStabilityTests：MainLayoutController 的投影用例与焦点用例
- ConversationWorkspaceTests：`starting and continuing share the same focused draft and attachment controls`
- 新增：遮罩随抽屉状态显隐、按下发出 ScrimPressed

## 与旧实现的差异

- 状态记录替代 NavigationSnapshot；旧代码直接调 SetCompactMode / SetStartMode / composer.Focus，现改为事件由组合者接；OnScrimPressed 回调改为事件。
