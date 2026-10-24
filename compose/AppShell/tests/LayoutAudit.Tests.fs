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
open System.Text.Json.Nodes
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.AppShell.Tests

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
        | :? ScrollViewer as sv when sv.Extent.Width > sv.Viewport.Width + 0.5 ->
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
