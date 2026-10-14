namespace Wanxiang2.TransferDesk

open System
open System.IO
open System.Text
open Avalonia.Controls
open Avalonia.Platform.Storage
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang.Protocol
open Wanxiang.Core
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.ImportSheet

/// 导入对话（决策 313）的接线：说明 → 选文件并预览 → 确认导入。
/// 选文件窗口在点击回调里直接打开（浏览器要求用户手势）；预览通过前什么都不发；
/// 确认后交给 `submit`，只认最新一次提交的回执（ImportAttempt），等待有上限，超时给说明。
type ImportDesk(look: ImportSheetLook, timing: TransferTiming, overlay: OverlayDesk, topLevel: unit -> TopLevel, submit: ImportPreview -> (bool -> unit) -> unit) =
    let ui = Dispatcher.UIThread
    let sheet = ImportSheet(look, ConversationTransfer.privacyNote)
    let attempts = ImportAttempt()
    let mutable preview: ImportPreview option = None
    let mutable active = false
    /// 状态行当前的色档：「正在导入…」沿用上一档（与旧代码只改文字一致）
    let mutable tone = ImportTone.Muted
    let setStatus text t =
        tone <- t
        sheet.SetStatus(text, t)

    let refresh () =
        let chrome = ImportView.chrome preview.IsSome attempts.Busy
        sheet.SetChrome(chrome.importVisible, chrome.importEnabled, chrome.pickEnabled, chrome.pickText)

    let loadText (fileName: string) (byteCount: int64) (text: string) =
        if active then
            match ConversationTransfer.parseImportWithin ImportLimits.maxMessages byteCount text with
            | Ok p ->
                preview <- Some p
                setStatus (ImportView.previewStatus p.title p.messages.Length) ImportTone.Text
                sheet.SetDetails(
                    Some
                        [ yield sprintf "文件：%s" fileName
                          match p.exportedAt with
                          | Some at -> yield sprintf "导出时间：%s" (at.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                          | None -> ()
                          yield sprintf "导入后的标题：%s" (ConversationTransfer.importedTitle p.title)
                          match p.lineageNote with
                          | Some note -> yield note
                          | None -> ()
                          if p.hasAttachments then yield "文件里提到的附件只有名称，文件本体不在导出里，导入后无法打开。"
                          match ImportView.skippedNote p.skipped with
                          | Some note -> yield note
                          | None -> ()
                          yield "模型使用这台主机的对话默认值，导入后可以在对话顶部更换。" ])
                sheet.FocusImport()
            | Error message ->
                preview <- None
                setStatus (message) ImportTone.Danger
                sheet.SetDetails None
            refresh ()

    let pick () =
        match topLevel () with
        | null -> ()
        | top ->
            try
                let picking =
                    top.StorageProvider.OpenFilePickerAsync(
                        FilePickerOpenOptions(
                            Title = "导入对话",
                            AllowMultiple = false,
                            FileTypeFilter = [ FilePickerFileType("万象对话 JSON", Patterns = [| "*.json" |]) ]))
                async {
                    try
                        let! picked = picking |> Async.AwaitTask
                        if picked.Count > 0 then
                            let file = picked[0]
                            use! stream = file.OpenReadAsync() |> Async.AwaitTask
                            use buffer = new MemoryStream()
                            // 先只读到上限 + 1 字节：超限就不必把整份大文件读进内存。
                            let limit = ConversationTransfer.maxImportBytes + 1L
                            let chunk = Array.zeroCreate<byte> 65536
                            let mutable reading = true
                            while reading && buffer.Length < limit do
                                let! n = stream.ReadAsync(chunk, 0, chunk.Length) |> Async.AwaitTask
                                if n = 0 then reading <- false else buffer.Write(chunk, 0, n)
                            let bytes = buffer.ToArray()
                            let text = if int64 bytes.Length > ConversationTransfer.maxImportBytes then "" else Encoding.UTF8.GetString bytes
                            ui.Post(fun () -> loadText file.Name (int64 bytes.Length) text)
                    with ex ->
                        ui.Post(fun () -> if active then setStatus (ImportView.readFailed ex.Message) ImportTone.Danger)
                } |> Async.Start
            with ex -> setStatus (ImportView.pickerFailed ex.Message) ImportTone.Danger

    let confirm () =
        match preview with
        | Some p ->
            match attempts.Begin() with
            | None -> ()
            | Some attempt ->
                sheet.SetStatus(ImportView.importing, tone)
                refresh ()
                let finish ok (message: string) =
                    if attempts.Finish attempt then
                        if ok then overlay.CloseDialog()
                        elif active then
                            setStatus (message) ImportTone.Danger
                            refresh ()
                // 旧主机不认识 conversation.import 时不会回应：等待有上限。
                DispatcherTimer.RunOnce((fun () -> finish false ImportView.timedOut), timing.ImportResponseTimeout()) |> ignore
                submit p (fun ok -> finish ok ImportView.rejected)
        | None -> ()

    do
        sheet.CancelClicked.Add(fun () -> overlay.CloseDialog())
        sheet.PickClicked.Add(fun () -> pick ())
        sheet.ImportClicked.Add(fun () -> confirm ())

    new(theme: ThemeManager, overlay: OverlayDesk, topLevel: unit -> TopLevel, submit: ImportPreview -> (bool -> unit) -> unit) =
        ImportDesk(TransferLook.importOf theme, TransferTiming.ofTheme theme, overlay, topLevel, submit)

    member _.IsOpen = active
    member _.Preview = preview
    member _.Sheet = sheet
    member _.StatusText = sheet.StatusText
    member _.DetailsText = sheet.DetailsText
    member _.ImportButton = sheet.ImportButton
    member _.PickButton = sheet.PickButton

    /// 打开对话框（宽 480），焦点落在选文件
    member _.Show() =
        active <- true
        preview <- None
        attempts.Reset()
        setStatus (ImportView.noFile) ImportTone.Muted
        sheet.SetDetails None
        refresh ()
        overlay.ShowDialog(sheet.View, timing.DialogWidth(), onClosed = (fun () -> active <- false; preview <- None))
        sheet.FocusPick()

    /// 读到文件内容后的唯一入口（选文件窗口与测试都走这里）
    member _.LoadText(fileName: string, byteCount: int64, text: string) = loadText fileName byteCount text
