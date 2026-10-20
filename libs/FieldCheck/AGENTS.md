# FieldCheck

字段级校验反馈（迁自旧 `Controls/Primitives.fs` 的 `Ui.fieldValidationMessage`、`setFieldError`、`clearFieldError`、`bindValidationSummaryReset`、`applyValidationFeedback`，与 `Controls/Interaction.fs` 的 `setInvalid`/`isInvalid`/`syncInvalid`）。根规则见项目根目录 AGENTS.md。

状态归属：每个输入框的错误行由 `FieldCheck` 实例创建并持有（旧实现是模块级弱表）。校验态是附加属性 `IsInvalid`，同步到类名 `invalid`，描边经主题资源键声明式改写：错误态绑 `Palette.Danger`、复位绑 `Palette.Border`（画笔是主题管理者的活实例，改色不重挂）。

`Apply` 不再收提示条回调，而是返回应弹出的文字；组合者把它交给提示层。字段组排版（label → 控件 → 错误 → hint，hint 与错误互斥）仍在旧 `Ui.fieldGroup`，等主题与基础控件一起迁。

## 外观

- `FieldCheck()` 无参构造（W7 外观退役：`FieldCheckAppearance` 已删除）。错误行走主题资源键的声明式绑定：`Palette.Danger`、`Metric.FontCaption`、`Metric.CaptionLineHeight`、`Margin.FieldError`（主题派生档）。
- 接口：`MessageFor`（取/建错误行）、`ApplyError`（幂等写单槽错误行）、`Clear`（清错误行）、`HasError`、`IsInvalid`/`InvalidClass`、`BindSummaryReset`、`Apply`（提交后统一反馈，返回提示条文字）。
- 用到的样式类：`invalid`（`FieldCheck.InvalidClass`）。

## 证据与旧测试

- UiStabilityTests：`field validation is local and does not change geometry`
- SettingsSurfaceTests：`validation feedback with a single error toasts the message and hides the summary`、`validation feedback with multiple errors fills the focusable summary`、`validation feedback with no errors stays silent and hides the summary`
- 新增：文本没变不清错误、摘要随修正收起、资源键改值错误行与描边实时随动。
- UiStabilityTests 的 `field group shows hint or error but never both` 依赖 `Ui.inputFieldGroup`，留旧仓库。
