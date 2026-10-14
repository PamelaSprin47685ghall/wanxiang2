# ModelChip

输入区模型芯片（迁自旧 `Views/Composer.fs` 的 `modelChip` / `SetModelLabel`）：显示当前模型名，点开切换；未选时用 muted 色。芯片是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `ModelChipLook`：`Ink`、`Gauge`（字号、最大宽、内边距、圆角、窄屏最小高）、`BodyFont`、`Dress`。

## 证据与旧测试

覆盖旧 `SetModelLabel` 与点击打开选择器。本库无头行为用例覆盖标签换色与读屏 Invoke。
