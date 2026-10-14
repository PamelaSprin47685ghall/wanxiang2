# SkeletonList

历史加载骨架（迁自旧 `Views/ChatView.fs` 的 `EnsureSkeletonBuilt` / `ShowSkeletonLoading` / `HideSkeletonLoading` 与骨架呼吸）：助手 → 用户 → 助手三块占位；显示且挂在可视树上、未减弱动效时按帧呼吸整块透明度。相位记账由使用方注入（旧 `BreathPhase`），计时器属于控件自己。根规则见项目根目录 AGENTS.md。

## 外观

- `SkeletonListLook`：`Ink`（`SkeletonInk.Bar` / `Edge`）、`Gauge`（间距、阅读宽、头像、圆角、透明度档）、`Frame`、`Reduced`，都是活值。
- `SkeletonBreath`：`Begin` 相位归零，`Step` 走一拍返回透明度（旧 BreathPhase 曲线）。

## 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.ChatView skeleton loading transitions toggle skeletonPanel and messagePanel visibility` 中骨架树与呼吸部分（显隐切换、挂载才转、减弱动效静态透明度）。本库金样 `tests/golden/skeleton.txt` 由旧 `ChatView.ShowSkeletonLoading`（减弱动效）生成；行为用例覆盖挂上才呼吸、减弱即时切换。
