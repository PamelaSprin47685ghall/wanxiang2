# ComposerBox

输入壳（迁自旧 `Views/Composer.fs` 的输入框、shell 外观与焦点环、发送 / 停止 / 排队 / 附件 / 上下文键、拖入文件、禁用说明、看图提示、附属位、Enter 与 Ctrl+Enter 发送）。附件托盘与模型芯片由宿主放进插槽；发送门控由宿主用 `ComposerSendState.decide` 折成 `SendView` 写入；历史召回与输入法探针经钩子注入（旧 PromptHistory、ImeGuard）。各键是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `ComposerBoxLook`：`Ink`、`Gauge`、`BodyFont`、`Glyph`、`DressSend` / `DressQueue` / `DressAttach` / `DressGhost`。

## 证据与旧测试

覆盖旧 `ComposerTests` 类用例中发送 / 停止 / 排队切换、Enter 规则、输入法组合不提交、禁用说明与占位文字、看图受阻提示。本库无头行为用例覆盖读屏 Invoke、按键规则、禁用与看图提示。

- 键序：输入法组合 → 宿主钩子 → Esc 停止 → Enter 发送判定；Enter 发送档里 Ctrl+Enter 交给输入框换行。发送/排队键不读门控缓存，直接交宿主同步判定。窄屏收起对话上下文按钮（决策 364）。拖放挂在整个根上。
