# TransferDesk（组合者）

ImportSheet + ExportSheet + ImportView + ExportView + ProgressThrottle + ConversationTransfer + ConversationExport + TranscriptMarkdown + ExportFileName + MessageView + Wire + OverlayDesk + Theme = 旧 `Views/ImportDialog.fs` 与 `Views/ExportDialog.fs`。根规则见项目根目录 AGENTS.md。

- 导入：`ImportDesk(theme, overlay, topLevel: unit -> TopLevel, submit: ImportPreview -> (bool -> unit) -> unit)`；`Show()`、`LoadText(fileName, byteCount, text)`、`IsOpen`、`Preview`、`StatusText`、`DetailsText`、`ImportButton`、`PickButton`。解析用 `parseImportWithin ImportLimits.maxMessages`；ImportAttempt 只认最新回执；等待上限取主题 `Timing.ImportResponseTimeout`。
- 导出：`ExportDesk(theme, overlay, conversationId, title, sendQuery: ConversationExportQuery -> Task<bool>, topLevel, ?lineage)`；`Show()`、`Handle(page)`、`Fail(exportId, message)`、`Disconnect()`、`IsOpen`、`MarkdownText`、`JsonText`、`Sheet`。分页 ConversationExportController；呈现 ExportView.decide；节流 ProgressThrottle（窗口取 `Timing.ProgressTextThrottle`，到期检查 `Timing.ExportExpiryCheckTick`）；Markdown 由 TranscriptMarkdown.render 加头部说明，JSON 由 ConversationTransfer.toJson；文件名 ExportFileName.ofTitle；减弱动效取主题开关。
- 读取中「取消导出」只取消并原地给「重新导出」，其余时候同一键关闭对话框。

## 证据与旧测试
- R14TransferTests：`313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`、`312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`（测试工程引用 ChatDomain、ServerModel 产出真实分页）
- ConversationExportTests：`cancel stays in the dialog and offers the retry entry`
- PresentationRefreshTests：`export progress bar is determinate under reduced motion`（经主题的减弱动效开关）

## 与旧实现的差异
- Markdown 改由 TranscriptMarkdown（与旧 Export.toMarkdown 金样逐字节相同）；减弱动效与时长从主题取，不读全局 MotionPolicy / MotionLedger。
- 导入状态行「正在导入…」沿用上一档颜色，与旧代码一致。
