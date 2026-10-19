/// 测试共用：真实主题管理者与样式、宿主件、上下文与动作
module Fixture

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView
open Wanxiang2.MessageCardView

let noTint () =
    CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }

let themed () =
    let manager = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    let styles = ThemeStyles manager
    styles.Install Application.Current.Styles
    for KeyValue(k, v) in manager.Resources do
        Application.Current.Resources[k] <- v
    manager, styles

let mutable now = DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero)

let host () =
    { Tint = noTint ()
      MathHtml = (fun _ -> None)
      Wrap = CodeWrapMemory false
      Clock = CaretClock()
      Touch = CardTouch()
      Now = fun () -> now }

let cards () =
    let manager, styles = themed ()
    MessageCards(CardLooks.ofTheme manager styles, host ()), manager

/// 品牌头像在两边都用同一个占位方块
let avatar () = Border(Width = 26.0, Height = 26.0, Background = Brushes.Transparent) :> Control

let ctxOf (f: Samples.Flags) =
    { FontSize = 14.5
      AutoCollapseReasoning = f.AutoCollapse
      Streaming = f.Streaming
      IsLastAssistant = f.IsLastAssistant
      Usage = f.Usage
      MissingAttachments = f.Missing
      AwaitingApproval = f.Awaiting
      BrandAvatar = avatar }

let log = ResizeArray<string>()

let actions =
    { CopyText = fun t -> log.Add("copy:" + t)
      Regenerate = fun () -> log.Add "regenerate"
      EditAndFork = fun m -> log.Add("fork:" + m.text)
      DeleteMessage = fun id -> log.Add(sprintf "delete:%d" id)
      DownloadAttachment = fun sha -> log.Add("download:" + sha)
      OpenLink = fun u -> log.Add("open:" + u)
      CopyLink = Some(fun id -> log.Add(sprintf "link:%d" id))
      Recover = Some(fun r -> log.Add(sprintf "recover:%A" r)) }
