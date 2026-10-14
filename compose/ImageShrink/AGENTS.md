# ImageShrink

组合者：图片自动缩小（决策 332）的判定与说明。`ShrunkImage` 记录、`maxBytes`（MediaTypes 的 8 MiB）、`maxDimension`（ImageFit 的 4096）、`needsShrinkFor 文件名 字节数 尺寸`、`note`，迁自旧 `Model/ImageShrink.fs`。旧 `needsShrink 文件名 bytes` 要 Skia 读文件头，宿主先取尺寸再调 `needsShrinkFor`。协作者：ImageFit、MediaTypes、MessageView（`AttachmentRef.formatSize`）。命名空间沿用 `Wanxiang.UI`，与宿主的 `ImageShrink.shrink` 同名合并。根规则见项目根目录 AGENTS.md。

# SPEC
1. 真实组合：测试直接用三个库的交付包（`tests/Tests.fs`）。

# 迁入或覆盖的旧测试
- R16ImageVisionTests：`normal images and non-images are left alone`（判定部分；`shrink` 返回 Ok None 那一行要 Skia，留宿主）、`oversize photo is downscaled under 8 MiB with a before-after note`（note 三条断言；缩小本身留宿主）、`huge dimensions shrink to 4096 even when the file is small`（判定部分；4096×546 由 ImageFit 金标准覆盖同类尺寸）
