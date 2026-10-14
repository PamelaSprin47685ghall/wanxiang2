module Wanxiang2.ChatPane.Tests.GoldenTests

// 整树金样：旧 ChatView 主体（tools/panegold 用 extract-ledger 533b85b 生成），按同一份 Dump 转写比对。

open System
open System.IO
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ChatPane
open Wanxiang2.ChatPaneTests

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

/// 逐行比对，失败时指出第一处不同
let private same (expected: string) (actual: string) =
    let e = expected.TrimEnd('\n').Split('\n')
    let a = actual.TrimEnd('\n').Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

let private logo (size: float) = Border(Width = size, Height = size, Background = Brushes.Transparent) :> Control

[<Fact>]
let ``主体各状态与旧 ChatView 同一棵树`` () =
    Headless.run (fun () ->
        let font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
        let sb = Text.StringBuilder()
        let cases: (string * (ChatPane<string> -> unit)) list =
            [ "messages", (fun p -> p.HideEmpty())
              "skeleton", (fun p -> p.HideEmpty(); p.ShowSkeleton())
              "older-failure", (fun p -> p.HideEmpty(); p.ShowOlderFailure("没能取到更早的消息：超时", ignore))
              "empty-conversation", (fun p -> p.ShowEmpty(ChatEmptyState.EmptyConversation, None))
              "no-conversation-primary", (fun p -> p.ShowEmpty(ChatEmptyState.NoConversation, Some("新对话", ignore)))
              "not-connected", (fun p -> p.ShowEmpty(ChatEmptyState.NotConnected, Some("连接主机", ignore)))
              "connection-failed", (fun p -> p.ShowEmpty(ChatEmptyState.ConnectionFailed "超时", Some("重试", ignore)))
              "no-provider", (fun p -> p.ShowEmpty(ChatEmptyState.NoProvider, Some("添加模型", ignore))) ]
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles theme
        styles.Install Avalonia.Application.Current.Styles
        for name, show in cases do
            let pane = ChatPane<string>(ChatPaneLooks.ofTheme theme styles, logo)
            pane.SetStarterPick(Some ignore)
            pane.SetPresets [ "晨记", "每天早上写三件", ignore ]
            show pane
            let dump = Dump.tree font pane.Host
            sb.Append("== ").Append(name).Append('\n').Append(dump.TrimEnd('\n')).Append('\n') |> ignore
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "chatpane-new.txt"), sb.ToString())
        same (golden "chatpane.txt") (sb.ToString()))
