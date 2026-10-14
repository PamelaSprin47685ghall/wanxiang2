# RevealPane

限高视口（迁自旧 `Views/MessageCard.fs` 的 `detailViewport`）：内容超出给定高度时，下方露出「展开全部 / 收起」幽灵按钮；首次布局前视口高度为 0，不据此判定裁切。根规则见项目根目录 AGENTS.md。

## 外观

- `RevealPaneLook`：`Gauge`（`RevealGauge.Gap` 视口与按钮间距、`Gutter` 右侧滚动条槽，都是活值），`Dress`（给按钮挂幽灵按钮样式类）。
- 限高值由使用方按布局规则传入（旧 `LayoutPolicy.expandedDetailMaxHeight`）。

## 证据与旧测试

覆盖旧用例：`UiStabilityTests.large tool detail is bounded first and can explicitly expand fully`、`SelectionOperabilityTests.long user messages get a clipped viewport with an expand entry`、`ScrollLayoutTests.嵌套 ScrollViewer 与草稿/待发送滚动视口具备右侧安全留白与视觉分层`（右侧留白部分）。本库在 headless 下验证：短内容不出按钮、超高出按钮、全文态切换与读屏名、隐藏时退出全文态、度量实时生效且摘下后退订。整卡的控件树金样在组合者 MessageCardView。

## 与旧实现的差异

旧代码返回 `(Control * (bool -> unit))` 二元组，现在是一个对象（`Host`、`SetVisible`、`ToggleFull`）。
