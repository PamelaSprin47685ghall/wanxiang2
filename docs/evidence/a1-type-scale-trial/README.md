# A1 字号试验 · 前后对比证据

日期：2026-10-31。试验分支 `trial/a1-type-scale`（基于 `dbf3535`，试验后已删除，未合并）。

## 试验内容

把 `libs/ThemeMetrics/ThemeMetrics.fs` 的八档字号整体按约 1.25 比率上调：

| 档位 | 改前 | 改后 |
| --- | --- | --- |
| `FontMicro` | 10.5 | 11.0 |
| `FontCaption` | 11.5 | 12.0 |
| `FontSmall` | 12.5 | 13.0 |
| `FontBody` | 13.5 | 14.0 |
| `FontReading` | 14.5 | 15.5 |
| `FontTitle` | 16.0 | 17.0 |
| `FontHeading` | 19.0 | 20.0 |
| `FontDisplay` | 25.0 | 26.0 |

流程：`./x pack` 重打 feed → 清 `artifacts/gallery-cache/wanxiang2.*` → `bash tools/gallery.sh --render <条目> <out.png>`（浅色主题，720×900）。

## 结果

| 条目 | 是否变化 | 差异度量 |
| --- | --- | --- |
| `SettingsScreen.providers` | 变化 | 过滤字节差异 45155/2592900（1.74%） |
| `SidebarDesk.rows` | 变化 | 过滤字节差异 3344/2592900（0.13%） |
| `MessageCardView.assistant` | **逐字节完全相同** | 0 |
| `ChatEmpty.empty` | **逐字节完全相同** | 0 |

图片：`<条目>.before.png` / `<条目>.after.png`。

差异度量口径：PNG 解码后 zlib 展开的过滤字节逐字节比较（不是逐像素通道比较），只用于说明「有没有变、变了多少」，不作为像素基线门禁依据。

## 结论

1. **改度量字号档影响不到主要阅读面。** 阅读列正文字号来自用户偏好 `UiPrefs.fontScale`（默认 14.5，范围 12–19），不是 `Metric.FontReading`——见 `libs/WorkspaceState/UiPrefs.fs` 与 `compose/AppShell/AppShell.fs`（`FontSize = model.Prefs.fontScale`）。这是 `MessageCardView` 与 `ChatEmpty` 两张图零变化的原因。A1 想解决的「正文与说明层级弱」在阅读区不能靠改度量档解决。

2. **`Metric.FontReading` 零消费者。** `grep -rn FontReading` 全仓只命中 `libs/ThemeMetrics/ThemeMetrics.fs` 自身的 `all` 与 `initial`。clean-break 重构（`ad22bf0`）把输入区字号从 `FontReading` 改成 `FontBody` 后，旧消费点删除而登记档留在公开 `all` 里。

## 未做

- 未合并试验改动，未改生产数值；
- 未重生成正式基线（试验未进入交付）；
- 未决定 `Metric.FontReading` 的删除或改用途（属公开契约变更，需负责人拍板）。
