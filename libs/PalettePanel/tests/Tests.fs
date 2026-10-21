module Wanxiang2.PalettePanel.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.PalettePanel

let item id kind title recent =
    {
        Id = id
        Title = title
        Subtitle = ""
        KindLabel = kind
        Shortcut = None
        Recent = recent
    }

let sample =
    [
        item "act:new" "操作" "新建对话" false
        item "act:theme" "操作" "切换主题" false
        item "set:appearance" "设置" "设置 › 外观与操作" false
        item "conv:1" "对话" "周报草稿" false
        item "conv:2" "对话" "Rust 学习笔记" false
    ]

/// 组合者的查询：子串筛选 + 末尾全文交接行（文案同 CommandPalette.fullTextTitle）
let slots (recent: string list) =
    {
        Query =
            fun q ->
                let ranked =
                    if String.IsNullOrWhiteSpace q then
                        sample
                        |> List.map (fun i ->
                            { i with
                                Recent = List.contains i.Id recent
                            })
                    else
                        sample |> List.filter (fun i -> i.Title.Contains q)

                let ranked =
                    (ranked |> List.filter _.Recent)
                    @ (ranked |> List.filter (fun i -> not i.Recent))

                let full =
                    if String.IsNullOrWhiteSpace q then
                        []
                    else
                        [
                            {
                                Id = "search:fulltext"
                                Title = sprintf "在全部消息中搜索「%s」" q
                                Subtitle = "交给侧栏搜索，查找消息正文"
                                KindLabel = "操作"
                                Shortcut = Some "Ctrl / ⌘ + Shift + F"
                                Recent = false
                            }
                        ]

                {
                    Rows = ranked @ full
                    MatchCount = ranked.Length
                    HasRecent = not recent.IsEmpty
                }
        CompactKeys = fun k -> k.Replace(" ", "")
        PrefersTouch = fun () -> false
    }

let texts (root: Control) =
    root.GetVisualDescendants()
    |> Seq.choose (function
        | :? TextBlock as t -> Option.ofObj t.Text
        | _ -> None)
    |> List.ofSeq

let press (c: Control) k =
    let e =
        KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = KeyModifiers.None)

    c.RaiseEvent e
    Dispatcher.UIThread.RunJobs()
    e

// 迁自旧 R20PaletteTests。
[<Fact>]
let ``376 面板键盘：方向键选择、回车执行所选项、全文交接在末尾`` () =
    Headless.run (fun () ->
        let mutable ran: (string * string) option = None

        let content, search =
            PalettePanel.build (slots []) (fun i q -> ran <- Some(i.Id, q))

        let window = Window(Width = 700.0, Height = 600.0, Content = content)
        window.Show()
        Dispatcher.UIThread.RunJobs()
        search.Text <- "对话"
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("在全部消息中搜索「对话」", texts content)
        press search Key.Down |> ignore
        press search Key.Enter |> ignore
        Assert.True(ran.IsSome)
        Assert.Equal(("search:fulltext", "对话"), ran.Value)

        let status =
            content.GetVisualDescendants()
            |> Seq.pick (function
                | :? TextBlock as t when t.Name = "CommandPaletteStatus" -> Some t
                | _ -> None)

        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting status)
        search.Text <- "zzzzq"
        Dispatcher.UIThread.RunJobs()
        Assert.Contains("没有匹配", status.Text)
        window.Close())

[<Fact>]
let ``selection announces position and wraps; rows carry kind and shortcut help`` () =
    Headless.run (fun () ->
        let content, search = PalettePanel.build (slots []) (fun _ _ -> ())
        let window = Window(Width = 700.0, Height = 600.0, Content = content)
        window.Show()
        search.Text <- "对话"
        Dispatcher.UIThread.RunJobs()

        let status =
            content.GetVisualDescendants()
            |> Seq.pick (function
                | :? TextBlock as t when t.Name = "CommandPaletteStatus" -> Some t
                | _ -> None)

        Assert.Equal("1 条结果", status.Text)
        press search Key.Up |> ignore
        Assert.Equal("在全部消息中搜索「对话」，操作，第 2 项，共 2 项", status.Text)

        let rows =
            content.GetVisualDescendants()
            |> Seq.choose (function
                | :? Border as b when b.Classes.Contains PalettePanel.rowClass -> Some b
                | _ -> None)
            |> List.ofSeq

        Assert.True(rows[1].Classes.Contains "selected")
        Assert.False(rows[0].Classes.Contains "selected")
        Assert.Equal("操作，交给侧栏搜索，查找消息正文，快捷键 Ctrl / ⌘ + Shift + F", AutomationProperties.GetHelpText rows[1])
        Assert.Contains("Ctrl/⌘+Shift+F", texts content)
        window.Close())

[<Fact>]
let ``blank query groups recent before actions`` () =
    Headless.run (fun () ->
        let content, _ = PalettePanel.build (slots [ "conv:2" ]) (fun _ _ -> ())
        let window = Window(Width = 700.0, Height = 600.0, Content = content)
        window.Show()
        let all = texts content
        let i0 = all |> List.findIndex ((=) "最近使用")
        let i1 = all |> List.findIndex ((=) "Rust 学习笔记")
        let i2 = all |> List.findIndex ((=) "操作")
        let i3 = all |> List.findIndex ((=) "新建对话")
        Assert.True(i0 < i1 && i1 < i2 && i2 < i3)
        window.Close())

[<Fact>]
let ``arrow keys inside the list move between rows and up from the first returns to search`` () =
    Headless.run (fun () ->
        let content, search = PalettePanel.build (slots []) (fun _ _ -> ())
        let window = Window(Width = 700.0, Height = 600.0, Content = content)
        window.Show()
        Dispatcher.UIThread.RunJobs()

        let rows =
            content.GetVisualDescendants()
            |> Seq.choose (function
                | :? Border as b when b.Classes.Contains PalettePanel.rowClass -> Some b
                | _ -> None)
            |> List.ofSeq

        rows[0].Focus() |> ignore
        press rows[0] Key.Down |> ignore
        Assert.True rows[1].IsFocused
        press rows[1] Key.Up |> ignore
        press rows[0] Key.Up |> ignore
        Assert.True search.IsFocused
        window.Close())
