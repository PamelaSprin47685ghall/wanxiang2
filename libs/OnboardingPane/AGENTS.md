# OnboardingPane

聊天主界面上的「添加一个模型服务」卡片（迁自旧 `Views/OnboardingCard.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `OnboardingPane(look)`；`View` 是卡片；`Render state` 同步一帧 `OnboardingPaneState`（服务商键、「更多」文字、说明、密钥 / 地址框是否可见、连接中、状态行、兼容端点切换键、本机发现说明）。
- 输入：`KeyBox`（密码字符 ●，回车即连接）、`CustomUrlBox`、`KeyText` / `CustomUrlText`（已去空白）、`ClearKey()`；`FocusInitial needsKey`（要密钥进密钥框，否则落在「连接」）。
- 事件：`ChipClicked key`、`MoreClicked anchor`（连接中不发；组合者在锚点弹菜单）、`ConnectClicked`、`SwitchClicked`、`AdvancedClicked`、`UseLocalClicked`。
- 连接中：「连接」保持最小宽，只换成「正在连接…」并禁用（读屏帮助文字同步），两个输入框禁用。
- 步骤与状态文字（OnboardingFlow）、预设与载荷（ProviderPresets、OnboardingRules）、探测、本机发现、写回与「更多」菜单（PopupMenu / OverlayDesk）都由组合者接。

## 外观

- `OnboardingPaneLook`：公共字段加 `Space3xl`（卡片内边距）、`CardMaxWidth`、`PendingMinWidth`。全部是主题管理者的活实例。
- 样式类：按钮 `surface button-* hover-*`、卡 `grouping-card`、输入壳 `surface hover-raised focus-soft`。

## 证据与旧测试

- OnboardingUiTests：`密钥为空时不发起任何请求并就地说明`（呈现与焦点部分）、`路径错时改用兼容端点后重试，载荷换成兼容传输`（切换键部分）、`发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`（呈现部分）、`没有 Ollama 时不显示一键使用`
- 新增：`连接中按钮保持几何只换文字并禁用，选择键与更多不再响应`、`服务商键选中为主色，点击发出预设 key`、`look values stay live after the card is shown`
- 写回、探测、撤销半成品、外壳旅程（`外壳里走完整条旅程…` 等）在组合者与宿主。

## 与旧实现的差异

- 无可见差异。服务商键与「更多」在每次 `Render` 时重建（旧实现选择变化时重建）。
