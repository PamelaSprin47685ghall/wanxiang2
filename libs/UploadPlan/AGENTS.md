# UploadPlan

附件上传与取回的纯部分，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs 的 PickAttachment、DropFiles、HandleClipboardPaste、BeginUpload、BeginUploadCore、SaveDownload 与 HandleEvent 的附件分支：`chunkSize`（256 KiB）、`chunks`（(序号, base64) 列表）、`sha256Hex`、`shrinkFirst`（需要缩小且不超过 64 MiB 才先缩小）、`stillOwned`（读完文件时仍是同一次连接）、`pasteFileName`、`pasteSource`（文件优先于位图）与各句提示文案。读文件、剪贴板、Skia、发线上消息、存文件归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **上传的字节与旧代码一致。** [根 AXIOM: 1] 同一个文件分出的块、校验和与旧程序发出的相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由 `tools/OldUploadPlan.fs`（旧代码原样摘录，send 换成收集）跑 `tests/Render.fs` 生成；金标准用例通过。
2. `tools/gen-golden.fsx` 重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧整壳 UI 测试经过上传路径但依赖 Avalonia 与连接；新增 `分块拼回去就是原文件，序号从 0 连续`、`读完文件时已断开或换了主机，就不上传`。
