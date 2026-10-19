module Wanxiang2.MessageCardView.Tests.BehaviorTests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.MessageCardView
open Wanxiang2.MessageCardView.Tests

let private sample name =
    let _, m, f = Samples.all |> List.find (fun (n, _, _) -> n = name)
    m, f

let private all (root: Control) =
    let rec walk (c: ILogical) =
        seq {
            match c with
            | :? Control as cc -> yield cc
            | _ -> ()
            for child in c.LogicalChildren do yield! walk child
        }
    walk root |> List.ofSeq

let private named (name: string) (root: Control) =
    all root |> List.find (fun c -> AutomationProperties.GetName c = name)

let private show (c: Control) =
    let w = Window(Width = 600.0, Height = 1200.0, Content = c)
    w.Show()
    w.UpdateLayout()
    w

let private press (c: Control) =
    c.RaiseEvent(Input.KeyEventArgs(RoutedEvent = Input.InputElement.KeyDownEvent, Key = Input.Key.Enter))

let private render name =
    let cards, manager = Fixture.cards ()
    let m, f = sample name
    let card, stream = cards.Render(m, Fixture.ctxOf f, Fixture.actions, f.Index)
    cards, manager, card, stream

[<Fact>]
let ``MessageCard toolCallCard header toggles argument and result details`` () =
    Headless.run (fun () ->
        let _, _, card, _ = render "assistant-tools"
        let w = show card
        let header = named "展开工具调用 builtin_echo" card
        press header
        Assert.Equal("收起工具调用 builtin_echo", AutomationProperties.GetName header)
        Assert.Contains(all card, fun c -> match c with :? TextBlock as t -> t.Text = "调用参数" && t.IsEffectivelyVisible | _ -> false)
        w.Close())

[<Fact>]
let ``工具概览在等待批准时用警示色，不显示「完成」对勾配色`` () =
    Headless.run (fun () ->
        let _, manager, card, _ = render "assistant-tools-awaiting"
        let runs =
            all card
            |> List.collect (fun c -> match c with :? TextBlock as t when not (isNull t.Inlines) -> List.ofSeq t.Inlines | _ -> [])
            |> List.choose (function :? Documents.Run as r -> Some r | _ -> None)
        let overview = runs |> List.find (fun r -> r.Text.Contains "等待")
        Assert.Same(manager.Brush Wanxiang2.ThemePalette.PaletteKey.Warning, overview.Foreground)
        Assert.Contains(all card, fun c -> match c with :? TextBlock as t -> t.Text = "等待批准" | _ -> false))

[<Fact>]
let ``230 错误卡有半截回复时先给「继续生成」，没有时不给`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let _, err, _ = Samples.errors |> List.find (fun (n, _, _) -> n = "error-continue")
        let withContinue = cards.ErrorCard(err, ignore, Fixture.actions, Some ignore)
        let without = cards.ErrorCard(err, ignore, Fixture.actions, None)
        Assert.Contains(all withContinue, fun c -> AutomationProperties.GetName c = "继续生成")
        Assert.DoesNotContain(all without, fun c -> AutomationProperties.GetName c = "继续生成"))

[<Fact>]
let ``§215 密钥失效的错误卡给出「打开模型服务设置」并路由到 sink`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let _, err, _ = Samples.errors |> List.find (fun (n, _, _) -> n = "error-auth")
        let card = cards.ErrorCard(err, ignore, Fixture.actions, None)
        let label = ErrorRecovery.label ErrorRecovery.OpenProviderSettings
        Fixture.log.Clear()
        press (named label card)
        Assert.Contains("recover:OpenProviderSettings", Fixture.log)
        // 不可重试：没有重试按钮
        Assert.DoesNotContain(all card, fun c -> AutomationProperties.GetName c = "重试生成"))

[<Fact>]
let ``262 限流倒计时未结束时重试按钮不可按并显示剩余秒数，走完才可按`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let err = { GenerationError.create GenerationErrorKind.ProviderRateLimited "限流" with retryAfterSeconds = Some 5 }
        let card = cards.ErrorCard(err, ignore, Fixture.actions, None)
        let retry = named "重试生成，5 秒后可用" card
        Assert.False retry.IsEnabled
        // 时间走过截止时刻；重新挂上时刷新
        let w = show (Border())
        Fixture.now <- Fixture.now.AddSeconds 6.0
        (w.Content :?> Border).Child <- card
        Assert.True retry.IsEnabled
        Assert.Equal("重试生成", AutomationProperties.GetName retry)
        Fixture.now <- Fixture.now.AddSeconds -6.0
        w.Close())

[<Fact>]
let ``262 没有倒计时的可重试错误照常可按`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let _, err, _ = Samples.errors |> List.find (fun (n, _, _) -> n = "error-retry")
        let mutable hits = 0
        let card = cards.ErrorCard(err, (fun () -> hits <- hits + 1), Fixture.actions, None)
        let retry = named "重试生成" card
        Assert.True retry.IsEnabled
        press retry
        Assert.Equal(1, hits))

[<Fact>]
let ``error card toggles technical detail and triggers retry action`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let _, err, _ = Samples.errors |> List.find (fun (n, _, _) -> n = "error-retry")
        let card = cards.ErrorCard(err, ignore, Fixture.actions, None)
        let fold = named "技术细节" card
        press fold
        Assert.Equal("收起技术细节", AutomationProperties.GetName fold))

[<Fact>]
let ``r8 右键消息正文弹消息菜单（含复制消息链接）`` () =
    Headless.run (fun () ->
        let _, _, card, _ = render "assistant-plain"
        let menu = card.ContextMenu
        let labels = menu.ItemsSource |> Seq.cast<MenuItem> |> Seq.map (fun i -> string i.Header) |> List.ofSeq
        Assert.Contains(MessageMenu.label (MessageMenuItem.CopyLink 7UL), labels)
        Fixture.log.Clear()
        let link = menu.ItemsSource |> Seq.cast<MenuItem> |> Seq.find (fun i -> string i.Header = MessageMenu.label (MessageMenuItem.CopyLink 7UL))
        link.RaiseEvent(Interactivity.RoutedEventArgs(MenuItem.ClickEvent))
        Assert.Contains("link:7", Fixture.log)
        // 正文不挂主题自带的英文 Copy 浮层
        Assert.All(all card |> List.choose (function :? SelectableTextBlock as s -> Some s | _ -> None), fun s -> Assert.Null s.ContextFlyout))

[<Fact>]
let ``生成中的消息没有菜单项也没有操作按钮`` () =
    Headless.run (fun () ->
        let _, _, card, stream = render "assistant-streaming"
        Assert.Empty(card.ContextMenu.ItemsSource |> Seq.cast<obj>)
        Assert.True stream.IsSome
        stream.Value.Update "正在写更多"
        Assert.DoesNotContain(all card, fun c -> AutomationProperties.GetName c = "重新生成"))

[<Fact>]
let ``复制按钮走宿主的复制入口并换成已复制`` () =
    Headless.run (fun () ->
        let _, _, card, _ = render "assistant-plain"
        Fixture.log.Clear()
        let copy = named "复制助手消息 1" card
        press copy
        Assert.Contains(Fixture.log, fun s -> s.StartsWith "copy:第一段")
        Assert.Equal("已复制", AutomationProperties.GetName copy))

[<Fact>]
let ``streaming completion keeps the message footnote row geometry constant`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let m, f = sample "assistant-last-usage"
        let measure (flags: Samples.Flags) =
            let card, _ = cards.Render({ m with commitId = (if flags.Streaming then None else m.commitId) }, Fixture.ctxOf flags, Fixture.actions, f.Index)
            let w = show card
            let strip = ((card :?> Border).Child :?> StackPanel).Children[1] :?> StackPanel
            let h = strip.Children[0].Bounds.Height
            w.Close()
            h
        Assert.Equal(measure { f with Streaming = true; Usage = None }, measure f))

[<Fact>]
let ``rebuilt streaming caret continues the breath run instead of restarting at peak`` () =
    Headless.run (fun () ->
        let manager, styles = Fixture.themed ()
        let mutable t = 0L
        let clock = CaretClock((fun () -> t), 1000L)
        let cards = MessageCards(CardLooks.ofTheme manager styles, { Fixture.host () with Clock = clock })
        let m, f = sample "assistant-streaming"
        let first, _ = cards.Render(m, Fixture.ctxOf f, Fixture.actions, None)
        let w = show first
        let origin = clock.Origin
        Assert.True clock.IsLive
        t <- 700L
        let second, _ = cards.Render(m, Fixture.ctxOf f, Fixture.actions, None)
        w.Content <- second
        Assert.Equal(origin, clock.Origin)
        Assert.Equal(TimeSpan.FromMilliseconds 700.0, clock.Elapsed)
        w.Close())

[<Fact>]
let ``卡片堆按身份复用，流式卡每次重建`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let deck = CardDeck(StackPanel())
        let m, f = sample "assistant-plain"
        let s, sf = sample "assistant-streaming"
        let mutable built = 0
        let entry (msg: MessageView) (flags: Samples.Flags) =
            let ctx = Fixture.ctxOf flags
            CardDeck.key msg ctx, (fun () -> built <- built + 1; fst (cards.Render(msg, ctx, Fixture.actions, None)))
        deck.Show [ entry m f; entry s sf ] |> ignore
        let kept = deck.Panel.Children[0]
        deck.Show [ entry m f; entry s sf ] |> ignore
        Assert.Same(kept, deck.Panel.Children[0])
        Assert.Equal(3, built)
        Assert.Equal(2, deck.Panel.Children.Count))

[<Fact>]
let ``外观来自主题管理者的活值`` () =
    Headless.run (fun () ->
        let _, manager, card, _ = render "assistant-plain"
        let w = show card
        manager.SetSpace(Space.RadiusLg, 20.0)
        Assert.Equal(CornerRadius 20.0, (card :?> Border).CornerRadius)
        manager.SetSpace(Space.RadiusLg, 12.0)
        w.Close())

[<Fact>]
let ``卡上每个可点件都有读屏 Invoke 并报按钮`` () =
    Headless.run (fun () ->
        for name in [ "user-attach"; "assistant-tools"; "assistant-reasoning-open"; "assistant-last-usage" ] do
            let _, _, card, _ = render name
            let w = show card
            let spots =
                all card
                |> List.filter (fun c -> (c :? Border || c :? Button) && c.Focusable && not (isNull c.Cursor))
            Assert.NotEmpty spots
            for s in spots do
                let peer = Automation.Peers.ControlAutomationPeer.CreatePeerForElement s
                Assert.Equal(Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
                Assert.IsAssignableFrom<Automation.Provider.IInvokeProvider>(peer) |> ignore
            w.Close())

[<Fact>]
let ``读屏 Invoke 复制消息与展开工具卡`` () =
    Headless.run (fun () ->
        let _, _, card, _ = render "assistant-tools"
        let w = show card
        Fixture.log.Clear()
        let via (c: Control) =
            ((Automation.Peers.ControlAutomationPeer.CreatePeerForElement c :> obj) :?> Automation.Provider.IInvokeProvider).Invoke()
            Threading.Dispatcher.UIThread.RunJobs()
        via (named "复制助手消息 1" card)
        Assert.Contains(Fixture.log, fun s -> s.StartsWith "copy:")
        let header = named "展开工具调用 builtin_echo" card
        via header
        Assert.Equal("收起工具调用 builtin_echo", AutomationProperties.GetName header)
        w.Close())
