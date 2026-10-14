# ListEmptyCard

列表空态卡，迁自旧 `Views/Sidebar.fs` 的 emptyStateHost、emptySkeleton、emptyActionButton，以及 Rebuild 末段的三张卡。要显示哪种空态、用什么文案，由组合者按 `SidebarEmpty.decide` 决定。根规则见项目根目录 AGENTS.md。

- `ListEmptyCard(look)` 暴露 `View`、`ActionButton`、`Skeleton`。`Show(style, title, note, skeletonVisible, actionVisible)` 只在样式变化时换卡；骨架和行动键跨卡复用，所以焦点和读屏名保持连续。其余方法有 `Hide`、`SetAction`、`SetActionVisible`、`FocusAction`。
- `EmptyCardStyle`（限定访问）：`Note` 对应未连接，`Brand` 对应加载中，`Plain` 对应还没有对话。
- 事件：`ActionClicked`。

## 外观

- `ListEmptyCardLook` 在公共字段之外加了 `Logo`、`LogoSize`、`SkeletonFill`、`SkeletonOpacity`、`SkeletonBarHeight`、`RadiusSm`、`Space3xl`、`Space6xl`。

## 证据与旧测试

- 旧用例需要起完整侧栏，由 SidebarDesk 承接。本库新增两个用例：换卡时机与共享行动键；骨架跟随主题改值。
