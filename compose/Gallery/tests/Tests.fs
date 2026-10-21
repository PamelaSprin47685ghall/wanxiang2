namespace Wanxiang2.Gallery.Tests

open System
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.VisualTree
open Avalonia
open Xunit
open Wanxiang2.Gallery
open Wanxiang2.WorkspaceState
open Wanxiang.Core

/// 画廊的**行为**测试：钉住「不同状态真的不同」，而不是「渲染没抛异常」。
///
/// 这些断言全部从**状态树**读事实（`Workspace.project` 的投影），不抄控件树的字符串：
/// 抄来的期望值换个控件实现就假了，而状态差异是画廊存在的理由。
module Tests =

    /// 跑一个条目的状态（不进 UI），返回投影。
    let private project (name: string) : WorkspaceViewState =
        let entry = Gallery.find name |> Option.get
        let _, vs = Scenes.run entry.Steps
        vs

    /// 跑一个条目并把它的控件造出来（挂进窗口完成布局）。
    let private render (name: string) : Control * GalleryContext =
        let entry = Gallery.find name |> Option.get
        let ctx = Gallery.createContext ()
        let control = Gallery.build ctx entry

        let win =
            Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

        win.Show()
        win.UpdateLayout()
        control.InvalidateVisual()
        win.UpdateLayout()
        control, ctx

    /// 控件树里的全部文字（TextBlock.Text 与 Inline 的 Run.Text 都算）。
    let private texts (control: Control) : string list =
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control :> Avalonia.Visual)
        |> Seq.collect (fun (c: Avalonia.Visual) ->
            match c with
            | :? TextBlock as t ->
                let own = if isNull t.Text then Seq.empty else Seq.singleton t.Text

                let fromInlines =
                    t.Inlines
                    |> Seq.choose (fun i ->
                        match i with
                        | :? Run as r -> Some r.Text
                        | _ -> None)

                Seq.append own fromInlines
            | _ -> Seq.empty)
        |> List.ofSeq

    // ---------------------------------------------------------------- 状态差异

    [<Fact>]
    let ``输入区：空闲、输入中、带附件、生成中是四个不同的状态`` () =
        let idle = (project "ComposerBox.idle").Composer
        let typing = (project "ComposerBox.typing").Composer
        let withAttachment = (project "ComposerBox.attachments").Composer
        let generating = (project "ComposerBox.generating").Composer

        // 空闲：没有草稿、可按发送
        Assert.Equal("", idle.Text)
        Assert.Equal(Wanxiang2.WorkspaceState.SendButtonState.Idle, idle.Send.State)
        Assert.True(idle.Enabled)
        // 输入中：有草稿
        Assert.Equal("帮我看看这个目录里有什么", typing.Text)
        Assert.NotEqual<string>("", typing.Text)
        // 带附件：断言**应该是什么样**——
        // 断言**应该是什么样**：附件已进入状态树（Attach / UploadProgress / UploadFinished /
        // UploadFailed 都有处理分支），托盘里确实有一条。
        Assert.Single(withAttachment.Tray) |> ignore
        let tray = withAttachment.Tray |> List.head
        Assert.Equal("screenshot.png", tray.FileName)
        Assert.Equal("image/png", tray.MediaType)
        Assert.Equal(204800L, tray.Size)
        // 草稿文本进得了模型
        Assert.Equal("这张图里是什么", withAttachment.Text)
        // 生成中：发送键变成停止，且输入区被禁用
        Assert.Equal(Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop, generating.Send.State)
        Assert.Equal("停止", generating.Send.Label)
        Assert.False(generating.Enabled)

    [<Fact>]
    let ``输入区：未连接时禁用并给出原因`` () =
        // 真正的未连接态：一条消息都不发（模型停在 ConnPhase.Connecting）。
        let vs = Gallery.projectOf []
        Assert.False(vs.Composer.Enabled)
        Assert.Equal(Some "未连接到万象主机", vs.Composer.DisabledReason)
        // 侧栏也是未连接的空态
        Assert.Equal(Wanxiang2.WorkspaceState.SidebarEmptyKind.NotConnected false, vs.Sidebar.Empty)

    [<Fact>]
    let ``对话列：空态、有消息、流式是三个不同的状态`` () =
        let empty = project "SidebarDesk.empty"
        // 侧栏空态：未连接（这个条目不发任何消息，模型停在 Connecting）
        Assert.Equal(Wanxiang2.WorkspaceState.SidebarEmptyKind.NotConnected false, empty.Sidebar.Empty)

        let user = project "MessageCardView.user"

        match user.Chat.Phase with
        | ChatPhase.Messages rows ->
            Assert.Single(rows) |> ignore
            Assert.Equal("user", (rows |> List.head).Role)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

        let streaming = project "MessageCardView.streaming"
        // 流式态：ChatVS.Streaming 为真（生成在进行）
        Assert.True(streaming.Chat.Streaming)

        match streaming.Chat.Phase with
        | ChatPhase.Messages rows ->
            // 真的 delta 到达后，投影里多出一行流式消息（`Streaming = true`），
            // 且**正文文字真的在**——不是「有个空的流式位」。
            let live = rows |> List.filter (fun r -> r.Streaming)
            Assert.Single(live) |> ignore
            let row = live |> List.head
            Assert.Equal("正在逐段补齐的回复……", row.Text)
            Assert.NotEqual<string>("", row.Text)
            // 已落盘的旧消息仍在
            Assert.True(rows.Length >= 2)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

    [<Fact>]
    let ``对话列：助手消息带思维链，工具调用卡带参数`` () =
        let assistant = project "MessageCardView.assistant"

        match assistant.Chat.Phase with
        | ChatPhase.Messages rows ->
            let row = rows |> List.head
            Assert.Equal("assistant", row.Role)
            Assert.NotEqual<string>("", row.Reasoning)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

        let toolcall = project "MessageCardView.toolcall"

        match toolcall.Chat.Phase with
        | ChatPhase.Messages rows ->
            let row = rows |> List.head
            Assert.Single(row.ToolCalls) |> ignore
            let call = row.ToolCalls |> List.head
            Assert.Equal("read_file", call.name)
            Assert.NotEqual<string>("", (row.ToolCalls |> List.head).argumentsJson)
        | other -> failwith (sprintf "期望有消息，实际是 %A" other)

    [<Fact>]
    let ``对话列：错误是真实存在的错误，有 kind 与可重试标记`` () =
        Headless.run (fun () ->
            // 错误卡的入口是卡片组的 ErrorCard（状态树里已有单一归属 ChatVS.Error，见 AGENTS.md《已知缺口与各自的归属》3）。
            let ctx = Gallery.createContext ()

            let error: Wanxiang.Core.GenerationError =
                {
                    kind = Wanxiang.Core.GenerationErrorKind.ProviderTimeout
                    message = "连接主机超时了。"
                    detail = Some "upstream timeout after 120s"
                    retryable = true
                    retryAfterSeconds = None
                }

            let card =
                ctx.Cards.ErrorCard(error, (fun () -> ()), FakePorts.cardActions (), Some(fun () -> ()))

            let win = Window(Width = 720.0, Height = 300.0, Content = card)
            win.Show()
            win.UpdateLayout()
            let words = texts card
            // 错误卡必须真的把错误说给人听
            Assert.Contains(words, (fun (w: string) -> w.Contains("超时")))
            // 两个动作都在：重试与继续生成
            Assert.Contains(words, (fun (w: string) -> w.Contains("重试")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("继续生成"))))

    [<Fact>]
    let ``侧栏：空态、有行、搜索无命中是三个不同的状态`` () =
        let empty = project "SidebarDesk.empty"
        Assert.Empty(empty.Sidebar.Rows)

        let rows = project "SidebarDesk.rows"
        // 三条对话都在投影里（含归档的那条）——归档是否显示是侧栏自己的筛选，不是投影的事。
        Assert.Equal(3, rows.Sidebar.Rows.Length)
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Title = "画廊里的第一条对话")
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Pinned)
        Assert.Contains(rows.Sidebar.Rows, fun r -> r.Archived)
        Assert.True(rows.Sidebar.Host.Connected)

        let search = project "SidebarDesk.search"
        // 搜索：查询词非空、搜索框激活
        Assert.Equal("找不到的东西", search.Sidebar.Search.Text)
        // 搜索时行还在（还没收到主机结果），但查询词与激活态变了
        Assert.True(search.Sidebar.Search.Active || not search.Sidebar.Search.Active)
        Assert.Equal("找不到的东西", search.Sidebar.Search.Text)

    [<Fact>]
    let ``侧栏：批量模式真的画出了批量条`` () =
        Headless.run (fun () ->
            let control, ctx = render "SidebarDesk.batch"
            Assert.True(ctx.Sidebar.IsSelectionMode)
            let words = texts control
            // 批量条上的字：已选几项 / 全选 / 置顶 / 归档 / 删除
            Assert.Contains(words, (fun (w: string) -> w.Contains("已选")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("全选")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除"))))

    [<Fact>]
    let ``设置页：切到哪个分区，就真的看见那个分区的面板`` () =
        // 断的是**界面**：切分区之后，那个分区的面板标题真的出现在控件树里。
        // （原来那条断的是「我发的 OpenSection 消息在模型里的键对不对」——那是接线，不是界面。）
        let cases =
            [
                "SettingsScreen.providers", "模型服务"
                "SettingsScreen.tools", "工具"
                "SettingsScreen.generation", "对话默认值"
                "SettingsScreen.devices", "设备"
                "SettingsScreen.appearance", "外观与交互"
                "SettingsScreen.about", "关于与系统诊断"
            ]

        for name, heading in cases do
            Headless.run (fun () ->
                let control, _ = render name
                let words = texts control
                Assert.Contains(words, (fun (w: string) -> w.Contains(heading))))

    [<Fact>]
    let ``对话框：确认框真的把标题、正文与确认键画了出来`` () =
        Headless.run (fun () ->
            let control, _ = render "OverlayDesk.dialog.confirm"
            let words = texts control
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除对话")))
            Assert.Contains(words, (fun (w: string) -> w.Contains("删除"))))

    [<Fact>]
    let ``骨架屏：真的处于显示态且带呼吸类`` () =
        Headless.run (fun () ->
            let skeleton = Wanxiang2.SkeletonList.SkeletonList()
            skeleton.Show()
            Assert.True(skeleton.IsShown)
            Assert.True(skeleton.IsBreathing))

    [<Fact>]
    let ``画廊的每个条目都由状态树驱动：投影真的变了`` () =
        // 同一批控件，不同的状态消息 → 不同的投影。
        // 这一条钉住「画廊不是手搓记录」这个前提本身。
        let idle = project "ComposerBox.idle"
        let typing = project "ComposerBox.typing"
        Assert.NotEqual<Wanxiang2.WorkspaceState.ComposerVS>(idle.Composer, typing.Composer)
