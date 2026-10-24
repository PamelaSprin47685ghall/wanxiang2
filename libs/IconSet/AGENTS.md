# IconSet（库）

旧 `Design/Icons.fs` 迁入：语义名 `Glyph`（51 个，含转圈 `Loader`）、`IconSet.kind`（Lucide 字形）、按（画笔实例，字形）缓存的 `IconSet.drawing`、图标控件 `IconSet.image glyph brush size` 与 `IconSet.control glyph size`。字形全部来自官方 `LucideImageExtension`，不自绘。`PackIconLucide` 的控件模板是按 Avalonia 11 编译的，在 12 上 `DynamicResourceExtension.ProvideValue` 对不上，所以 `control` 不挂那个模板，只把官方字形画进 `Foreground`。`image` 的画笔是主题管理者的活实例，尺寸是活的 `IObservable<float>`；`control` 用固定尺寸。转圈挂主题的 `anim-spin`，不用控件自带 Spin。依赖 Avalonia 与 IconPacks.Avalonia.Lucide，不引用任何其他库。模块名避开旧宿主的 `Icons`。根规则见项目根目录 AGENTS.md。

## 外观
- 无 Look 记录：画笔和尺寸是每次调用的参数。用到的 ControlTheme 样式类：无。

# LEMMA
1. **语义映射与旧实现相同。** [根 AXIOM: 5]
2. **图标跟随活画笔与活尺寸。** 改色不用重建。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`the icon keeps the live brush and follows the live size`（字形映射与尺寸跟随同一条用例）。
2. [LEMMA: 2] headless 用例：尺寸改值后宽高跟随，图像复用，字形画笔就是传入的实例（这一版 Lucide 用填充画笔出墨，旧注释说的是描边画笔）。

# 迁入或覆盖的旧测试
旧 `IconCenteringTests`（墨迹断言）要整套旧视图，留在旧仓库。
