module Wanxiang2.PaletteDesk.Tests.Tests

// 真实组合：主题外观 + CommandPalette 排序 + PalettePanel + ImeShield + OverlayDesk 对话层。

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Input
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.PaletteDesk
open Wanxiang2.DeskTests

let private item id kind title keywords =
    { id = id; kind = kind; title = title; subtitle = ""; keywords = keywords; shortcut = None }

let private sample =
    [ item "act:new" PaletteAction "新建对话" [ "new" ]
      item "act:theme" PaletteAction "切换主题" [ "theme" ]
      item "set:appearance" PaletteSetting "设置 › 外观与操作" []
      item "conv:1" PaletteConversation "周报草稿" []
      item "conv:2" PaletteConversation "Rust 学习笔记" [] ]

let private setup () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    theme, overlay, PaletteDesk(theme, overlay, (fun () -> false)), root

let private press (tb: TextBox) k =
    tb.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = KeyModifiers.None, Source = tb))

let private presenter (tb: TextBox) =
    tb.GetVisualDescendants() |> Seq.pick (function :? TextPresenter as p -> Some p | _ -> None)

// 迁自 R20PaletteTests（数据经本组合者的 CommandPalette.rank 与全文交接行）。
[<Fact>]
let ``376 面板键盘：方向键选择、回车执行所选项、全文交接在末尾`` () =
    Headless.run (fun () ->
        let _, _, palette, _ = setup ()
        let mutable ran: (string * string) option = None
        let content, search = palette.Build((fun () -> sample), (fun () -> []), fun i q -> ran <- Some(i.id, q))
        let window = Desk.show content 700.0 600.0
        search.Text <- "对话"
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("在全部消息中搜索「对话」", Desk.texts content)
        press search Key.Down
        press search Key.Enter
        Assert.True ran.IsSome
        Assert.Equal("对话", snd ran.Value)
        let status = content.GetVisualDescendants() |> Seq.pick (function :? TextBlock as t when t.Name = "CommandPaletteStatus" -> Some t | _ -> None)
        Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting status)
        search.Text <- "zzzzq"
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("没有匹配", status.Text)
        window.Close())

// 迁自 R21ImeTests：组合者装上 ImeShield 后，输入法组合期间回车不执行、方向键不移动。
[<Fact>]
let ``386 命令面板：组合中回车不执行、方向键不移动`` () =
    Headless.run (fun () ->
        let _, _, palette, _ = setup ()
        let mutable ran = 0
        let content, search = palette.Build((fun () -> []), (fun () -> []), fun _ _ -> ran <- ran + 1)
        let window = Desk.show content 800.0 600.0
        search.Text <- "对话"
        Dispatcher.UIThread.RunJobs()
        search.Focus() |> ignore
        (presenter search).PreeditText <- "dui"
        press search Key.Down
        press search Key.Enter
        Assert.Equal(0, ran)
        (presenter search).PreeditText <- null
        press search Key.Enter
        Assert.Equal(1, ran)
        window.Close())

// 旧 AppShell.ShowCommandPalette：进对话层、搜索框初始焦点；执行先关面板，下一拍才跑；已有对话框时不开。
[<Fact>]
let ``执行先关面板再在下一拍运行，已有对话框时不重复打开`` () =
    Headless.run (fun () ->
        let _, overlay, palette, root = setup ()
        let window = Desk.show root 800.0 600.0
        let ran = ResizeArray<string * string>()
        palette.Run.Add(fun (i, q) -> ran.Add((i.id, q)); Assert.False overlay.Layer.IsDialogOpen)
        palette.Show((fun () -> sample), fun () -> [ "act:theme" ])
        Dispatcher.UIThread.RunJobs()
        Assert.True overlay.Layer.IsDialogOpen
        let search = root.GetVisualDescendants() |> Seq.pick (function :? TextBox as t when t.Name = "CommandPaletteSearch" -> Some t | _ -> None)
        Assert.True search.IsFocused
        // 空查询：最近使用在前
        Assert.Contains("最近使用", Desk.texts root)
        palette.Show((fun () -> sample), fun () -> [])
        Assert.Single(root.GetVisualDescendants() |> Seq.filter (function :? TextBox as t -> t.Name = "CommandPaletteSearch" | _ -> false)) |> ignore
        press search Key.Enter
        Assert.False overlay.Layer.IsDialogOpen
        Assert.Equal<(string * string) list>([ "act:theme", "" ], List.ofSeq ran)
        window.Close())
