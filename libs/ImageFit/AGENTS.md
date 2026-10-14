# ImageFit

超大图片自动缩小（决策 332/342）的纯规则，迁自旧 `Model/ImageShrink.fs`：最长边上限 `maxDimension`、轮数 `maxAttempts`、等比缩放 `fit`、下一轮上限 `nextLimit`（×0.75）、是否保留透明 `keepsAlpha`、每轮编码候选 `candidates`（PNG 优先，JPEG 85/75/65）、改名 `renamed` / `renamedFor`、四句失败文案。Skia 的解码、EXIF 摆正、缩放、铺白底、编码与动图判断仍归宿主；`maxBytes`、`needsShrink`、`note`、`ShrunkImage` 要 MediaTypes 与 MessageView，归组合者。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **缩小结果与旧代码一致。** [根 AXIOM: 1] 同一张图按本库规则得到的尺寸、格式、文件名与旧 `ImageShrink.shrink` 相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧程序集经 Skia 真实缩小纯色图生成（`tests/Render.fs` 的 INTERACTIVE 分支）；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（需 `LD_LIBRARY_PATH` 指向旧 bin 的 `runtimes/linux-x64/native`）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧 R16ImageVisionTests / R17CapabilityTests 的 ImageShrink 用例都要真实编解码，留给宿主；本库的规则由金标准覆盖。新增用例：`332 放不下时按 0.75 逐轮缩边，最长边不超过 4096`、`332 有透明通道的 PNG/WebP 先试 PNG，其余只试 JPEG 85/75/65`、`342 平台缩小的结果按媒体类型改名`。
