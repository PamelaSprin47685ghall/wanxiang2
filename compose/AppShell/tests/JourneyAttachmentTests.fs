module Wanxiang2.AppShell.Tests.JourneyAttachmentTests

// 展望 §2 旅程 3：**在旅程内**验「附件上线」。
//
// 这里的「上线」由主机 ACK 决定：`attachment.complete` 只是「客户端发完了」，
// 真正让附件就绪的是主机的 `attachment.committed`
// （状态树见 `libs/WorkspaceState/Update.fs` 的 `WireEvent.AttachmentCommitted`）。
//
// 本文件刻意不直接派发状态消息：认证、目录、ACK 全部经**真实的线协议事件入口**
// （`FakePorts.Deliver` → 宿主事件订阅 → `ShellComposer.HandleEvent`），
// 附件入口是**真实的「添加附件」按键**（`ComposerBox.AttachButton`），
// 发送入口是**真实的发送按键**（`ComposerBox.SendButton`）。
// 断言看的是真实控件树的状态（发送按键的可用性与提示、托盘里的 chip 状态字）
// 与真实出线的命令（`WireEvent.Command (ClientCommand.SendUserMessage …)`）。

open System.Text.Json.Nodes
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI
open Wanxiang2.AppShell.Tests

/// 一个真实可用的模型服务（目录里必须有 id 与非空 models，composer 才可用）。
let private catalogProviders =
    let provider = JsonObject()
    provider["id"] <- JsonValue.Create "p1"
    provider["label"] <- JsonValue.Create "P1"
    provider["models"] <- JsonArray([| JsonValue.Create "m1" :> JsonNode |])
    JsonArray([| provider :> JsonNode |])

/// 认证 + 目录都走真实线协议事件。
let private connect (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "inst-1" |})

    let tools = JsonArray()
    let generation = JsonObject()

    rig.Fake.Deliver(
        CatalogSnapshot
            {|
                providers = catalogProviders
                tools = tools
                generation = generation
            |}
    )

    Pump.settle ()

/// 点真按键（无头环境下没有输入设备，走无障碍调用）。
let private click (button: Button) =
    let peer =
        Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement button

    ((peer :> obj) :?> Avalonia.Automation.Provider.IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

/// 托盘 chip 上的文本（真实控件树里的 TextBlock）。
/// 托盘挂的是普通控件树，视觉树未必已布局，所以按子控件显式遍历。
let rec private texts (c: Control) : string list =
    let own =
        match c with
        | :? TextBlock as tb -> [ tb.Text ]
        | _ -> []

    let children =
        match c with
        | :? Panel as p -> p.Children |> Seq.cast<Control> |> Seq.toList
        | :? Decorator as d when not (isNull d.Child) -> [ d.Child ]
        | :? ContentControl as cc when not (isNull cc.Content) ->
            match cc.Content with
            | :? Control as child -> [ child ]
            | _ -> []
        | _ -> []

    own @ (children |> List.collect texts)

let private trayText (rig: Rig) : string =
    rig.Views.Composer.Tray.Strip |> texts |> String.concat "\u0001"

/// 已出线的「发送用户消息」命令。
let private sentMessages (rig: Rig) =
    rig.Fake.Events
    |> List.choose (fun ev ->
        match ev with
        | WireEvent.Command(ClientCommand.SendUserMessage d) -> Some d
        | _ -> None)

[<Fact>]
let ``旅程 3：附件在旅程内上线——主机确认前发不出去，确认后才带附件发出`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        connect rig

        // 1. 真实输入框里打字 → 文本进状态树。
        rig.Views.Composer.Box.Input.Text <- "你好"
        Pump.settle ()
        Assert.Equal("你好", rig.Shell.Model.Composer.Text)

        // 2. 真实「添加附件」按键 → 宿主真实上传入口（AttachmentBegin/Chunk/Complete）。
        let bytes = [| for i in 1..3 -> byte i |]
        rig.Fake.OnPickAttachment <- fun () -> rig.Shell.AttachFiles (rig.Shell.UploadOwner()) [ "pic.png", bytes ]
        click rig.Views.Composer.Box.AttachButton
        Pump.settle ()

        Assert.Equal(1, rig.Views.Composer.Tray.Count)
        let hasEvent (f: WireEvent -> bool) = rig.Fake.Events |> List.exists f

        Assert.True(
            hasEvent (function
                | WireEvent.AttachmentBegin _ -> true
                | _ -> false),
            "attachment.begin 应已发出"
        )

        Assert.True(
            hasEvent (function
                | WireEvent.AttachmentChunk _ -> true
                | _ -> false),
            "attachment.chunk 应已发出"
        )

        Assert.True(
            hasEvent (function
                | WireEvent.AttachmentComplete _ -> true
                | _ -> false),
            "attachment.complete 应已发出"
        )

        // 3. 主机还没确认：附件在上传中，真实发送按键必须是禁的，提示说清在等什么。
        let draft = rig.Shell.Model.Composer.Attachments |> List.head
        Assert.False draft.Ready
        Assert.False rig.Views.Composer.Box.SendButton.IsEnabled
        Assert.Equal("附件上传中，请稍候…", ToolTip.GetTip(rig.Views.Composer.Box.SendButton) :?> string)
        Assert.Contains("上传中", trayText rig)

        // 门控真的挡着：走真实控件的提交入口，什么也不该发出去。
        rig.Views.Composer.Box.Submit()
        Pump.settle ()
        Assert.Empty(sentMessages rig)

        // 4. 主机 ACK 走真实线协议事件入口。
        rig.Fake.Deliver(
            WireEvent.AttachmentCommitted
                {|
                    attachmentId = draft.Id
                    sha256 = UploadFacts.sha256Hex bytes
                    size = int64 bytes.Length
                |}
        )

        Pump.settle ()

        Assert.True(rig.Shell.Model.Composer.Attachments |> List.head |> fun a -> a.Ready)
        Assert.True rig.Views.Composer.Box.SendButton.IsEnabled
        // chip 状态字从「上传中」变成文件大小（就绪）。
        Assert.Contains("3 B", trayText rig)
        Assert.DoesNotContain("上传中", trayText rig)

        // 5. 点真实发送按键 → 命令真的带上附件发出去。
        click rig.Views.Composer.Box.SendButton
        Pump.settle ()

        let sent = sentMessages rig
        Assert.Equal(1, sent.Length)
        let contents = sent.[0].messageJson["contents"].AsArray()
        let textNode = contents.[0]["text"]
        Assert.Equal("你好", textNode.GetValue<string>())
        let att = contents.[1]
        let attType = att["type"]
        let attName = att["fileName"]
        let attMedia = att["mediaType"]
        let attSize = att["size"]
        let attSha = att["sha256"]
        Assert.Equal("attachment", attType.GetValue<string>())
        Assert.Equal("pic.png", attName.GetValue<string>())
        Assert.Equal("image/png", attMedia.GetValue<string>())
        Assert.Equal(3L, attSize.GetValue<int64>())
        Assert.Equal(UploadFacts.sha256Hex bytes, attSha.GetValue<string>()))
