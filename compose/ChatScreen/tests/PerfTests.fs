module Wanxiang2.ChatScreen.Tests.PerfTests

// 迁自旧 IncrementalRenderTests「长对话的流式刷新代价与消息条数无关」：200 条历史下流式每帧 < 20ms。
// 卡片身份直接用 ScreenKey 结构相等对账；每帧格式化身份（%A）会把这条线压穿。

open System
open System.Diagnostics
open Avalonia
open Avalonia.Controls
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ChatScreen
open Wanxiang2.ChatScreenTests

let private messageAt (index: int) : MessageView =
    { MessageView.empty with
        role = (if index % 2 = 0 then "user" else "assistant")
        text = $"一段示例正文，带 **加粗** 与 `代码`。\n\n第 {index} 条"
        commitId = Some(uint64 index)
        committedAt = Some DateTimeOffset.UtcNow }

[<Fact>]
let ``长对话的流式刷新代价与消息条数无关`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles theme
        styles.Install Application.Current.Styles
        let s = GoldenTests.screen theme styles
        let w = Window(Width = 900.0, Height = 800.0, Content = s.Host)
        w.Show()
        s.SetConversationChrome true
        s.Pane.HideEmpty()
        let messages = [ for i in 1 .. 200 -> messageAt i ]
        let frame n =
            { Messages = messages; Streaming = Some { MessageView.empty with role = "assistant"; text = String('x', n) }
              Error = None; FontSize = 14.5; AutoCollapseReasoning = false; Usage = None; MissingAttachments = Set.empty }
        s.Render(frame 1)
        let watch = Stopwatch.StartNew()
        for n in 2 .. 21 do s.Render(frame n)
        watch.Stop()
        let perFrame = watch.Elapsed.TotalMilliseconds / 20.0
        w.Close()
        Assert.True(perFrame < 20.0, $"200 条历史下流式每帧 {perFrame:F2} ms，仍然太贵"))
