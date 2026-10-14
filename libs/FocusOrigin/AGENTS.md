# FocusOrigin

聚焦来源报告（迁自旧 `Controls/Interaction.fs` 的 `reportFocusOrigin`）：键盘或程序化聚焦给控件挂 `focus-by-program` 类，指针聚焦与失焦去掉。根规则见项目根目录 AGENTS.md。

## 外观

无 Look 记录。用到的样式类：`focus-by-program`（只挂类名；焦点环的样式由主题安装）。

## 证据与旧测试

旧 FocusRingTests 检查的是焦点环的画法（主题样式），留旧仓库与主题一起迁。本库在 headless 下用真实焦点切换验证类名。
