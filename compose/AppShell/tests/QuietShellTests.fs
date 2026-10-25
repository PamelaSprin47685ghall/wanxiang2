module Wanxiang2.AppShell.Tests.QuietShellTests

// 活树上的安静壳：就绪空态只有三块；打开一轮后不再叠卡片。按钮仍走真实控件。
open System
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
open Wanxiang.UI
open Wanxiang2.AppShell.Tests
open Wanxiang2.ThemePalette
open Wanxiang2.WorkspaceState

let private convId = "11111111-1111-1111-1111-111111111111"

let private shown (c: Control) =
    let rec up (v: Visual) =
        if isNull v then
            true
        else
            match v with
            | :? Control as ctrl when not ctrl.IsVisible -> false
            | _ -> up (v.GetVisualParent())

    up c

let private thick (t: Thickness) =
    t.Left > 0.01 || t.Top > 0.01 || t.Right > 0.01 || t.Bottom > 0.01

let private nameOf (c: Control) =
    let n = AutomationProperties.GetName c
    if String.IsNullOrWhiteSpace n then "" else n

let private solid (brush: IBrush) =
    match brush with
    | :? ISolidColorBrush as s -> Some s.Color
    | _ -> None

let private hex (brush: IBrush) =
    match solid brush with
    | Some c -> sprintf "#%02X%02X%02X" c.R c.G c.B
    | None -> if isNull brush then "" else brush.GetType().Name

let private paint (c: Control) =
    match c with
    | :? Border as b -> b.Background
    | :? Panel as p -> p.Background
    | _ -> null

let private clearBrush (brush: IBrush) =
    match solid brush with
    | None -> isNull brush
    | Some c -> c.A = 0uy

let private absRect (c: Control) =
    let mutable x = c.Bounds.X
    let mutable y = c.Bounds.Y
    let mutable cur = c.GetVisualParent()

    while not (isNull cur) do
        x <- x + cur.Bounds.X
        y <- y + cur.Bounds.Y
        cur <- cur.GetVisualParent()

    Rect(Point(x, y), c.Bounds.Size)

let private family (tb: TextBlock) =
    if isNull tb.FontFamily then
        ""
    else
        tb.FontFamily.ToString()

let private catalogProviders =
    let provider = JsonObject()
    provider["id"] <- JsonValue.Create "p1"
    provider["label"] <- JsonValue.Create "P1"
    provider["models"] <- JsonArray([| JsonValue.Create "m1" :> JsonNode |])
    JsonArray([| provider :> JsonNode |])

let private connect (rig: Rig) =
    rig.Fake.Deliver(AuthAccepted {| instanceId = "quiet-inst" |})

    rig.Fake.Deliver(
        CatalogSnapshot
            {|
                providers = catalogProviders
                tools = JsonArray()
                generation = JsonObject()
            |}
    )

    Pump.settle ()

let private blockText (tb: TextBlock) =
    let own = if isNull tb.Text then "" else tb.Text

    let runs =
        if isNull tb.Inlines then
            ""
        else
            tb.Inlines
            |> Seq.choose (fun i ->
                match i with
                | :? Run as r -> Some r.Text
                | _ -> None)
            |> String.concat ""

    own + runs

let private findText (root: Control) (needle: string) =
    root.GetVisualDescendants()
    |> Seq.tryPick (fun v ->
        match v with
        | :? TextBlock as tb when shown tb && (blockText tb).Contains needle -> Some tb
        | _ -> None)

let private buttons (root: Control) =
    root.GetVisualDescendants()
    |> Seq.choose (fun v ->
        match v with
        | :? Button as b when shown b -> Some b
        | _ -> None)

let private invoke (b: Button) =
    if not b.IsEnabled then
        failwith (sprintf "「%s」不可用" (nameOf b))

    let peer = ControlAutomationPeer.CreatePeerForElement b
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()
    Pump.settle ()

let private blob (c: Control) =
    let rec walk (v: Control) =
        let own =
            match v with
            | :? TextBlock as tb ->
                let runs =
                    if isNull tb.Inlines then
                        ""
                    else
                        tb.Inlines
                        |> Seq.choose (fun i ->
                            match i with
                            | :? Run as r -> Some r.Text
                            | _ -> None)
                        |> String.concat ""

                (if isNull tb.Text then "" else tb.Text) + runs
            | :? Button as b -> nameOf b
            | _ -> ""

        let kids =
            v.GetVisualChildren()
            |> Seq.choose (fun x ->
                match x with
                | :? Control as child -> Some(walk child)
                | _ -> None)
            |> String.concat ""

        own + kids

    walk c

let private openQuiet (rig: Rig) =
    rig.Fake.Deliver(
        WireEvent.ConversationListSnapshot
            {|
                items =
                    JsonArray(
                        [|
                            JsonNode.Parse(
                                sprintf
                                    """{"conversationId":"%s","title":"安静探查","lastMessage":"摘要","pinned":false,"archived":false,"runtimeState":"idle","createdAt":"2026-10-20T08:00:00Z","updatedAt":"2026-10-21T08:00:00Z","messageCount":2,"isFork":false,"lastCommitId":2,"config":{"provider":"p1","model":"m1"}}"""
                                    convId
                            )
                        |]
                    )
                lastCommitId = 2UL
            |}
    )

    Pump.settle ()

    match buttons rig.Root |> Seq.tryFind (fun b -> (nameOf b).Contains "安静探查") with
    | Some row -> invoke row
    | None -> failwith "没有对话行"

    rig.Fake.Deliver(
        WireEvent.ConversationSnapshot
            {|
                conversationId = Guid.Parse convId
                title = "安静探查"
                lastCommitId = 2UL
                runtimeState = "idle"
                generationId = None
                messages =
                    JsonArray(
                        [|
                            JsonNode.Parse
                                """{"commitId":1,"committedAt":"2026-10-21T08:00:00Z","payload":{"role":"user","contents":[{"type":"text","text":"用户原文甲"}]}}"""
                            JsonNode.Parse
                                """{"commitId":2,"committedAt":"2026-10-21T08:00:00Z","payload":{"role":"assistant","contents":[{"type":"reasoning","text":"先想清楚"},{"type":"text","text":"助手正文乙"},{"type":"functionCall","callId":"c1","name":"lookup","arguments":{"q":"x"}}]}}"""
                        |]
                    )
                snapshotEarliestCommitId = 0UL
                snapshotHasMore = false
                config = SessionConfig.empty
            |}
    )

    Pump.settle ()

let private messageOf (rig: Rig) (needle: string) =
    rig.Views.Chat.Pane.Messages.Children
    |> Seq.cast<Control>
    |> Seq.tryFind (fun c -> (blob c).Contains needle)

let private thickBits (root: Control) =
    root.GetVisualDescendants()
    |> Seq.choose (fun v ->
        match v with
        | :? Border as b when shown b && thick b.BorderThickness ->
            Some(sprintf "边框「%s」%A" (nameOf b) b.BorderThickness)
        | :? Button as b when shown b && thick b.BorderThickness ->
            Some(sprintf "按钮「%s」%A" (nameOf b) b.BorderThickness)
        | _ -> None)
    |> Seq.toList

let private shellOf (composer: Control) =
    composer.GetVisualDescendants()
    |> Seq.tryPick (fun v ->
        match v with
        | :? Border as b when shown b && abs (b.CornerRadius.TopLeft - 16.0) < 0.1 && thick b.BorderThickness -> Some b
        | _ -> None)

let private inside (outer: Control) (inner: Control) =
    let o = absRect outer
    let i = absRect inner

    i.X >= o.X - 1.0
    && i.Right <= o.Right + 1.0
    && i.Y >= o.Y - 1.0
    && i.Bottom <= o.Bottom + 1.0

let private ready (rig: Rig) =
    connect rig
    rig.Shell.Dispatch(User(Composer(ComposerUserMsg.ModelPick("p1", "m1"))))
    Pump.settle ()

let private assertEmpty (rig: Rig) (canvas: string) =
    Assert.Equal(canvas, hex (paint rig.Views.Workspace.Host))
    let bar = rig.Views.Chat.Header.Host
    Assert.True(abs (bar.Bounds.Height - 52.0) <= 1.0, sprintf "顶栏高 %0.1f" bar.Bounds.Height)
    Assert.True(buttons bar |> Seq.exists (fun b -> (nameOf b).Contains "切换侧边栏"))

    match findText bar "新对话" with
    | Some title -> Assert.Contains("Noto Serif", family title)
    | None -> Assert.Fail "顶栏没有衬线标题"

    let forbidden = [ "重命名"; "置顶"; "归档"; "导出"; "删除" ]

    let row =
        buttons bar
        |> Seq.filter (fun b ->
            let n = nameOf b

            let content =
                match b.Content with
                | :? string as s -> s
                | _ -> ""

            forbidden |> List.exists (fun word -> n = word || content = word))
        |> Seq.map nameOf
        |> Seq.toList

    Assert.True(List.isEmpty row, sprintf "顶栏还有一排操作：%s" (String.concat "," row))

    match findText rig.Root "从一句开始" with
    | None -> Assert.Fail "没有「从一句开始」"
    | Some lead ->
        Assert.Equal(25.0, lead.FontSize)
        Assert.Contains("Noto Serif", family lead)

    let hint = "模型、附件和发送都在下面。要找回以前的对话，用侧栏。"

    let hints =
        rig.Root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? TextBlock as tb when shown tb && tb.Text = hint -> Some tb
            | _ -> None)
        |> Seq.length

    Assert.Equal(1, hints)

    let starters = [ "用三句话说明这台主机现在能做什么"; "指出一份 TOML 配置里的错误"; "把工具调用说成人话" ]

    let starterButtons =
        starters
        |> List.map (fun label ->
            match buttons rig.Root |> Seq.tryFind (fun b -> nameOf b = label) with
            | Some b -> b
            | None -> failwith ("没有起手式 " + label))

    Assert.Equal(3, starterButtons.Length)

    for b in starterButtons do
        Assert.True(b.MinHeight >= 44.0, sprintf "%s 高 %0.1f" (nameOf b) b.MinHeight)
        Assert.Equal(8.0, b.CornerRadius.TopLeft)
        Assert.True(thick b.BorderThickness, nameOf b)

    let composer = rig.Views.Composer.Box.Host

    match shellOf composer with
    | None -> Assert.Fail "输入区不是一条圆角 16 的边"
    | Some shell ->
        Assert.Equal(1.0, shell.BorderThickness.Left)
        Assert.Equal(Thickness 0.0, rig.Views.Composer.Box.Input.BorderThickness)
        Assert.Equal("写下一句", rig.Views.Composer.Box.Input.PlaceholderText)
        Assert.True(inside shell rig.Views.Composer.Box.Input, "输入框不在这一条里")
        Assert.True(inside shell rig.Views.Composer.Box.AttachButton, "添加附件不在这一条里")
        Assert.True(inside shell rig.Views.Composer.Box.SendButton, "发送不在这一条里")

        match buttons composer |> Seq.tryFind (fun b -> (nameOf b).Contains "模型") with
        | Some model -> Assert.True(inside shell model, "模型不在这一条里")
        | None -> Assert.Fail "没有模型控件"

        let extra =
            composer.GetVisualDescendants()
            |> Seq.choose (fun v ->
                match v with
                | :? Border as b when shown b && thick b.BorderThickness && not (obj.ReferenceEquals(b, shell)) ->
                    Some(sprintf "「%s」%A" (nameOf b) b.BorderThickness)
                | _ -> None)
            |> Seq.toList

        Assert.True(List.isEmpty extra, sprintf "输入区还有第二层边：%s" (String.concat "," extra))

    let cards =
        rig.Root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Control as c when shown c && c.Classes.Contains "grouping-card" -> Some(c.GetType().Name)
            | _ -> None)
        |> Seq.toList

    Assert.True(List.isEmpty cards, sprintf "还有分组卡：%s" (String.concat "," cards))

    let emptyBlock =
        let lead = findText rig.Root "从一句开始" |> Option.get

        let rec up (v: Visual) =
            match v with
            | :? Control as lay when abs (lay.MaxWidth - 480.0) < 0.5 -> lay
            | _ -> up (v.GetVisualParent())

        up lead

    let composerHost = rig.Views.Composer.Host
    let emptyMid = (absRect emptyBlock).X + (absRect emptyBlock).Width / 2.0
    let composerMid = (absRect composerHost).X + (absRect composerHost).Width / 2.0
    Assert.True(abs (emptyMid - composerMid) <= 4.0, sprintf "中线 %0.1f / %0.1f" emptyMid composerMid)

    let rail = rig.Views.Sidebar.View
    Assert.True(abs (rail.Bounds.Width - 284.0) <= 1.5, sprintf "栏宽 %0.1f" rail.Bounds.Width)
    Assert.False(rig.Views.Sidebar.Batch.Header.IsVisible)
    Assert.False(rig.Views.Sidebar.Batch.ActionBar.IsVisible)
    Assert.True(findText rail "万" |> Option.isSome)
    Assert.True(buttons rail |> Seq.exists (fun b -> (nameOf b).Contains "设置"))

[<Fact>]
let ``安静壳：就绪空态只有引导、三行起手式和一条输入`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            ready rig
            assertEmpty rig "#F5F4ED"
            rig.Theme.Motion.ApplyReduced true
            rig.Theme.Apply PaletteMode.Dark
            Pump.settle ()
            assertEmpty rig "#15151A"
        finally
            window.Close())

let private fullThickness (t: Thickness) =
    t.Left > 0.01 && t.Top > 0.01 && t.Right > 0.01 && t.Bottom > 0.01

/// 打开一轮后：没有分组卡。整圈边只剩侧栏搜索、一条输入、一块工具卡。
/// 思考、工具活动和消息动作的按钮边厚为 0。
let private assertNoStack (rig: Rig) =
    let groups =
        rig.Root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Control as c when shown c && c.Classes.Contains "grouping-card" ->
                Some(sprintf "%s「%s」" (c.GetType().Name) (nameOf c))
            | _ -> None)
        |> Seq.toList

    Assert.True(List.isEmpty groups, sprintf "打开一轮后还有分组卡：%s" (String.concat "," groups))

    let describe (c: Control) =
        let kind = if c :? Button then "按钮" else "边框"

        let radius =
            match c with
            | :? Border as b -> b.CornerRadius.TopLeft
            | :? Button as b -> b.CornerRadius.TopLeft
            | _ -> 0.0

        sprintf "%s「%s」r=%0.0f %s" kind (nameOf c) radius (c.GetType().Name)

    let fullBorders =
        rig.Root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Border as b when shown b && fullThickness b.BorderThickness -> Some(b :> Control)
            | :? Button as b when shown b && fullThickness b.BorderThickness -> Some(b :> Control)
            | _ -> None)
        |> Seq.toList

    let holds (needle: string) (c: Control) =
        c.GetVisualDescendants()
        |> Seq.exists (fun v ->
            match v with
            | :? Control as child when (nameOf child).Contains needle -> true
            | _ -> false)
        || (nameOf c).Contains needle

    let search, rest =
        fullBorders
        |> List.partition (fun c ->
            holds "搜索标题和正文" c
            && (match c with
                | :? Border as b -> abs (b.CornerRadius.TopLeft - 8.0) < 0.1
                | _ -> false))

    let composer, rest =
        rest
        |> List.partition (fun c ->
            match c with
            | :? Border as b -> abs (b.CornerRadius.TopLeft - 16.0) < 0.1
            | _ -> false)

    let tools, extra = rest |> List.partition (fun c -> (nameOf c).Contains "工具调用")

    Assert.Equal(1, search.Length)
    Assert.Equal(1, composer.Length)
    Assert.Equal(1, tools.Length)
    Assert.True(List.isEmpty extra, sprintf "打开一轮后还有整圈边：%s" (extra |> List.map describe |> String.concat " | "))

    for label in [ "思考过程"; "工具活动"; "工具调用"; "复制"; "编辑并分叉"; "重新生成"; "删除这条消息" ] do
        buttons (rig.Views.Chat.Pane.Messages :> Control)
        |> Seq.filter (fun b -> (nameOf b).Contains label)
        |> Seq.iter (fun b ->
            Assert.True(
                not (fullThickness b.BorderThickness) && b.BorderThickness = Thickness 0.0,
                sprintf "%s 还有边 %A" (nameOf b) b.BorderThickness
            ))

let private assertTurn (rig: Rig) =
    let bar = rig.Views.Chat.Header.Host
    Assert.True(abs (bar.Bounds.Height - 52.0) <= 1.0, sprintf "顶栏高 %0.1f" bar.Bounds.Height)
    Assert.Equal(24.0, rig.Views.Chat.Pane.Messages.Spacing)

    let kids = rig.Views.Chat.Pane.Messages.Children |> Seq.cast<Control> |> Seq.toList
    Assert.True(kids.Length >= 2, "消息没有排成一列")
    let gap = kids.[1].Bounds.Y - (kids.[0].Bounds.Y + kids.[0].Bounds.Height)
    Assert.True(abs (gap - 24.0) <= 1.0, sprintf "消息间距 %0.1f" gap)

    match
        rig.Root.GetVisualDescendants()
        |> Seq.tryPick (fun v ->
            match v with
            | :? Border as b when nameOf b = "用户消息" && shown b -> Some b
            | _ -> None)
    with
    | None -> Assert.Fail "没有用户气泡"
    | Some bubble ->
        Assert.Equal(Thickness 0.0, bubble.BorderThickness)
        Assert.Equal(12.0, bubble.CornerRadius.TopLeft)
        Assert.Equal(12.0, bubble.CornerRadius.TopRight)
        Assert.Equal(6.0, bubble.CornerRadius.BottomRight)
        Assert.Equal(12.0, bubble.CornerRadius.BottomLeft)
        Assert.True(bubble.Bounds.Width <= 560.0, sprintf "气泡宽 %0.1f" bubble.Bounds.Width)
        Assert.Equal(HorizontalAlignment.Right, bubble.HorizontalAlignment)

        match messageOf rig "用户原文甲" with
        | None -> Assert.Fail "用户消息不在列里"
        | Some host ->
            Assert.True(List.isEmpty (thickBits host), sprintf "用户消息外面还有边：%s" (String.concat "," (thickBits host)))

    match messageOf rig "助手正文乙" with
    | None -> Assert.Fail "助手正文不在列里"
    | Some host ->
        match findText host "助手正文乙" with
        | None -> Assert.Fail "助手正文没有落到文字上"
        | Some prose ->
            let rec firstBorder (v: Visual) =
                match v with
                | :? Border as b -> b
                | _ -> firstBorder (v.GetVisualParent())

            let body = firstBorder prose
            Assert.Equal(Thickness 0.0, body.BorderThickness)

            Assert.True(
                clearBrush body.Background,
                sprintf "助手正文自己有底 %s %s %A" (body.GetType().Name) (hex body.Background) body.Classes
            )

        let bits = thickBits host
        Assert.Equal(1, bits.Length)
        Assert.Contains("工具调用", bits.[0])

    let column = rig.Views.Chat.Pane.Messages :> Control

    for label in [ "复制用户消息"; "复制助手消息"; "编辑并分叉"; "重新生成"; "删除这条消息" ] do
        match buttons column |> Seq.tryFind (fun b -> (nameOf b).StartsWith label) with
        | None -> Assert.Fail("没有 " + label)
        | Some b ->
            Assert.Equal(Thickness 0.0, b.BorderThickness)

            let rec filled (v: Visual) =
                if isNull v || obj.ReferenceEquals(v, column) then
                    false
                else
                    match v with
                    | :? Border as border when not (clearBrush border.Background) && thick border.BorderThickness ->
                        true
                    | _ -> filled (v.GetVisualParent())

            Assert.False(filled (b.GetVisualParent()), label + " 坐在一块有底的条上")

    assertNoStack rig

    let rail = rig.Views.Sidebar.View
    Assert.True(abs (rail.Bounds.Width - 284.0) <= 1.5, sprintf "栏宽 %0.1f" rail.Bounds.Width)
    Assert.False(rig.Views.Sidebar.Batch.Header.IsVisible, "没多选时批量头还在")
    Assert.False(rig.Views.Sidebar.Batch.ActionBar.IsVisible, "没多选时批量条还在")

[<Fact>]
let ``安静壳：打开一轮后气泡、正文和工具不再叠卡片`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        let window = Window(Width = 1280.0, Height = 800.0, Content = rig.Root)
        window.Show()
        Pump.settle ()

        try
            ready rig
            openQuiet rig
            assertTurn rig
            rig.Theme.Motion.ApplyReduced true
            rig.Theme.Apply PaletteMode.Dark
            Pump.settle ()
            Assert.Equal("#15151A", hex (paint rig.Views.Workspace.Host))
            assertTurn rig

            let beforeShell = rig.Root.Bounds.Width
            let beforeRail = rig.Views.Sidebar.View.Bounds.Width
            let beforeStage = rig.Views.Composer.Host.Bounds.Width

            match buttons rig.Root |> Seq.tryFind (fun b -> (nameOf b).StartsWith "展开工具调用") with
            | None -> Assert.Fail "没有展开工具调用"
            | Some tool ->
                invoke tool
                Assert.StartsWith("收起工具调用", nameOf tool)

            match messageOf rig "助手正文乙" with
            | None -> Assert.Fail "展开后助手消息丢了"
            | Some host ->
                let bits = thickBits host
                Assert.Equal(1, bits.Length)
                Assert.Contains("工具调用", bits.[0])

            assertNoStack rig

            Assert.Equal(beforeShell, rig.Root.Bounds.Width)
            Assert.Equal(beforeRail, rig.Views.Sidebar.View.Bounds.Width)
            Assert.Equal(beforeStage, rig.Views.Composer.Host.Bounds.Width)

            let genId = Guid.NewGuid()

            rig.Fake.Deliver(
                WireEvent.GenerationStarted
                    {|
                        conversationId = Guid.Parse convId
                        generationId = genId
                        providerId = "p1"
                        model = "m1"
                    |}
            )

            Pump.settle ()
            let bar = rig.Views.Chat.Header.Host
            let chip = rig.Views.Chat.Header.GeneratingChip
            Assert.True(abs (bar.Bounds.Height - 52.0) <= 1.0, sprintf "生成时顶栏高 %0.1f" bar.Bounds.Height)
            Assert.Equal(Thickness 0.0, chip.BorderThickness)
            Assert.True(clearBrush chip.Background, "生成状态还是一块有底的胶囊")
            Assert.Equal(0.0, chip.CornerRadius.TopLeft)
            Assert.True(chip.Opacity > 0.5, "生成状态没有写在顶栏上")
            Assert.True(bar.GetVisualDescendants() |> Seq.exists (fun v -> obj.ReferenceEquals(v, chip)))
            Assert.True(findText bar "生成" |> Option.isSome, "顶栏上没有生成这几个字")
        finally
            window.Close())
