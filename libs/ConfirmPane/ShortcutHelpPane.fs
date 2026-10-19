namespace Wanxiang2.ShortcutHelpPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Layout
open Avalonia.Media

/// 快捷键帮助的内容（迁自旧 Views/Dialogs.fs 的 shortcuts）。分组与条目由组合者给（旧原文见组合者）。
/// 唯一的键盘停留点是关闭键；没有内容级 Enter 快捷（会误关嵌套层）。
type ShortcutHelpPane(look: ShortcutHelpPaneLook, sections: (string * (string * string) list) list) =
    let kit = Kit(look)
    let closed = Event<unit>()
    let closeButton = kit.Button(Tone.Primary, "关闭", fun () -> closed.Trigger())
    let view =
        AutomationProperties.SetHelpText(closeButton, "关闭快捷键帮助 (Esc / Enter)")
        ToolTip.SetTip(closeButton, "关闭快捷键帮助 (Esc / Enter)")
        closeButton.HorizontalAlignment <- HorizontalAlignment.Right
        let panel = StackPanel(Orientation = Orientation.Vertical)
        kit.Live(look.SpaceXl, fun v -> panel.Spacing <- v)
        for (category, items) in sections do
            panel.Children.Add(kit.SectionLabel category)
            let rows = StackPanel(Orientation = Orientation.Vertical)
            kit.Live(look.SpaceMd, fun v -> rows.Spacing <- v)
            for (keys, description) in items do
                let keyText = kit.Text(keys, look.MicroFontSize, look.Muted)
                keyText.FontWeight <- FontWeight.Medium
                keyText.HorizontalAlignment <- HorizontalAlignment.Center
                kit.Live(look.MonoFont, fun f -> keyText.FontFamily <- f)
                let cap = kit.Card(look.SpaceMd, look.SpaceXXs, look.RadiusSm, keyText)
                kit.Live(look.KeyCapMinWidth, fun v -> cap.MinWidth <- v)
                let caption = kit.Text(description, look.SmallFontSize, look.Text)
                caption.VerticalAlignment <- VerticalAlignment.Center
                rows.Children.Add(kit.HStack(look.SpaceXl, [ cap :> Control; caption :> Control ]))
            panel.Children.Add rows
        let content = kit.VStack(look.Space2xl, [ kit.Title "键盘快捷键" :> Control; panel :> Control; closeButton :> Control ])
        let scroller =
            ScrollViewer(
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto)
        kit.Live(look.ContentMaxHeight, fun v -> scroller.MaxHeight <- v)
        scroller

    member _.View = view :> Control
    member _.CloseButton = closeButton :> Control
    member _.InitialFocus = closeButton :> Control
    [<CLIEvent>] member _.Closed = closed.Publish
