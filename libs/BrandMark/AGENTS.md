# BrandMark

品牌标：圆角方形，有图显示图（不垫底色、Uniform 不裁剪），无图退化成「万」字（迁自旧 `Views/Brand.fs` 的 `Brand` 模块）。根规则见项目根目录 AGENTS.md。

- `BrandMark.tryLoad asset files`：先内嵌资源再本地文件，找不到 None。资源地址与候选路径由组合者给：旧值是 `avares://Wanxiang.UI/Assets/logo.png`，然后 `AppContext.BaseDirectory/logo.png`、`BaseDirectory/pwa/logo.png`、`$WANXIANG_HOME`（缺省 `~/.config/wanxiang`）`/logo.png`。
- `BrandMark.tile look image size`：图由调用方传（加载一次，多处复用）。
- 旧文件里的 `Export` 模块不属于控件，已由 TranscriptMarkdown / ExportFileName 承担。

## 外观

- `BrandMarkLook`：`Surface`、`Accent`（画笔）；`RadiusRatio`、`GlyphRatio`（活的值）。
- 用到的样式类：无。

## 证据与旧测试

- VisualPassR3Tests：`品牌标有图时不垫白底`（旧测试找不到图时直接跳过；这里用内存位图必定走有图分支）。
- 新增：无图退化与圆角比例实时生效、加载顺序。

## 与旧实现的差异

- 位图不再是模块级单例，加载与渲染分开；资源地址与路径由组合者给。
