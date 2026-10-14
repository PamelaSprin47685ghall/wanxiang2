# __NAME__

[TODO] 写明本控件的职责、状态归属、事件，以及覆盖的旧测试名。

## 外观

- `__NAME__Look`：`Background`、`Foreground`（画笔）、`Padding`（活的尺寸）。全部由主题管理者提供，本库不写字面样式值。
- 用到的 ControlTheme 样式类：无。

## 证据

- `Tests.fs`：在 Avalonia headless 下验证真实控件行为与外观实例的实时生效。
