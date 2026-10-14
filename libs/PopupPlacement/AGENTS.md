# PopupPlacement（库）

旧 `Controls/OverlayHost.fs` 的 `positionPopup` 里的坐标计算迁入：`place`。锚点下方隔 gap 处左对齐（或右对齐），下方放不下翻到上方，最后钳进视口并留 inset。量控件尺寸、`TranslatePoint`、写 Margin 仍归 OverlayHost（旧值：inset = `Spacing.space2xl` 16，gap = `Spacing.spaceMd` 8）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组尺寸，落点与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/popupplacement.txt` 由照抄的旧计算在 30 组尺寸上生成。

# 迁入或覆盖的旧测试
旧 UiStabilityTests 的 `popup flips and clamps inside the viewport then restores anchor focus` 还测焦点归还，**旧用例不删**（部分覆盖）。本库新增：金样、翻到上方、右对齐。
