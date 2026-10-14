# CopyButton

复制按钮的确认反馈（迁自旧 `Controls/CopyFeedback.fs`）。确认记账（淡出、保持、复位）由本控件自己持有，规则与 CopyConfirm 库相同但不引用它；计时器指向下一个到点时刻。图标、提示文字、读屏名由使用方按 `StateChanged` 去换（旧 `show` 回调）。根规则见项目根目录 AGENTS.md。

## 外观

- `CopyButtonLook`：`FadeOpacity`（活的不透明度），`Hold`、`Fade`（活的时长）。时长也是主题给的可变值，不写死。
- 用到的样式类：无。

## 证据与旧测试

旧仓库没有直接测 CopyFeedback 的用例（MessageCard 复制用例经整张卡片）。本库在 headless 下用可控时钟验证：淡出→保持→复位、减弱动效、重新起算、离开可视树复位、外观实时生效。CopyConfirm 的金样已覆盖同一组时刻规则。

## 与旧实现的差异

`CopyState` 取代 `CopyVisual`（加了 RequireQualifiedAccess），使用方改按 `StateChanged` 订阅。
