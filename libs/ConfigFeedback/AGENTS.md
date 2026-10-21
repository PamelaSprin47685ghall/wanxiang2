# ConfigFeedback

合并包：ConfigFeedback、DownloadBuffers、HostRequests、UploadFacts。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ConfigFeedback`

## ConfigFeedback

配置写入的 request → ConfigApplied 确认台账（超时、断连全部失败），迁自旧 `Model/UiControllers.fs`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **每个请求只结束一次：确认、超时、断连三者先到者生效。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- R27HostCapabilitiesTests：`config tracker expires silent requests and resolves only once`
- 新增 `ConfigApplied.notice` 与 `ConfigAppliedNotice`，迁自旧 AppShell HandleEvent 的 ConfigApplied 分支。新增用例 `首次运行卡片登记空文案：成功时不叠 toast 但仍拉目录`（在 `tests/ConfigFeedback.Tests.fs`）。
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。

## 成员 `DownloadBuffers`

## DownloadBuffers

附件下载的在途接收缓冲（按小写 sha256），单个下载有硬上限，迁自旧 `Model/UiControllers.fs`。`DownloadChunkResult` 的 Unknown 等是通用名，按根规则加了 `[<RequireQualifiedAccess>]`，调用方写 `DownloadChunkResult.Accepted` 等。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **内存不随异常流增长：触顶整体丢弃，断连清零。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- 无旧纯逻辑用例。
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。

## 成员 `HostRequests`

## HostRequests

等主机回应的两类在途请求（决策 443/444、W4），迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs 的 `pendingProbes`、`latestSearchQueryId`、`searchSentAt` 三个字段及读写它们的 `ProbeProvider`、`ExpireHostRequests`、`HandleEvent`（ProviderProbeResult / SearchResults / SearchFailed）、搜索发送：
- `PendingProbes`：`Track`、`Resolve`、`Expire(now, timeout)` 取出到点的 (requestId, providerId)。
- `SearchInFlight`：`Sent`、`Accept`（只认最近一次）、`Expire`、`Latest`。
到点后的文案（`HostFeatures.noResponseMessage`）与界面回填归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **状态转移与旧代码一致。** [根 AXIOM: 1]

## SPEC
1. [LEMMA: 1] `tests/HostRequests.Tests.fs` 通过：`W4 只认最近一次查询，迟到的旧结果丢掉`、`443 到点的探测按发出顺序收尾，已回应的不再收尾`。

## 迁入或覆盖的旧测试
- 旧仓库这几处只在整壳 UI 测试里间接经过；新增 `W4 只认最近一次查询，迟到的旧结果丢掉`、`443 到点的探测按发出顺序收尾，已回应的不再收尾`。

## 成员 `UploadFacts`

## UploadFacts

附件上传与取回的纯部分，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs 的 PickAttachment、DropFiles、HandleClipboardPaste、BeginUpload、BeginUploadCore、SaveDownload 与 HandleEvent 的附件分支：`chunkSize`（256 KiB）、`chunks`（(序号, base64) 列表）、`sha256Hex`、`shrinkFirst`（需要缩小且不超过 64 MiB 才先缩小）、`stillOwned`（读完文件时仍是同一次连接）、`pasteFileName`、`pasteSource`（文件优先于位图）与各句提示文案。读文件、剪贴板、Skia、发线上消息、存文件归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **上传的字节与旧代码一致。** [根 AXIOM: 1] 同一个文件分出的块、校验和与旧程序发出的相同。

## SPEC
1. [LEMMA: 1] `tests/UploadFacts.Tests.fs` 通过：`分块拼回去就是原文件，序号从 0 连续`、`读完文件时已断开或换了主机，就不上传`。

## 迁入或覆盖的旧测试
- 旧整壳 UI 测试经过上传路径但依赖 Avalonia 与连接；新增 `分块拼回去就是原文件，序号从 0 连续`、`读完文件时已断开或换了主机，就不上传`。
