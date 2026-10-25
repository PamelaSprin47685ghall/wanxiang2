module Wanxiang2.AppShell.Tests.LayoutAuditTests

// 实证布局审计（回归大修）：单控件画廊与组合者逻辑测试都绿，用户却看到
// 「到处溢出 / 排版混乱 / 按钮时灵时不灵 / 流程跑不通」。缺的是**全组合**的
// 布局层验证：把真实 Shell（侧栏 + 顶栏 + 消息区 + 输入区）装进 1280×800
// 无头窗口，走真实线协议事件与真实按键（automation invoke），在每个旅程状态
// 扫描整棵视觉树：
//   A) ScrollViewer 横向溢出：extent > viewport（横向禁滚 = 内容被裁或被挤出）
//   B) 不在任何滚动区内、绝对矩形却出窗的可见控件（真·溢出）
//   C) 两枚可见可命中按钮的绝对矩形交叠 > 2px（命中偷抢 →「按钮时灵时不灵」）
//   D) NoWrap 且无省略号的 TextBlock 想要的宽度超过实得宽度（截断文案）
//   E) 可见可命中的 Button/TextBox 任一边 < 12px（点不中）
//   F) 可见却不透明、却零尺寸的按钮（点了个寂寞）
// 状态序列：S0 初始 → S1 已连接 → S2 打开会话（点真实会话行 + 真实快照）
// → S3 输入框打字 → S4 点真实发送键（并断言 SendUserMessage 出线）→ S5 窄窗。
// 任何发现都算测试失败，失败信息带阶段 / 控件类型 / 可访问名 / 绝对矩形。

open System
open System.IO
open System.Text
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.AppShell.Tests
open Wanxiang2.ShellLayout
open Wanxiang2.ThemePalette
open Wanxiang2.WorkspaceState

// ---------- 树工 ----------

/// 可见性：沿视觉祖先链全可见才可见（折叠的面板不出现在审计里）。
let private effectivelyVisible (c: Control) =
    let rec up (v: Visual) =
        if isNull v then
            true
        else
            match v with
            | :? Control as ctrl when not ctrl.IsVisible -> false
            | _ -> up (v.GetVisualParent())

    up c

/// 有效不透明度：自己与祖先的 Opacity 连乘（生成芯片靠它隐身）。
let private opacityOf (c: Control) =
    let mutable o = c.Opacity
    let mutable cur = c.GetVisualParent()

    while not (isNull cur) && o > 0.01 do
        match cur with
        | :? Control as p -> o <- o * p.Opacity
        | _ -> ()

        cur <- cur.GetVisualParent()

    o

/// 绝对矩形：Bounds 相对视觉父级，沿祖先累加。
let private absRect (c: Control) =
    let mutable x = c.Bounds.X
    let mutable y = c.Bounds.Y
    let mutable cur = c.GetVisualParent()

    while not (isNull cur) do
        x <- x + cur.Bounds.X
        y <- y + cur.Bounds.Y
        cur <- cur.GetVisualParent()

    Rect(Point(x, y), c.Bounds.Size)

/// 可访问名（finding 里定位控件用）。
let private autoName (c: Control) =
    let n = AutomationProperties.GetName c

    if isNull n then "" else n

/// 逻辑子树文本（找控件 / 预览截断文案用）。
let rec private texts (c: Control) : string list =
    let own =
        match c with
        | :? TextBlock as tb ->
            [
                if not (isNull tb.Text) then
                    tb.Text
            ]
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

let private preview (s: string) =
    if s.Length > 14 then s.Substring(0, 14) + "…" else s

let private tag (c: Control) =
    let r = absRect c

    let label =
        if String.IsNullOrWhiteSpace(autoName c) then
            ""
        else
            sprintf "「%s」" (autoName c)

    sprintf "%s%s @(%0.0f,%0.0f %0.0f×%0.0f)" (c.GetType().Name) label r.X r.Y r.Width r.Height

// ---------- 审计 ----------

let private chainOf (c: Control) =
    c.GetVisualAncestors()
    |> Seq.choose (fun v ->
        match v with
        | :? Control as p -> Some p
        | _ -> None)
    |> Seq.truncate 12
    |> Seq.map (fun p ->
        sprintf "%s[%0.0f,%0.0f %0.0fx%0.0f]" (p.GetType().Name) p.Bounds.X p.Bounds.Y p.Bounds.Width p.Bounds.Height)
    |> String.concat " < "

/// 控件此刻真的落在屏上：在窗内，且落在每个祖先滚动区的视口里
/// （被滚走的命中区不参与交叠/截断审计，否则全是假阳性）。
let private onScreen (root: Control) (c: Control) =
    let size = root.Bounds.Size
    let r = absRect c

    if
        r.Right <= 0.0
        || r.Left >= size.Width
        || r.Bottom <= 0.0
        || r.Top >= size.Height
    then
        false
    else
        c.GetVisualAncestors()
        |> Seq.forall (fun v ->
            match v with
            | :? ScrollViewer as sv -> (absRect sv).Intersects r
            | _ -> true)

let private audit (stage: string) (root: Control) : string list =
    let findings = ResizeArray<string>()
    let size = root.Bounds.Size
    let w = size.Width
    let h = size.Height

    let all =
        root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Control as c -> Some c
            | _ -> None)
        |> Seq.filter (fun c -> effectivelyVisible c && c.Bounds.Width > 0.5 && c.Bounds.Height > 0.5)
        |> Seq.toList

    /// 可见且不透明但零尺寸的按钮：单独收集（上面 all 已把零尺寸排除）。
    let ghostButtons =
        root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Button as b -> Some b
            | _ -> None)
        |> Seq.filter (fun b ->
            effectivelyVisible b
            && opacityOf b > 0.05
            && (b.Bounds.Width <= 0.5 || b.Bounds.Height <= 0.5))
        |> Seq.toList

    let insideScroll (c: Control) =
        c.GetVisualAncestors()
        |> Seq.exists (fun v ->
            match v with
            | :? ScrollViewer -> true
            | _ -> false)

    // A：滚动区横向溢出。
    for c in all do
        match c with
        | :? ScrollViewer as sv when
            sv.Extent.Width > sv.Viewport.Width + 0.5
            && not (sv.Classes.Contains "code-scroll")
            ->
            findings.Add(
                sprintf
                    "%s｜A 滚动区横向溢出 extent=%0.0f viewport=%0.0f（差 %0.0f）：%s"
                    stage
                    sv.Extent.Width
                    sv.Viewport.Width
                    (sv.Extent.Width - sv.Viewport.Width)
                    (tag sv)
            )
        | _ -> ()

    // B：不在滚动区内的可见控件出了窗。
    for c in all do
        if not (insideScroll c) then
            let r = absRect c

            if r.Right > w + 0.5 || r.Left < -0.5 || r.Bottom > h + 0.5 then
                findings.Add(sprintf "%s｜B 控件出窗（窗 %0.0fx%0.0f）：%s 链：%s" stage w h (tag c) (chainOf c))

    // C：按钮交叠（命中偷抢）。
    let buttons =
        all
        |> List.filter (fun c -> c :? Button && c.IsHitTestVisible && opacityOf c > 0.05 && onScreen root c)
        |> List.map (fun c -> (c, absRect c))

    let isAncestor (a: Control) (b: Control) =
        b.GetVisualAncestors() |> Seq.exists (fun v -> Object.ReferenceEquals(v, a))

    for i in 0 .. buttons.Length - 2 do
        for j in i + 1 .. buttons.Length - 1 do
            let a, ra = buttons.[i]
            let b, rb = buttons.[j]

            if not (isAncestor a b) && not (isAncestor b a) then
                let ix = min ra.Right rb.Right - max ra.Left rb.Left
                let iy = min ra.Bottom rb.Bottom - max ra.Top rb.Top

                if ix > 2.0 && iy > 2.0 then
                    findings.Add(sprintf "%s｜C 按钮交叠 %0.0fx%0.0f：%s ↔ %s" stage ix iy (tag a) (tag b))

    // D：NoWrap 无省略号的 TextBlock 被裁宽。
    for c in all do
        match c with
        | :? TextBlock as tb when
            tb.TextWrapping = TextWrapping.NoWrap
            && tb.TextTrimming = TextTrimming.None
            && not (String.IsNullOrEmpty tb.Text)
            && onScreen root c
            ->

            if tb.DesiredSize.Width > tb.Bounds.Width + 1.0 then
                findings.Add(
                    sprintf
                        "%s｜D 截断文案「%s」desire=%0.0f actual=%0.0f"
                        stage
                        (preview tb.Text)
                        tb.DesiredSize.Width
                        tb.Bounds.Width
                )
        | _ -> ()

    // E：微型可命中控件。
    for c in all do
        if
            (c :? Button || c :? TextBox)
            && c.IsHitTestVisible
            && opacityOf c > 0.05
            && onScreen root c
        then
            let r = absRect c

            if r.Width < 12.0 || r.Height < 12.0 then
                findings.Add(sprintf "%s｜E 微型控件：%s" stage (tag c))

    // F：可见按钮零尺寸。
    for b in ghostButtons do
        findings.Add(sprintf "%s｜F 可见按钮零尺寸：%s（文字「%s」）" stage (tag b) (preview (texts b |> String.concat " ")))

    List.ofSeq findings

// ---------- 旅程 ----------

let private click (button: Button) =
    let peer = ControlAutomationPeer.CreatePeerForElement button
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private catalogProviders =
    let provider = JsonObject()
    provider["id"] <- JsonValue.Create "p1"
    provider["label"] <- JsonValue.Create "P1"
    provider["models"] <- JsonArray([| JsonValue.Create "m1" :> JsonNode |])
    JsonArray([| provider :> JsonNode |])

let private connect (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "audit-inst" |})

    rig.Fake.Deliver(
        CatalogSnapshot
            {|
                providers = catalogProviders
                tools = JsonArray()
                generation = JsonObject()
            |}
    )

    Pump.settle ()

let private conversationId = "11111111-1111-1111-1111-111111111111"

let private summaryItem id title last pinned archived running =
    JsonNode.Parse(
        sprintf
            """{"conversationId":"%s","title":"%s","lastMessage":"%s","pinned":%b,"archived":%b,"runtimeState":"%s","createdAt":"2026-10-20T08:00:00Z","updatedAt":"2026-10-21T08:00:00Z","messageCount":4,"isFork":false,"lastCommitId":12,"config":{"provider":"p1","model":"m1"}}"""
            id
            title
            last
            pinned
            archived
            (if running then "generating" else "idle")
    )

let private listSnapshot =
    let items =
        JsonArray(
            summaryItem conversationId "布局审计里的第一条对话" "上一条回复的摘要" true false false,
            summaryItem "22222222-2222-2222-2222-222222222222" "第二条对话" "另一条摘要" false false false,
            summaryItem "33333333-3333-3333-3333-333333333333" "归档的对话" "归档行" false true false
        )

    WireEvent.ConversationListSnapshot {| items = items; lastCommitId = 12UL |}

let private messageItem commitId role contents =
    JsonNode.Parse(
        sprintf
            """{"commitId":%d,"committedAt":"2026-10-21T08:00:00Z","payload":{"role":"%s","contents":%s}}"""
            commitId
            role
            contents
    )

let private textContent (text: string) =
    sprintf """[{"type":"text","text":"%s"}]""" text

let private longBody = String.replicate 6 "这是布局审计用的长消息正文，用来检验换行、阅读列宽与滚动裁剪是否各就各位。"

let private snapshotMessages =
    JsonArray(
        messageItem 1 "user" (textContent "布局审计标记甲乙丙丁"),
        messageItem 2 "assistant" (textContent "收到标记。下面是一段很长的回复，用来撑开阅读列。"),
        messageItem 3 "assistant" (textContent longBody),
        messageItem 4 "user" (textContent longBody),
        messageItem 5 "assistant" (textContent "短回复收尾。")
    )

let private conversationSnapshot =
    WireEvent.ConversationSnapshot
        {|
            conversationId = Guid.Parse conversationId
            title = "布局审计里的第一条对话"
            lastCommitId = 12UL
            runtimeState = "idle"
            generationId = None
            messages = snapshotMessages
            snapshotEarliestCommitId = 0UL
            snapshotHasMore = false
            config = SessionConfig.empty
        |}

/// 侧栏里按标题找真实会话行按钮（行宿主是 Button，文字在逻辑子树里）。
let private findRowButton (root: Control) (needle: string) : Button option =
    root.GetVisualDescendants()
    |> Seq.choose (fun v ->
        match v with
        | :? Button as b -> Some b
        | _ -> None)
    |> Seq.tryFind (fun b ->
        let named = autoName b

        (not (String.IsNullOrWhiteSpace named) && named.Contains needle)
        || (texts b |> List.exists (fun t -> not (isNull t) && t.Contains needle)))

/// 侧栏固定段骨架（诊断页脚下沉 / 列表不可见用）。
let private sidebarSkeleton (rig: Rig) =
    [
        rig.Views.Sidebar.Brand.View
        rig.Views.Sidebar.Search.View
        rig.Views.Sidebar.Batch.Header
        rig.Views.Sidebar.Batch.ActionBar
        rig.Views.Sidebar.Host.View
        rig.Views.Sidebar.EmptyCard.View
    ]
    |> List.map (fun c ->
        sprintf "%s[%0.0f,%0.0f %0.0fx%0.0f]" (c.GetType().Name) c.Bounds.X c.Bounds.Y c.Bounds.Width c.Bounds.Height)
    |> String.concat " ；"

/// 侧栏全部按钮的可访问名（行丢失断点诊断用）。
let private sidebarButtons (rig: Rig) =
    rig.Views.Sidebar.View.GetVisualDescendants()
    |> Seq.choose (fun v ->
        match v with
        | :? Button as b -> Some b
        | _ -> None)
    |> Seq.map (fun b ->
        let n = autoName b

        if String.IsNullOrWhiteSpace n then b.GetType().Name else n)
    |> String.concat " / "

[<Fact>]
let ``布局审计：全组合五状态无溢出无交叠且旅程可走通`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            let mutable findings = []

            // S0：初始（配对前）。
            findings <- findings @ audit "S0 初始" rig.Root

            // S1：真实线协议连接（认证 + 目录）。
            connect rig
            findings <- findings @ audit "S1 已连接" rig.Root

            // S2：列表 → 点真实会话行 → 真实对话快照。
            rig.Fake.Deliver listSnapshot
            Pump.settle ()

            match findRowButton rig.Views.Sidebar.View "布局审计里的第一条对话" with
            | None ->
                findings <-
                    findings
                    @ [
                        sprintf
                            "S2｜未找到会话行按钮（流程断点）：Summaries=%d 可见行=%d 按钮=[%s] 骨架：%s"
                            rig.Shell.Model.Summaries.Length
                            (rig.Views.Sidebar.GetVisibleOrder()).Length
                            (sidebarButtons rig)
                            (sidebarSkeleton rig)
                    ]
            | Some row ->
                let out0 = rig.Fake.Commands.Length + rig.Fake.Events.Length
                click row

                let out1 = rig.Fake.Commands.Length + rig.Fake.Events.Length

                if out1 = out0 then
                    findings <- findings @ [ "S2｜点会话行后既无命令也无事件出线（流程断点）" ]

            rig.Fake.Deliver conversationSnapshot
            Pump.settle ()

            // 会话真的渲染：标题进了顶栏、标记消息进了消息区。
            if not (rig.Views.Chat.Header.Title.Contains "布局审计里的第一条对话") then
                findings <- findings @ [ sprintf "S2｜顶栏标题未更新：「%s」" (rig.Views.Chat.Header.Title) ]

            let markerVisible =
                rig.Views.Chat.Host.GetVisualDescendants()
                |> Seq.exists (fun v ->
                    match v with
                    | :? TextBlock as tb -> not (isNull tb.Text) && tb.Text.Contains "布局审计标记甲乙丙丁"
                    | _ -> false)

            if not markerVisible then
                findings <- findings @ [ "S2｜消息标记未渲染（快照没落到消息区）" ]

            findings <- findings @ audit "S2 会话打开" rig.Root

            // S3：真实输入框打字。
            rig.Views.Composer.Box.Input.Text <- "布局审计的发送正文"
            Pump.settle ()
            findings <- findings @ audit "S3 输入中" rig.Root

            // S3.5：生成中（生成芯片、运行点、排队键同台）。
            rig.Fake.Deliver(
                WireEvent.ConversationSnapshot
                    {|
                        conversationId = Guid.Parse conversationId
                        title = "布局审计里的第一条对话"
                        lastCommitId = 12UL
                        runtimeState = "generating"
                        generationId = Some(Guid.NewGuid())
                        messages = snapshotMessages
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )

            Pump.settle ()
            findings <- findings @ audit "S3.5 生成中" rig.Root

            // 回到闲置再走发送旅程（生成中发送键会换成排队语义）。
            rig.Fake.Deliver conversationSnapshot
            Pump.settle ()

            // S4：点真实发送键 → SendUserMessage 必须出线。
            if not rig.Views.Composer.Box.SendButton.IsEnabled then
                findings <- findings @ [ "S4｜发送键不可用（流程断点）" ]
            else
                click rig.Views.Composer.Box.SendButton
                Pump.settle ()

                let sentNow =
                    rig.Fake.Commands
                    |> List.exists (fun cmd ->
                        match cmd with
                        | ClientCommand.SendUserMessage _ -> true
                        | _ -> false)

                if not sentNow then
                    findings <- findings @ [ "S4｜点发送后 SendUserMessage 未出线（流程断点）" ]

            findings <- findings @ audit "S4 已发送" rig.Root

            // S5：窄窗（溢出在窄窗最容易现形）。
            window.Width <- 480.0
            window.Height <- 640.0
            Pump.settle ()
            findings <- findings @ audit "S5 窄窗" rig.Root

            findings <-
                if findings.Length > 200 then
                    List.take 200 findings @ [ sprintf "……另有 %d 条" (findings.Length - 200) ]
                else
                    findings

            Assert.True(findings.IsEmpty, String.Join("\n", findings))
        finally
            window.Close())

/// 活树上的安排尺寸、画笔和字族。常量对常量不算通过。
[<Fact>]
let ``布局审计：活树符合设计稿的尺寸、放置、浮层与两套主题`` () =
    Headless.run (fun () ->
        let near (actual: float) (expected: float) (what: string) =
            Assert.True(abs (actual - expected) <= 1.5, sprintf "%s expected %0.1f actual %0.1f" what expected actual)

        let hex (brush: IBrush) =
            match brush with
            | :? SolidColorBrush as solid ->
                let c = solid.Color
                sprintf "#%02X%02X%02X" c.R c.G c.B
            | _ -> ""

        let bg (c: Control) =
            match c with
            | :? Border as b -> hex b.Background
            | :? Panel as p -> hex p.Background
            | :? Button as b -> hex b.Background
            | _ -> ""

        let family (tb: TextBlock) =
            if isNull tb.FontFamily then
                ""
            else
                tb.FontFamily.ToString()

        let blocks (root: Control) =
            root.GetVisualDescendants()
            |> Seq.choose (fun v ->
                match v with
                | :? TextBlock as tb -> Some tb
                | _ -> None)
            |> Seq.filter effectivelyVisible
            |> Seq.toList

        // 会话行标题写在 Run 里，Text 属性是 null。读排出来的文字，不读模型字段。
        let textOf (tb: TextBlock) =
            let inlineText =
                if isNull tb.Inlines || tb.Inlines.Count = 0 then
                    ""
                else
                    tb.Inlines
                    |> Seq.choose (fun part ->
                        match part with
                        | :? Run as run when not (isNull run.Text) -> Some run.Text
                        | _ -> None)
                    |> String.concat ""

            if inlineText.Length > 0 then inlineText
            elif isNull tb.Text then ""
            else tb.Text

        let findText (root: Control) (needle: string) =
            match blocks root |> List.tryFind (fun tb -> textOf tb |> fun t -> t.Contains needle) with
            | Some tb -> tb
            | None -> failwith (sprintf "找不到「%s」" needle)

        let buttonWith (root: Control) (needle: string) =
            root.GetVisualDescendants()
            |> Seq.choose (fun v ->
                match v with
                | :? Button as b when effectivelyVisible b -> Some b
                | _ -> None)
            |> Seq.tryFind (fun b ->
                let name = AutomationProperties.GetName b

                (not (isNull name) && name.Contains needle)
                || (texts b |> List.exists (fun t -> not (isNull t) && t.Contains needle)))

        let rec capped (v: Visual) =
            match v with
            | :? Control as lay when lay.MaxWidth > 0.0 && lay.MaxWidth < 4000.0 -> Some lay.MaxWidth
            | _ ->
                match v.GetVisualParent() with
                | null -> None
                | parent -> capped parent

        let midX (c: Control) =
            let r = absRect c
            r.X + r.Width / 2.0

        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()

        let resize (w: float) (h: float) =
            window.Width <- w
            window.Height <- h
            Dispatcher.UIThread.RunJobs()
            Pump.settle ()
            rig.Shell.Reproject()
            Pump.settle ()

        let composer = rig.Views.Composer.Host :?> Border
        let rail = rig.Views.Sidebar.View
        let bar = rig.Views.Chat.Header.Host

        // 留白档看窗口宽度，不看舞台宽度。阅读列的安排宽度必须落在舞台减去两侧留白之内。
        let assertPads (label: string) (expected: float) =
            near composer.Padding.Left expected label
            near composer.Padding.Right expected (label + " 右")
            near rig.Views.Chat.Pane.Scroller.Viewer.Padding.Left expected (label + " 阅读区")

            let emptyLeft =
                rig.Views.Chat.Pane.Host.GetVisualChildren()
                |> Seq.choose (fun v ->
                    match v with
                    | :? ScrollViewer as sv when sv <> rig.Views.Chat.Pane.Scroller.Viewer -> Some sv.Padding.Left
                    | _ -> None)
                |> Seq.toList

            Assert.NotEmpty emptyLeft

            for p in emptyLeft do
                near p expected (label + " 空态")

        let assertColumnWithin (expectedInset: float) =
            let stage = composer.Bounds.Width
            let budget = stage - 2.0 * expectedInset

            let cols =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Control as c when
                        abs (c.MaxWidth - 748.0) < 0.5 && effectivelyVisible c && c.Bounds.Width > 0.5
                        ->
                        Some c
                    | _ -> None)
                |> Seq.toList

            Assert.NotEmpty cols

            for c in cols do
                Assert.True(
                    c.Bounds.Width <= budget + 1.5,
                    sprintf "阅读列宽 %0.1f 舞台 %0.1f 留白 %0.0f 预算 %0.1f" c.Bounds.Width stage expectedInset budget
                )

                let r = absRect c
                let hostR = absRect composer

                Assert.True(
                    r.X >= hostR.X + expectedInset - 1.5
                    && r.Right <= hostR.Right - expectedInset + 1.5,
                    sprintf
                        "阅读列 [%0.1f,%0.1f] 超出内容区 [%0.1f,%0.1f]"
                        r.X
                        r.Right
                        (hostR.X + expectedInset)
                        (hostR.Right - expectedInset)
                )

        let mutable measuredEmpty = 0.0
        let mutable measuredReading = 0.0
        let mutable measuredInset16 = 0.0
        let mutable measuredSettings = 0.0
        let mutable measuredPalette = 0.0
        let mutable measuredConfirm = 0.0

        let overlaps (a: Rect) (b: Rect) =
            let i = a.Intersect b
            i.Width > 2.0 && i.Height > 2.0

        let note (line: string) =
            printfn "%s" line
            TestContext.Current.TestOutputHelper.WriteLine line

        let surfaceReport (label: string) (rowTitle: string) =
            let shellW = rig.Root.Bounds.Width
            let railW = rail.Bounds.Width
            let stageW = composer.Bounds.Width

            let bubble =
                rig.Root.GetVisualDescendants()
                |> Seq.tryPick (fun v ->
                    match v with
                    | :? Border as b when autoName b = "用户消息" && effectivelyVisible b -> Some b
                    | _ -> None)

            let bubbleText =
                match bubble with
                | None -> "bubble=MISSING"
                | Some b ->
                    let parentW =
                        match b.GetVisualParent() with
                        | :? Control as p -> p.Bounds.Width
                        | _ -> -1.0

                    let text =
                        b.GetVisualDescendants()
                        |> Seq.tryPick (fun v ->
                            match v with
                            | :? TextBlock as tb when effectivelyVisible tb && (textOf tb).Length > 0 -> Some tb
                            | _ -> None)

                    let assistantInside =
                        b.GetVisualDescendants()
                        |> Seq.exists (fun v ->
                            match v with
                            | :? ScrollViewer as sv when sv.Classes.Contains "code-scroll" -> true
                            | :? TextBlock as tb -> (textOf tb).Contains "abcdefghijklmnopqrstuvwxyz"
                            | _ -> false)

                    let br = absRect b

                    sprintf
                        "bubble w=%0.1f x=%0.1f right=%0.1f parentW=%0.1f align=%A radii=%0.1f/%0.1f/%0.1f/%0.1f fill=%s text=%s inside=%b"
                        b.Bounds.Width
                        b.Bounds.X
                        br.Right
                        parentW
                        b.HorizontalAlignment
                        b.CornerRadius.TopLeft
                        b.CornerRadius.TopRight
                        b.CornerRadius.BottomRight
                        b.CornerRadius.BottomLeft
                        (bg b)
                        (text |> Option.map (fun tb -> hex tb.Foreground) |> Option.defaultValue "")
                        assistantInside

            let hits =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Button as b when effectivelyVisible b -> Some b
                    | _ -> None)
                |> Seq.filter (fun b ->
                    let name = autoName b
                    let cap = texts b |> String.concat ""
                    name.Contains "复制" || name.Contains "分叉" || cap.Contains "复制" || cap.Contains "分叉")
                |> Seq.toList

            let hitText =
                hits
                |> List.map (fun b ->
                    let r = absRect b

                    let onBubble =
                        match bubble with
                        | Some bubble -> overlaps r (absRect bubble)
                        | None -> false

                    sprintf
                        "%s op=%0.2f hit=%b %0.0fx%0.0f onBubble=%b"
                        (autoName b)
                        (opacityOf b)
                        b.IsHitTestVisible
                        r.Width
                        r.Height
                        onBubble)
                |> String.concat " ;; "

            let mutual =
                hits
                |> List.pairwise
                |> List.filter (fun (a, b) -> overlaps (absRect a) (absRect b))
                |> List.map (fun (a, b) -> autoName a + "×" + autoName b)
                |> String.concat ","

            let code =
                rig.Root.GetVisualDescendants()
                |> Seq.tryPick (fun v ->
                    match v with
                    | :? ScrollViewer as sv when sv.Classes.Contains "code-scroll" -> Some sv
                    | _ -> None)

            let codeText =
                match code with
                | None -> "code=MISSING"
                | Some sv -> sprintf "code extent=%0.1f view=%0.1f" sv.Extent.Width sv.Viewport.Width

            let tool =
                rig.Root.GetVisualDescendants()
                |> Seq.tryPick (fun v ->
                    match v with
                    | :? Border as b when (autoName b).StartsWith "工具调用" && effectivelyVisible b -> Some b
                    | _ -> None)

            let toolText =
                match tool with
                | None -> "tool=MISSING"
                | Some card ->
                    let lines =
                        card.GetVisualDescendants()
                        |> Seq.choose (fun v ->
                            match v with
                            | :? TextBlock as tb when effectivelyVisible tb && (textOf tb).Length > 0 -> Some tb
                            | _ -> None)
                        |> Seq.map (fun tb ->
                            sprintf
                                "«%s» wrap=%A trim=%A max=%d bw=%0.1f bh=%0.1f dw=%0.1f"
                                (preview (textOf tb))
                                tb.TextWrapping
                                tb.TextTrimming
                                tb.MaxLines
                                tb.Bounds.Width
                                tb.Bounds.Height
                                tb.DesiredSize.Width)
                        |> String.concat " ;; "

                    sprintf "tool w=%0.1f h=%0.1f [%s]" card.Bounds.Width card.Bounds.Height lines

            let rowText =
                match findRowButton rail rowTitle with
                | None -> "row=MISSING"
                | Some row ->
                    let title = findText (row :> Control) "超长会话标题"

                    sprintf
                        "row h=%0.1f fill=%s edge=%0.1f brush=%s classes=%s title wrap=%A trim=%A bw=%0.1f bh=%0.1f dw=%0.1f"
                        row.Bounds.Height
                        (bg row)
                        row.BorderThickness.Left
                        (hex row.BorderBrush)
                        (row.Classes |> Seq.map string |> String.concat ",")
                        title.TextWrapping
                        title.TextTrimming
                        title.Bounds.Width
                        title.Bounds.Height
                        title.DesiredSize.Width

            note (
                sprintf
                    "PROBE %s shell=%0.1f rail=%0.1f stage=%0.1f | %s | hits=%s mutual=%s | %s | %s | %s"
                    label
                    shellW
                    railW
                    stageW
                    bubbleText
                    hitText
                    mutual
                    codeText
                    toolText
                    rowText
            )

        let oneLine (tb: TextBlock) (limit: float) =
            Assert.Equal(TextWrapping.NoWrap, tb.TextWrapping)
            Assert.Equal(TextTrimming.CharacterEllipsis, tb.TextTrimming)
            Assert.Equal(1, tb.MaxLines)

            Assert.True(
                tb.Bounds.Height <= tb.FontSize * 2.2,
                sprintf "行高 %0.1f 字号 %0.1f 「%s」" tb.Bounds.Height tb.FontSize (preview (textOf tb))
            )

            Assert.True(
                tb.Bounds.Width <= limit + 1.0,
                sprintf "宽 %0.1f 上限 %0.1f 「%s」" tb.Bounds.Width limit (preview (textOf tb))
            )

            Assert.True(
                tb.Bounds.Width < float (textOf tb).Length * 4.0,
                sprintf "没有收成一行：宽 %0.1f 字数 %d" tb.Bounds.Width (textOf tb).Length
            )

        let checkChrome (fill: string) (ink: string) (rowFill: string) (edge: string) (rowTitle: string) =
            let bubble =
                rig.Root.GetVisualDescendants()
                |> Seq.pick (fun v ->
                    match v with
                    | :? Border as b when autoName b = "用户消息" && effectivelyVisible b -> Some b
                    | _ -> None)

            Assert.Equal(HorizontalAlignment.Right, bubble.HorizontalAlignment)
            Assert.True(bubble.Bounds.Width <= 560.5, sprintf "气泡宽 %0.1f" bubble.Bounds.Width)
            let parent = bubble.GetVisualParent() :?> Control

            Assert.True(
                abs (bubble.Bounds.Right - parent.Bounds.Width) < 2.0,
                sprintf "气泡右缘 %0.1f 父宽 %0.1f" bubble.Bounds.Right parent.Bounds.Width
            )

            Assert.True(bubble.Bounds.X > 1.0, sprintf "气泡没有靠右，x=%0.1f" bubble.Bounds.X)
            near bubble.CornerRadius.TopLeft 12.0 "bubble tl"
            near bubble.CornerRadius.TopRight 12.0 "bubble tr"
            near bubble.CornerRadius.BottomRight 6.0 "bubble br"
            near bubble.CornerRadius.BottomLeft 12.0 "bubble bl"
            Assert.Equal(fill, bg bubble)

            let inkBlock =
                bubble.GetVisualDescendants()
                |> Seq.pick (fun v ->
                    match v with
                    | :? TextBlock as tb when (textOf tb).Length > 0 -> Some tb
                    | _ -> None)

            Assert.Equal(ink, hex inkBlock.Foreground)

            let assistantInside =
                bubble.GetVisualDescendants()
                |> Seq.exists (fun v ->
                    match v with
                    | :? ScrollViewer as sv when sv.Classes.Contains "code-scroll" -> true
                    | _ -> false)

            Assert.False assistantInside

            let named needle =
                match buttonWith rig.Root needle with
                | Some b -> b
                | None -> failwith (sprintf "没有「%s」" needle)

            let copyBtn = named "复制用户"
            let forkBtn = named "分叉"

            for b in [ copyBtn; forkBtn ] do
                Assert.True(effectivelyVisible b)
                Assert.True(opacityOf b > 0.5, autoName b)
                Assert.True(b.IsHitTestVisible, autoName b)
                let r = absRect b

                Assert.True(r.Width >= 12.0 && r.Height >= 12.0, sprintf "%s %0.0fx%0.0f" (autoName b) r.Width r.Height)
                Assert.False(overlaps r (absRect bubble), autoName b)

            Assert.False(overlaps (absRect copyBtn) (absRect forkBtn), "复制和分叉交叠")

            let card =
                rig.Root.GetVisualDescendants()
                |> Seq.pick (fun v ->
                    match v with
                    | :? Border as b when (autoName b).StartsWith "工具调用" && effectivelyVisible b -> Some b
                    | _ -> None)

            Assert.True(card.Bounds.Width <= 748.5, sprintf "工具卡宽 %0.1f" card.Bounds.Width)

            let lines =
                card.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? TextBlock as tb when effectivelyVisible tb && (textOf tb).Length > 0 -> Some tb
                    | _ -> None)
                |> Seq.toList

            oneLine (lines |> List.find (fun tb -> (textOf tb).StartsWith "调用 ")) card.Bounds.Width
            oneLine (lines |> List.find (fun tb -> (textOf tb).StartsWith "long_tool_name")) card.Bounds.Width

            let overview = named "调用 1 个工具"
            Assert.True(overview.Bounds.Width >= 44.0, sprintf "概览头宽 %0.1f" overview.Bounds.Width)
            Assert.True(overview.Bounds.Height >= 12.0, sprintf "概览头高 %0.1f" overview.Bounds.Height)

            match findRowButton rail rowTitle with
            | None -> Assert.Fail "当前行不在栏里"
            | Some row ->
                Assert.True(row.Bounds.Height >= 44.0, sprintf "行高 %0.1f" row.Bounds.Height)
                Assert.Equal(rowFill, bg row)
                near row.BorderThickness.Left 2.0 "row edge"
                Assert.Equal(edge, hex row.BorderBrush)
                let title = findText (row :> Control) "超长会话标题"
                Assert.Equal(TextWrapping.NoWrap, title.TextWrapping)
                Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming)

                Assert.True(
                    title.Bounds.Width < rail.Bounds.Width - 8.0,
                    sprintf "标题宽 %0.1f 栏宽 %0.1f" title.Bounds.Width rail.Bounds.Width
                )

                near rail.Bounds.Width 284.0 "rail with long title"

        try
            resize 1280.0 800.0
            connect rig
            resize 1280.0 800.0

            near rail.Bounds.Width 284.0 "rail"
            Assert.True(effectivelyVisible (findText rail "万"))
            Assert.Contains("万象", (findText rail "万象").Text)
            Assert.Contains("Noto Serif", family (findText rail "万"))
            Assert.Contains("Noto Serif", family (findText rail "万象"))
            Assert.Equal("新对话", AutomationProperties.GetName rig.Views.Sidebar.Brand.NewButton)

            Assert.True(
                rail.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? TextBox as box -> Some box
                    | _ -> None)
                |> Seq.exists (fun box -> box.PlaceholderText = "搜索标题和正文")
            )

            Assert.True(blocks rail |> List.exists (fun tb -> tb.Text = "已连接" || tb.Text = "选择主机"))

            near bar.Bounds.Height 52.0 "top bar"
            Assert.True(bar.IsVisible)
            Assert.Equal("新对话", rig.Views.Chat.Header.Title)
            Assert.Contains("Noto Serif", family (findText bar "新对话"))
            Assert.True((absRect bar).Y < 1.5, "顶栏贴在舞台顶部")

            let lead = findText rig.Root "从一句开始"
            Assert.Contains("Noto Serif", family lead)

            match capped lead with
            | Some cap ->
                near cap 480.0 "empty cap"
                measuredEmpty <- cap
            | None -> Assert.Fail "空态没有 480 的宽度上限"

            let emptyBlock =
                let rec up (v: Visual) =
                    match v with
                    | :? Control as lay when abs (lay.MaxWidth - 480.0) < 0.5 -> lay
                    | _ -> up (v.GetVisualParent())

                up lead

            Assert.True(lead.Bounds.Width <= 480.5)

            for starter in [ "用三句话说明这台主机现在能做什么"; "指出一份 TOML 配置里的错误"; "把工具调用说成人话" ] do
                Assert.True(buttonWith rig.Root starter |> Option.isSome, starter)

            let shellBorder =
                composer.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Border as b when abs (b.CornerRadius.TopLeft - 16.0) < 0.1 -> Some b
                    | _ -> None)
                |> Seq.tryHead

            Assert.True(shellBorder.IsSome, "输入区圆角 16")

            match buttonWith composer "未选择" with
            | Some chip ->
                let attachR = absRect rig.Views.Composer.Box.AttachButton
                let chipR = absRect chip
                let sendR = absRect rig.Views.Composer.Box.SendButton
                Assert.True(attachR.X < chipR.X && chipR.X < sendR.X, "附件和模型在左，发送在右")
            | None -> Assert.Fail "没有模型芯片"

            Assert.Equal("先连接一个模型，草稿会留在这里", rig.Views.Composer.Box.Input.PlaceholderText)
            near (midX emptyBlock) (midX composer) "empty and composer share a horizontal center"
            Assert.True((absRect emptyBlock).Y >= (absRect bar).Bottom - 1.0, "空态在顶栏之下")

            let bottomGap () =
                rig.Root.Bounds.Height - (absRect composer).Bottom

            Assert.True(bottomGap () > 80.0, sprintf "起步时应成组居中，底空距 %0.1f" (bottomGap ()))

            let groupMid = ((absRect emptyBlock).Y + (absRect composer).Bottom) / 2.0

            let areaMid = ((absRect bar).Bottom + rig.Root.Bounds.Height) / 2.0

            Assert.True(abs (groupMid - areaMid) < 48.0, sprintf "组中心 %0.1f 舞台中心 %0.1f" groupMid areaMid)

            Assert.Equal("#F5F4ED", bg rig.Views.Workspace.Host)
            Assert.Equal("#F0EEE5", bg rail)
            Assert.Equal("#1B365D", bg rig.Views.Composer.Box.SendButton)

            let bodySample = findText rail "已连接"
            Assert.Contains("Noto Sans", family bodySample)

            rig.Shell.Dispatch(User(Composer(ComposerUserMsg.ModelPick("p1", "m1"))))
            Pump.settle ()
            Assert.Equal("写下一句", rig.Views.Composer.Box.Input.PlaceholderText)
            Assert.True(bottomGap () > 80.0, "选了模型仍是起步居中")

            resize 1280.0 800.0
            assertPads "inset at 1280" 48.0

            resize 1400.0 800.0
            near rail.Bounds.Width 284.0 "rail at 1400"
            assertPads "inset at 1400" 48.0

            resize 1100.0 800.0
            assertPads "inset at 1100" 48.0
            assertColumnWithin 48.0
            Assert.True(composer.Bounds.Width < 1024.0, sprintf "1100 窗口的舞台应窄于 1024，实际 %0.1f" composer.Bounds.Width)

            resize 800.0 800.0
            near rail.Bounds.Width 284.0 "rail at 800"
            Assert.True(effectivelyVisible rail, "800 仍是侧栏列，不是抽屉")
            assertPads "inset at 800" 32.0
            assertColumnWithin 32.0

            resize 1280.0 560.0
            Assert.True(bottomGap () < 48.0, sprintf "高度 560 应贴底，底空距 %0.1f" (bottomGap ()))

            resize 1280.0 800.0
            Assert.True(bottomGap () > 80.0, "回到 800 后重新居中")

            let longTitle = String.replicate 8 "超长会话标题必须在栏内省略"
            let codeLine = String.replicate 20 "abcdefghijklmnopqrstuvwxyz0123456789 "
            let userLine = String.replicate 6 "用户气泡应该停在五百六十的宽度里。"

            let longList =
                WireEvent.ConversationListSnapshot
                    {|
                        items = JsonArray(summaryItem conversationId longTitle "摘要" false false false)
                        lastCommitId = 2UL
                    |}

            rig.Fake.Deliver longList
            Pump.settle ()
            near rail.Bounds.Width 284.0 "rail after long title"

            let railTitle = findText rail "超长会话标题"
            Assert.Equal(TextTrimming.CharacterEllipsis, railTitle.TextTrimming)
            Assert.True(railTitle.Bounds.Width < 270.0, sprintf "栏内标题宽 %0.1f" railTitle.Bounds.Width)
            Assert.True((absRect railTitle).Right <= (absRect rail).Right + 1.0)

            match findRowButton rail longTitle with
            | Some row -> click row
            | None -> Assert.Fail "长标题行不在栏里"

            rig.Fake.Deliver(
                WireEvent.ConversationSnapshot
                    {|
                        conversationId = Guid.Parse conversationId
                        title = longTitle
                        lastCommitId = 2UL
                        runtimeState = "idle"
                        generationId = None
                        messages =
                            JsonArray(
                                messageItem 1 "user" (textContent userLine),
                                messageItem 2 "assistant" (textContent ("```\\n" + codeLine + "\\n```")),
                                messageItem
                                    3
                                    "assistant"
                                    (sprintf
                                        """[{"type":"functionCall","callId":"call-audit","name":"%s","arguments":{"path":"%s"}}]"""
                                        (String.replicate 14 "long_tool_name_")
                                        (String.replicate 24 "dir/"))
                            )
                        snapshotEarliestCommitId = 0UL
                        snapshotHasMore = false
                        config = SessionConfig.empty
                    |}
            )

            Pump.settle ()
            rig.Shell.Reproject()
            Pump.settle ()
            surfaceReport "light" longTitle
            checkChrome "#1B365D" "#FBFAF7" "#E1DDCC" "#1B365D" longTitle

            let beforeShell = rig.Root.Bounds.Width
            let beforeRail = rail.Bounds.Width
            let beforeStage = composer.Bounds.Width

            let header =
                match buttonWith rig.Root "展开工具调用" with
                | Some h -> h
                | None -> failwith "没有工具头"

            click header
            Pump.settle ()
            near rig.Root.Bounds.Width beforeShell "shell after tool"
            near rail.Bounds.Width beforeRail "rail after tool"
            near composer.Bounds.Width beforeStage "stage after tool"
            let opened = audit "工具展开" rig.Root
            Assert.True(opened.IsEmpty, String.concat "\n" opened)
            click header
            Pump.settle ()

            Assert.True(bottomGap () < 48.0, sprintf "有消息应贴底，底空距 %0.1f" (bottomGap ()))
            near bar.Bounds.Height 52.0 "header with conversation"
            let headerTitle = findText bar "超长会话标题"
            Assert.Equal(TextTrimming.CharacterEllipsis, headerTitle.TextTrimming)
            Assert.True(headerTitle.Bounds.Width < rig.Root.Bounds.Width - rail.Bounds.Width)

            let reading =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Control as c when abs (c.MaxWidth - 748.0) < 0.5 && effectivelyVisible c -> Some c
                    | _ -> None)
                |> Seq.toList

            Assert.NotEmpty reading
            Assert.True(reading |> List.forall (fun c -> c.Bounds.Width <= 748.5))
            measuredReading <- reading |> List.map (fun c -> c.MaxWidth) |> List.max
            assertPads "inset with messages at 1280" 48.0
            assertColumnWithin 48.0

            resize 1100.0 800.0
            assertPads "inset with messages at 1100" 48.0
            assertColumnWithin 48.0

            resize 1280.0 800.0

            let bubbles =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Control as c when abs (c.MaxWidth - 560.0) < 0.5 && effectivelyVisible c -> Some c
                    | _ -> None)
                |> Seq.toList

            Assert.NotEmpty bubbles
            Assert.True(bubbles |> List.forall (fun c -> c.Bounds.Width <= 560.5))

            let codeScrolls =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? ScrollViewer as sv when sv.Classes.Contains "code-scroll" -> Some sv
                    | _ -> None)
                |> Seq.toList

            Assert.NotEmpty codeScrolls
            Assert.True(codeScrolls |> List.exists (fun sv -> sv.Extent.Width > sv.Viewport.Width + 8.0))
            Assert.True(rig.Root.Bounds.Width <= 1280.5)
            near rail.Bounds.Width 284.0 "rail beside code"
            Assert.True(composer.Bounds.Width <= rig.Root.Bounds.Width, sprintf "舞台 %0.1f" composer.Bounds.Width)
            let codeFindings = audit "代码行" rig.Root
            Assert.True(codeFindings.IsEmpty, String.Join("\n", codeFindings))

            rig.Shell.HandleShortcut ShortcutAction.OpenSettings |> ignore
            Pump.settle ()
            Assert.True rig.Views.Workspace.Host.IsVisible

            let surface =
                rig.Root.GetVisualDescendants()
                |> Seq.choose (fun v ->
                    match v with
                    | :? Control as c when c.Name = "settings-surface" -> Some c
                    | _ -> None)
                |> Seq.head

            near surface.Bounds.Width 720.0 "settings"
            measuredSettings <- surface.Bounds.Width
            Assert.Contains("Noto Serif", family (findText rig.Views.Settings.View "设置"))
            Assert.True(rig.Views.Settings.View.IsVisible)

            rig.Shell.HandleShortcut ShortcutAction.OpenCommandPalette |> ignore
            Pump.settle ()
            let dialog = rig.Views.Overlay.Layer.DialogCard
            Assert.True dialog.IsVisible
            near dialog.Bounds.Width 560.0 "palette"
            measuredPalette <- dialog.Bounds.Width
            Assert.True rig.Views.Settings.View.IsVisible

            Assert.True(rig.Shell.HandleShortcut ShortcutAction.Escape)
            Pump.settle ()
            Assert.False dialog.IsVisible
            Assert.True rig.Views.Settings.View.IsVisible

            Assert.True(rig.Shell.HandleShortcut ShortcutAction.Escape)
            Pump.settle ()
            Assert.False rig.Views.Settings.View.IsVisible

            rig.Shell.HandleShortcut ShortcutAction.OpenCommandPalette |> ignore
            Pump.settle ()
            Assert.True dialog.IsVisible
            rig.Shell.HandleShortcut ShortcutAction.OpenCommandPalette |> ignore
            Pump.settle ()
            Assert.False dialog.IsVisible

            rig.Views.Dialogs.Confirm("删除对话", "这段会删掉。", "删除", ignore) |> ignore
            Pump.settle ()
            near dialog.Bounds.Width 420.0 "confirm"
            measuredConfirm <- dialog.Bounds.Width
            rig.Shell.HandleShortcut ShortcutAction.Escape |> ignore
            Pump.settle ()

            resize 360.0 700.0
            rig.Views.Dialogs.Confirm("删除对话", "窄窗。", "删除", ignore) |> ignore
            Pump.settle ()

            Assert.True(
                dialog.Bounds.Width <= rig.Root.Bounds.Width - 24.0 + 0.5,
                sprintf "窄确认框 %0.1f 窗 %0.1f" dialog.Bounds.Width rig.Root.Bounds.Width
            )

            rig.Shell.HandleShortcut ShortcutAction.Escape |> ignore
            Pump.settle ()

            resize 500.0 800.0
            Assert.False(effectivelyVisible rail, "窄屏侧栏不是第二列")

            Assert.True(
                composer.Bounds.Width > rig.Root.Bounds.Width - 24.0,
                sprintf "舞台宽 %0.1f" composer.Bounds.Width
            )

            near composer.Padding.Left 24.0 "inset at 500"
            let collapsedBefore = rig.Shell.Model.Nav.SidebarCollapsed
            Assert.True(rig.Shell.HandleShortcut ShortcutAction.ToggleSidebar)
            Pump.settle ()
            Assert.Equal(collapsedBefore, rig.Shell.Model.Nav.SidebarCollapsed)
            Assert.True(effectivelyVisible rail, "抽屉打开")
            Assert.True(composer.Bounds.Width > rig.Root.Bounds.Width - 24.0)
            Assert.True(rig.Shell.HandleShortcut ShortcutAction.Escape)
            Pump.settle ()
            Assert.False(effectivelyVisible rail, "Escape 关掉抽屉")

            resize 400.0 800.0
            near composer.Padding.Left 16.0 "inset at 400"
            measuredInset16 <- composer.Padding.Left
            Assert.True(composer.Bounds.Width > rig.Root.Bounds.Width - 8.0)

            resize 1280.0 800.0
            rig.Theme.Motion.ApplyReduced true
            rig.Theme.Apply PaletteMode.Dark
            Pump.settle ()
            surfaceReport "dark" longTitle
            checkChrome "#2F4864" "#E7EEF7" "#34343E" "#8FB4DE" longTitle
            Assert.Equal("#15151A", bg rig.Views.Workspace.Host)
            Assert.Equal("#1A1A22", bg rail)
            Assert.Equal("#8FB4DE", bg rig.Views.Composer.Box.SendButton)

            Assert.True(rig.Shell.HandleShortcut ShortcutAction.NewConversation)

            match rig.Shell.Model.Conn with
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.IdleEmpty)) -> ()
            | other -> Assert.Fail(sprintf "Ctrl+N 之后应是空对话：%A" other)

            let measuredRail = rail.Bounds.Width
            let measuredBar = bar.Bounds.Height

            // DPR 把 1100 CSS 除成约 733 的安排宽度。留白和侧栏必须跟 CSS，不能跟这 733。
            resize 733.0 800.0
            near rig.Root.Bounds.Width 733.0 "arranged bounds"
            assertPads "bounds 733" 32.0
            near rail.Bounds.Width 284.0 "rail at bounds 733"
            Assert.False rig.Views.Workspace.State.compactMode

            rig.Fake.CssViewport <- 1100.0
            rig.Fake.FireEvery(TimeSpan.FromMilliseconds 250.0)
            Pump.settle ()
            near rig.Root.Bounds.Width 733.0 "bounds stay ~733 under css 1100"
            assertPads "css 1100" 48.0
            near rail.Bounds.Width 284.0 "rail column at css 1100"
            Assert.True(effectivelyVisible rail, "CSS 1100 仍是侧栏列")
            Assert.False(rig.Views.Workspace.State.compactMode, "CSS 1100 不是抽屉")

            rig.Fake.CssViewport <- 600.0
            rig.Fake.FireEvery(TimeSpan.FromMilliseconds 250.0)
            Pump.settle ()
            near rig.Root.Bounds.Width 733.0 "bounds stay ~733 under css 600"
            Assert.True(rig.Views.Workspace.State.compactMode, "CSS 600 进入抽屉")
            Assert.False(effectivelyVisible rail, "CSS 600 侧栏不是第二列")
            near composer.Padding.Left 24.0 "css 600 inset"
            near rig.Views.Chat.Pane.Scroller.Viewer.Padding.Left 24.0 "css 600 thread"

            Assert.True(
                composer.Bounds.Width > rig.Root.Bounds.Width - 24.0,
                sprintf "抽屉舞台宽 %0.1f" composer.Bounds.Width
            )

            let measured =
                sprintf
                    "MEASURED rail=%0.0f bar=%0.0f empty=%0.0f reading=%0.0f inset16=%0.0f settings=%0.0f palette=%0.0f confirm=%0.0f"
                    measuredRail
                    measuredBar
                    measuredEmpty
                    measuredReading
                    measuredInset16
                    measuredSettings
                    measuredPalette
                    measuredConfirm

            // printfn 进进程输出；ITestOutputHelper 进宿主的测试输出。dotnet test 成功时默认不转发后者。
            printfn "%s" measured
            TestContext.Current.TestOutputHelper.WriteLine measured
        finally
            window.Close())
