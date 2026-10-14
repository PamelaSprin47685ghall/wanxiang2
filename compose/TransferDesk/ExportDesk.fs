namespace Wanxiang2.TransferDesk

open System
open System.Text
open System.Threading.Tasks
open Avalonia.Controls
open Avalonia.Platform.Storage
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang.Protocol
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.ExportSheet

/// 导出对话（决策 312）的接线：分页读取归 ConversationExportController，每帧呈现由 ExportView.decide 得出，
/// 进度文字节流归 ProgressThrottle（高频分页合并、窗口末尾补一帧；阶段切换与终态立即呈现），
/// 读满后同一次读取备好 Markdown（TranscriptMarkdown + 头部说明）与可再导入的 JSON；
/// 保存窗口在点击回调里直接打开。读取中「取消导出」只中止本次读取并原地给「重新导出」。
type ExportDesk
    (look: ExportSheetLook,
     timing: TransferTiming,
     overlay: OverlayDesk,
     conversationId: Guid,
     title: string,
     sendQuery: ConversationExportQuery -> Task<bool>,
     topLevel: unit -> TopLevel,
     lineage: ExportLineage) =
    let ui = Dispatcher.UIThread
    let controller = ConversationExportController(conversationId)
    let sheet = ExportSheet(look, title, ConversationTransfer.privacyNote)
    let mutable active = false
    let mutable saving = false
    let mutable saved = false
    let mutable saveError: string option = None
    let mutable output: byte[] option = None
    let mutable outputJson: byte[] option = None
    let mutable savedKind = ""
    let expiry = DispatcherTimer(Interval = timing.ExportExpiryCheckTick())
    let tail = DispatcherTimer(Interval = timing.ProgressTextThrottle())
    let mutable throttle = ProgressThrottle()

    let toTranscript (m: MessageView) : TranscriptMessage =
        { role = m.role
          committedAt = m.committedAt
          reasoning = m.reasoning
          toolCalls = m.toolCalls |> List.map (fun c -> { name = c.name; argumentsJson = c.argumentsJson; result = c.result })
          toolResults = m.toolResults
          text = m.text
          attachments = m.attachments |> List.map (fun a -> { fileName = a.fileName; sizeText = AttachmentRef.formatSize a.size }) }

    // 立即渲染一帧：记下刷新时刻，取消未落地的节流尾巴。
    let render () =
        tail.Stop()
        throttle.Rendered DateTimeOffset.UtcNow
        let phase =
            match controller.Status with
            | ExportReading(received, total) -> ExportPhase.Reading(received, total)
            | ExportReady count -> ExportPhase.Ready count
            | ExportFailed message -> ExportPhase.Failed message
            | ExportCancelled -> ExportPhase.Cancelled
            | ExportIdle -> ExportPhase.Idle
        let v =
            ExportView.decide phase
                { saving = saving; saved = saved; savedKind = savedKind; error = saveError }
                output.IsSome outputJson.IsSome (timing.ReducedMotion())
        sheet.Render
            { StatusText = v.statusText
              Tone =
                (match v.tone with
                 | ExportTone.Muted -> ExportSheetTone.Muted
                 | ExportTone.Success -> ExportSheetTone.Success
                 | ExportTone.Danger -> ExportSheetTone.Danger
                 | ExportTone.Text -> ExportSheetTone.Text)
              ProgressVisible = v.progressVisible
              Indeterminate = v.indeterminate
              ProgressValue = v.progressValue
              RetryVisible = v.retryVisible
              SaveVisible = v.saveVisible
              SaveJsonVisible = v.saveJsonVisible
              SaveEnabled = v.saveEnabled
              SaveJsonEnabled = v.saveJsonEnabled
              CloseText = v.closeText }

    // 刷新入口：force 走急件通道；只有读取中是高频态。
    let refresh (force: bool) =
        let highFrequency = match controller.Status with ExportReading _ -> true | _ -> false
        match throttle.Request(DateTimeOffset.UtcNow, force, highFrequency) with
        | ProgressRefresh.Now -> render ()
        | ProgressRefresh.Schedule delayMs ->
            tail.Interval <- TimeSpan.FromMilliseconds delayMs
            tail.Start()
        | ProgressRefresh.Skip -> ()

    let fail (exportId: Guid) (message: string) =
        if active then
            controller.Fail(exportId, message)
            refresh true

    let dispatch (query: ConversationExportQuery) =
        let sending = sendQuery query
        async {
            try
                let! sent = sending |> Async.AwaitTask
                if not sent then ui.Post(fun () -> fail query.exportId "连接已断开，未完成导出；请重新连接后重试。")
            with ex -> ui.Post(fun () -> fail query.exportId ("读取历史失败：" + ex.Message))
        } |> Async.Start

    let start () =
        output <- None
        outputJson <- None
        saveError <- None
        saved <- false
        let query = controller.Start()
        refresh true
        dispatch query

    let cancel () =
        controller.Cancel()
        refresh true

    let save (asJson: bool) =
        match controller.Document, (if asJson then outputJson else output) with
        | Some document, Some bytes when not saving ->
            let top = topLevel ()
            if isNull top then
                saveError <- Some "当前窗口不支持保存文件。"
                refresh false
            else
                try
                    // 文件选择器在点击回调内立即调用；带标题与扩展名，用户删掉扩展名时由系统补回。
                    let markdownName = ExportFileName.ofTitle document.title
                    let picking =
                        top.StorageProvider.SaveFilePickerAsync(
                            FilePickerSaveOptions(
                                Title = "导出对话",
                                SuggestedFileName =
                                    (if asJson then ConversationTransfer.safeJsonFileName document.title markdownName else markdownName),
                                DefaultExtension = (if asJson then "json" else "md"),
                                FileTypeChoices =
                                    [ if asJson then FilePickerFileType("万象对话 JSON", Patterns = [| "*.json" |])
                                      else FilePickerFileType("Markdown 文档", Patterns = [| "*.md" |]) ]))
                    saving <- true
                    saveError <- None
                    refresh false
                    async {
                        try
                            let! file = picking |> Async.AwaitTask
                            if not (isNull file) then
                                use file = file
                                use! stream = file.OpenWriteAsync() |> Async.AwaitTask
                                if stream.CanSeek then stream.SetLength 0L
                                do! stream.WriteAsync(bytes, 0, bytes.Length) |> Async.AwaitTask
                                do! stream.FlushAsync() |> Async.AwaitTask
                            ui.Post(fun () ->
                                if active then
                                    saving <- false
                                    if not (isNull file) then
                                        saved <- true
                                        savedKind <- (if asJson then "JSON" else "Markdown")
                                    refresh false)
                        with ex ->
                            ui.Post(fun () ->
                                if active then
                                    saving <- false
                                    saveError <- Some("保存失败：" + ex.Message + "。完整内容仍保留，可以再次保存。")
                                    refresh false)
                    } |> Async.Start
                with ex ->
                    saving <- false
                    saveError <- Some("无法打开保存窗口：" + ex.Message)
                    refresh false
        | _ -> ()

    do
        expiry.Tick.Add(fun _ -> if controller.Expire(DateTimeOffset.UtcNow, TimeSpan.FromSeconds 30.0) then refresh true)
        tail.Tick.Add(fun _ -> if throttle.TailFired() then render ())
        sheet.CloseClicked.Add(fun () ->
            match controller.Status with
            | ExportReading _ -> cancel ()
            | _ -> overlay.CloseDialog())
        sheet.RetryClicked.Add(fun () -> start ())
        sheet.SaveClicked.Add save

    new(theme: ThemeManager, overlay: OverlayDesk, conversationId: Guid, title: string, sendQuery: ConversationExportQuery -> Task<bool>, topLevel: unit -> TopLevel, ?lineage: ExportLineage) =
        ExportDesk(TransferLook.exportOf theme, TransferTiming.ofTheme theme, overlay, conversationId, title, sendQuery, topLevel, defaultArg lineage ConversationTransfer.rootLineage)

    member _.IsOpen = active
    member _.Sheet = sheet
    member _.MarkdownText = output |> Option.map Encoding.UTF8.GetString
    member _.JsonText = outputJson |> Option.map Encoding.UTF8.GetString

    /// 打开对话框（宽 480）并立即开始读取；焦点落在取消导出
    member _.Show() =
        active <- true
        overlay.ShowDialog(sheet.View, timing.DialogWidth(), onClosed = (fun () ->
            active <- false
            expiry.Stop()
            tail.Stop()
            throttle <- ProgressThrottle()
            controller.Cancel()
            output <- None
            outputJson <- None))
        expiry.Start()
        start ()
        sheet.FocusClose()

    /// 主机送来的一页
    member _.Handle(page: ConversationExportPageData) =
        if active then
            let next = controller.Accept page
            match controller.Document, output with
            | Some document, None ->
                try
                    let now = DateTimeOffset.Now
                    let markdown =
                        ConversationTransfer.withPreamble
                            (TranscriptMarkdown.render document.title (document.messages |> List.map toTranscript))
                            (ConversationTransfer.markdownPreamble lineage document.messages.Length now)
                    if int64 (Encoding.UTF8.GetByteCount markdown) > ConversationExportLimits.maxTranscriptBytes then
                        saveError <- Some "生成的 Markdown 超过 64 MiB 安全上限；没有保存不完整文件。"
                    else output <- Some(Encoding.UTF8.GetBytes markdown)
                    let json = ConversationTransfer.toJson conversationId document.title document.atCommitId now lineage document.raw
                    if int64 (Encoding.UTF8.GetByteCount json) <= ConversationTransfer.maxImportBytes then
                        outputJson <- Some(Encoding.UTF8.GetBytes json)
                with ex -> saveError <- Some("无法生成完整 Markdown：" + ex.Message)
            | _ -> ()
            refresh false
            next |> Option.iter dispatch

    member _.Fail(exportId: Guid, message: string) = fail exportId message

    member _.Disconnect() =
        if active then
            controller.Disconnect()
            refresh true
