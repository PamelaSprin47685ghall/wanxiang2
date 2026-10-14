# DownloadBuffers

附件下载的在途接收缓冲（按小写 sha256），单个下载有硬上限，迁自旧 `Model/UiControllers.fs`。`DownloadChunkResult` 的 Unknown 等是通用名，按根规则加了 `[<RequireQualifiedAccess>]`，调用方写 `DownloadChunkResult.Accepted` 等。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **内存不随异常流增长：触顶整体丢弃，断连清零。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- 无旧纯逻辑用例。
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。
