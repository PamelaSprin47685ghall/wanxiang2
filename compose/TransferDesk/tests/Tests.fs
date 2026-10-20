module Wanxiang2.TransferDesk.Tests.Tests

// 真实组合：主题外观 + OverlayDesk 对话层 + ConversationExportController / ServerModel 真实分页 + ConversationTransfer。

open System
open System.Text.Json.Nodes
open System.Threading.Tasks
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.TransferDesk
open Wanxiang2.DeskTests

let private userMessageJson (text: string) : JsonNode =
    let o = JsonObject()
    o["role"] <- "user"
    let contents = JsonArray()
    let tc = JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let private testConfig () = { SessionConfig.empty with provider = "openai"; model = "test-model" }

let private append events (proj: Projection) =
    let commit = Events.Commit.create (proj.latestCommitId + 1UL) DateTimeOffset.UtcNow events
    match Projection.applyCommit proj commit with
    | Ok updated -> updated
    | Error error -> failwith (WanxiangError.message error)

let private fixture count =
    let id = Guid.NewGuid()
    let mutable proj = Projection.empty |> append [ ConversationCreated { conversationId = id; title = "旅行计划"; config = testConfig () } ]
    for n in 1 .. count do
        proj <- proj |> append [ AgentMessageRecorded { conversationId = id; payloadJson = userMessageJson (sprintf "message-%04d" n) } ]
    proj, id

let private forkLineage parent =
    { parentId = Some parent; parentTitle = Some "原对话"; parentDeleted = false; forkOrdinal = Some 3
      forkBaseMessageId = Some 4UL; forks = [ Guid.NewGuid(), "子分叉", Some 9UL ] }

let private exported count =
    let proj, id = fixture count
    let controller = ConversationExportController id
    let mutable next = Some(controller.Start())
    while next.IsSome do
        next <- controller.Accept(match ServerModel.exportPage proj next.Value with Ok p -> p | Error e -> failwith e)
    let doc = controller.Document.Value
    ConversationTransfer.toJson id doc.title doc.atCommitId DateTimeOffset.UtcNow (forkLineage (Guid.NewGuid())) doc.raw

let private theme () = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)

let private invoke (control: Control) =
    ControlAutomationPeer.CreatePeerForElement control
    |> Assert.IsAssignableFrom<IInvokeProvider>
    |> fun provider -> provider.Invoke()
    Dispatcher.UIThread.RunJobs()

// 迁自 R14TransferTests。
[<Fact>]
let ``313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`` () =
    Headless.run (fun () ->
        let root = Grid()
        let window = Desk.show root 900.0 700.0
        let overlay = OverlayDesk(root, Desk.look ())
        let submitted = ResizeArray<ImportPreview>()
        let mutable finish: bool -> unit = ignore
        let dialog = ImportDesk(theme (), overlay, (fun () -> window :> TopLevel), fun p onDone -> submitted.Add p; finish <- onDone)
        dialog.Show()
        Dispatcher.UIThread.RunJobs()
        Assert.True overlay.Layer.IsDialogOpen
        Assert.True dialog.PickButton.IsFocused
        Assert.False dialog.ImportButton.IsVisible
        dialog.LoadText("bad.md", 10L, "# 不是 JSON")
        Assert.Contains("不是有效的 JSON", dialog.StatusText)
        Assert.False dialog.ImportButton.IsVisible
        let text = exported 2
        dialog.LoadText("旅行计划.json", int64 text.Length, text)
        Assert.True dialog.ImportButton.IsVisible
        Assert.Contains("2 条消息", dialog.StatusText)
        Assert.Contains("旅行计划（导入）", dialog.DetailsText)
        Desk.key dialog.ImportButton Key.Enter
        Assert.Equal(1, submitted.Count)
        finish false
        Assert.True overlay.Layer.IsDialogOpen
        Assert.Contains("没有完成", dialog.StatusText)
        Assert.True dialog.ImportButton.IsEnabled
        Desk.key dialog.ImportButton Key.Enter
        finish true
        Assert.False overlay.Layer.IsDialogOpen
        window.Close())

[<Fact>]
let ``312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`` () =
    Headless.run (fun () ->
        let proj, id = fixture 3
        let root = Grid()
        let window = Desk.show root 900.0 700.0
        let overlay = OverlayDesk(root, Desk.look ())
        let requested = ResizeArray<ConversationExportQuery>()
        let dialog =
            ExportDesk(theme (), overlay, id, "旅行计划", (fun q -> requested.Add q; Task.FromResult true), (fun () -> window :> TopLevel), forkLineage (Guid.NewGuid()))
        dialog.Show()
        Dispatcher.UIThread.RunJobs()
        let mutable i = 0
        while i < requested.Count do
            match ServerModel.exportPage proj requested[i] with
            | Ok p -> dialog.Handle p
            | Error e -> failwith e
            i <- i + 1
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("「原对话」第 3 条消息的分叉", dialog.MarkdownText.Value)
        Assert.Contains("message-0003", dialog.MarkdownText.Value)
        Assert.Contains("\"kind\": \"fork\"", dialog.JsonText.Value)
        let texts = Desk.texts root |> String.concat "\n"
        Assert.Contains("不含 API 密钥", texts)
        Assert.Contains("保存 JSON", texts)
        Assert.True dialog.Sheet.SaveButton.IsVisible
        overlay.CloseDialog()
        Assert.False dialog.IsOpen
        window.Close())

// 迁自 ConversationExportTests：读取中「取消导出」原地进入取消态并给「重新导出」，重新导出回到读取态。
[<Fact>]
let ``cancel stays in the dialog and offers the retry entry`` () =
    Headless.run (fun () ->
        let root = Grid()
        let window = Desk.show root 480.0 600.0
        let overlay = OverlayDesk(root, Desk.look ())
        let dialog = ExportDesk(theme (), overlay, Guid.NewGuid(), "取消后可重试", (fun _ -> Task.FromResult true), fun () -> window :> TopLevel)
        dialog.Show()
        Dispatcher.UIThread.RunJobs()
        let byName name = (Desk.named root name).Value
        invoke (byName "取消导出")
        Assert.True overlay.Layer.IsDialogOpen
        Assert.Contains("导出已取消。", Desk.texts root)
        let retry = byName "重新导出"
        Assert.True retry.IsVisible
        invoke retry
        let bars = root.GetVisualDescendants() |> Seq.choose (function :? ProgressBar as bar -> Some bar | _ -> None) |> Seq.toList
        Assert.Equal(1, bars.Length)
        Assert.True bars.Head.IsVisible
        Assert.False retry.IsVisible
        // 非读取态下同一键是「关闭」
        dialog.Disconnect()
        Dispatcher.UIThread.RunJobs()
        invoke (byName (AutomationProperties.GetName dialog.Sheet.CloseButton))
        Assert.False overlay.Layer.IsDialogOpen
        window.Close())

// 迁自 PresentationRefreshTests：减弱动效时进度条是确定态（经主题的减弱动效开关）。
[<Fact>]
let ``export progress bar is determinate under reduced motion`` () =
    Headless.run (fun () ->
        let t = theme ()
        t.Motion.SetReduced true
        let root = Grid()
        let window = Desk.show root 480.0 600.0
        let overlay = OverlayDesk(root, Desk.look ())
        let dialog = ExportDesk(t, overlay, Guid.NewGuid(), "减弱动效", (fun _ -> Task.FromResult true), fun () -> window :> TopLevel)
        dialog.Show()
        Dispatcher.UIThread.RunJobs()
        Assert.True dialog.Sheet.Progress.IsVisible
        Assert.False dialog.Sheet.Progress.IsIndeterminate
        overlay.CloseDialog()
        window.Close())
