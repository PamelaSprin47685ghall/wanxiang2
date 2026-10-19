namespace Wanxiang2.DevicesPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media

module internal Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

[<AutoOpen>]
module ButtonExtensions =
    type Button with
        member this.SetText(text: string) =
            this.Content <- text
            AutomationProperties.SetName(this, text)
        member this.SetEnabled(enabled: bool) =
            this.IsEnabled <- enabled

[<RequireQualifiedAccess>]
type TagTone =
    | Neutral
    | Warning
    | Danger

module internal Ui =
    let heading text =
        let t = TextBlock(Text = text, FontSize = 17.0, FontWeight = FontWeight.Medium)
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        t

    let smallText text =
        let t = TextBlock(Text = text, FontSize = 13.0)
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        t

    let title text =
        let t = TextBlock(Text = text, FontSize = 19.0, FontWeight = FontWeight.Medium)
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        t

    let caption text =
        let t = TextBlock(Text = text, FontSize = 12.0, TextWrapping = TextWrapping.Wrap)
        t.Classes.Add "muted"
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        t

    let label text =
        let t = TextBlock(Text = text, FontSize = 13.0, FontWeight = FontWeight.Medium)
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        t

    let sectionLabel text =
        let t = TextBlock(Text = text, FontSize = 13.0, FontWeight = FontWeight.Medium)
        t.Classes.Add "section-label"
        t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        t

    let card (child: Control) =
        let b = Border(Child = child, Padding = Thickness(20.0), CornerRadius = CornerRadius(8.0))
        b.Classes.Add "grouping-card"
        b.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer") |> ignore
        b.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Border") |> ignore
        b.BorderThickness <- Thickness 1.0
        b

    let tag (tone: TagTone) (text: string) =
        let t = TextBlock(Text = text, VerticalAlignment = VerticalAlignment.Center)
        let b = Border(VerticalAlignment = VerticalAlignment.Center, Child = t)
        b.Classes.Add "tag"
        match tone with
        | TagTone.Neutral -> ()
        | TagTone.Warning -> b.Classes.Add "tag-warning"
        | TagTone.Danger -> b.Classes.Add "tag-danger"
        b

    let statusDot () =
        Avalonia.Controls.Shapes.Ellipse(Width = 7.0, Height = 7.0, VerticalAlignment = VerticalAlignment.Center)

    let hairline () =
        let b = Border(Height = 1.0, HorizontalAlignment = HorizontalAlignment.Stretch)
        b.Classes.Add "hairline"
        b.Bind(Border.BackgroundProperty, Dyn.res "Palette.Line") |> ignore
        b

    let button cls text action =
        let b = Button(Content = text)
        AutomationProperties.SetName(b, text)
        b.Classes.Add cls
        b.Classes.Add("button-" + cls)
        b.Click.Add(fun _ -> action())
        b

    let iconButton content tip action =
        let b = Button(Content = content)
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, tip)
        ToolTip.SetTip(b, tip)
        b.Click.Add(fun _ -> action())
        b

    let textField placeholder =
        let tb = TextBox(PlaceholderText = placeholder)
        tb.Classes.Add "control-stroke"
        tb

    let textArea placeholder _minHeight =
        let tb = TextBox(PlaceholderText = placeholder, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap)
        tb.Bind(Layoutable.MinHeightProperty, Dyn.res "Metric.TextAreaMinHeight") |> ignore
        tb.Classes.Add "control-stroke"
        tb.Classes.Add "text-area"
        tb

    let fieldGroup (labelStr: string) (hintStr: string) (shell: Control) (validation: Control option) =
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        let l = label labelStr
        panel.Children.Add l
        if not (String.IsNullOrWhiteSpace hintStr) then
            let h = caption hintStr
            validation |> Option.iter (fun v ->
                h.IsVisible <- not v.IsVisible
                v.PropertyChanged.Add(fun args ->
                    if args.Property = Visual.IsVisibleProperty then
                        h.IsVisible <- not v.IsVisible))
            panel.Children.Add h
        panel.Children.Add shell
        validation |> Option.iter (fun v -> panel.Children.Add v)
        panel :> Control

    let switchRow (titleStr: string) (hintStr: string) (initial: bool) (onChanged: bool -> unit) =
        let sw = ToggleSwitch(IsChecked = Nullable initial, OnContent = "", OffContent = "")
        AutomationProperties.SetName(sw, titleStr)
        AutomationProperties.SetHelpText(sw, hintStr)
        let t = label titleStr
        let h = caption hintStr
        let left = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        left.Children.Add t
        left.Children.Add h
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(sw, Dock.Right)
        dock.Children.Add sw
        dock.Children.Add left
        let row = Border(Child = dock, Padding = Thickness(12.0, 8.0), MinHeight = 52.0)
        AutomationProperties.SetName(row, titleStr)
        let updateStatus (v: bool) =
            let s = if v then "开启" else "关闭"
            AutomationProperties.SetItemStatus(sw, s)
            AutomationProperties.SetItemStatus(row, s)
        updateStatus initial
        let mutable suppress = false
        row.PointerReleased.Add(fun e ->
            if e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                sw.IsChecked <- Nullable (not (sw.IsChecked.GetValueOrDefault())))
        sw.IsCheckedChanged.Add(fun _ ->
            let v = sw.IsChecked.GetValueOrDefault()
            updateStatus v
            if not suppress then onChanged v)
        let write (v: bool) =
            suppress <- true
            try sw.IsChecked <- Nullable v
            finally suppress <- false
            updateStatus v
        row :> Control, (fun () -> sw.IsChecked.GetValueOrDefault()), write
