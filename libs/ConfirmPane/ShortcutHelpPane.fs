namespace Wanxiang2.ShortcutHelpPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 快捷键帮助的内容（迁自旧 Views/Dialogs.fs 的 shortcuts）。分组与条目由组合者给（旧原文见组合者）。
/// 唯一的键盘停留点是关闭键；没有内容级 Enter 快捷（会误关嵌套层）。
type ShortcutHelpPane(?sections: (string * (string * string) list) list) =
    let closed = Event<unit>()
    let closeButton = Button(Content = "关闭")

    do
        closeButton.Classes.Add "primary"
        closeButton.Classes.Add "button-primary"
        AutomationProperties.SetHelpText(closeButton, "关闭快捷键帮助 (Esc / Enter)")
        ToolTip.SetTip(closeButton, "关闭快捷键帮助 (Esc / Enter)")
        closeButton.HorizontalAlignment <- HorizontalAlignment.Right
        closeButton.Click.Add(fun _ -> closed.Trigger())

    let titleBlock = TextBlock(Text = "键盘快捷键", FontWeight = FontWeight.Medium)
    let sectionsPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)

    let renderSections (secList: (string * (string * string) list) list) =
        sectionsPanel.Children.Clear()

        for (category, items) in secList do
            let catHeader = TextBlock(Text = category, FontWeight = FontWeight.Medium)
            catHeader.Classes.Add "section-label"
            sectionsPanel.Children.Add catHeader
            let rows = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)

            for (keys, description) in items do
                let keyText =
                    TextBlock(Text = keys, HorizontalAlignment = HorizontalAlignment.Center)

                keyText.FontWeight <- FontWeight.Medium
                keyText.Classes.Add "muted"

                let cap =
                    Border(
                        Child = keyText,
                        Padding = Thickness(8.0, 2.0),
                        CornerRadius = CornerRadius 4.0,
                        MinWidth = 60.0
                    )

                cap.Classes.Add "grouping-card"
                let caption = TextBlock(Text = description)
                caption.VerticalAlignment <- VerticalAlignment.Center
                let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 16.0)
                row.Children.Add cap
                row.Children.Add caption
                rows.Children.Add row

            sectionsPanel.Children.Add rows

    do renderSections (defaultArg sections [])

    let view =
        let content = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        content.Children.Add titleBlock
        content.Children.Add sectionsPanel
        content.Children.Add closeButton

        let scroller =
            ScrollViewer(
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            )

        scroller.Bind(Layoutable.MaxHeightProperty, Dyn.res "Metric.DialogContentMaxHeight")
        |> ignore

        scroller

    member _.View = view :> Control
    member _.CloseButton = closeButton :> Control
    member _.InitialFocus = closeButton :> Control

    member this.Render(vs: DialogVS) =
        if not (String.IsNullOrEmpty vs.Title) then
            titleBlock.Text <- vs.Title

        match vs.ShortcutHelp with
        | Some sh ->
            let raw = sh.Sections |> List.map (fun s -> s.Category, s.Items)
            renderSections raw
        | None -> ()

    [<CLIEvent>]
    member _.Closed = closed.Publish
