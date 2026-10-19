# BrandMark

合并包：BrandMark、ShellLayout。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BrandMark`

## BrandMark

品牌标：圆角方形，有图显示图（不垫底色、Uniform 不裁剪），无图退化成「万」字（迁自旧 `Views/Brand.fs` 的 `Brand` 模块）。根规则见项目根目录 AGENTS.md。

- `BrandMark.tryLoad asset files`：先内嵌资源再本地文件，找不到 None。资源地址与候选路径由组合者给：旧值是 `avares://Wanxiang.UI/Assets/logo.png`，然后 `AppContext.BaseDirectory/logo.png`、`BaseDirectory/pwa/logo.png`、`$WANXIANG_HOME`（缺省 `~/.config/wanxiang`）`/logo.png`。
- `BrandMark.tile look image size`：图由调用方传（加载一次，多处复用）。
- 旧文件里的 `Export` 模块不属于控件，已由 TranscriptMarkdown / ExportFileName 承担。

### 外观

- `BrandMarkLook`：`Surface`、`Accent`（画笔）；`RadiusRatio`、`GlyphRatio`（活的值）。
- 用到的样式类：无。

### 证据与旧测试

- VisualPassR3Tests：`品牌标有图时不垫白底`（旧测试找不到图时直接跳过；这里用内存位图必定走有图分支）。
- 新增：无图退化与圆角比例实时生效、加载顺序。

### 与旧实现的差异

- 位图不再是模块级单例，加载与渲染分开；资源地址与路径由组合者给。

## 成员 `ShellLayout`

## ShellLayout

主窗口外壳布局（迁自旧 `Views/MainLayout.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ShellState { Compact; DrawerOpen; SidebarCollapsed }`：一帧布局状态，替代旧 NavigationSnapshot 中与布局有关的部分。
- `ShellColumns.Apply state`：按状态投影侧栏列宽、分隔条、抽屉遮罩；窄屏抽屉打开时遮罩可见并可点；`NoteUserSidebarWidth` 记住用户拖出的宽度并夹在最小最大之间。
- 事件：`CompactApplied`、`ScrimPressed`（遮罩按下，组合者关抽屉）、`FocusHomeRequested`。
- `WorkspaceStack(chat, composer, look)`：开始页与续聊共用同一个输入区；`StartModeChanged` 告知组合者。
- Look 中尺寸变化时按上次状态重新投影。

### 外观

- `ShellLayoutLook`：`Scrim` 画笔、`ScrimFade` 时长、侧栏最小最大宽、分隔条宽、聊天区与开始页最小高，均为活实例。

### 证据与旧测试

- UiStabilityTests：MainLayoutController 的投影用例与焦点用例
- ConversationWorkspaceTests：`starting and continuing share the same focused draft and attachment controls`
- 新增：遮罩随抽屉状态显隐、按下发出 ScrimPressed

### 与旧实现的差异

- 状态记录替代 NavigationSnapshot；旧代码直接调 SetCompactMode / SetStartMode / composer.Focus，现改为事件由组合者接；OnScrimPressed 回调改为事件。
