namespace Wanxiang2.ModelSwitchPanel

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 一个可选模型。
type ModelChoice =
    {
        ProviderId: string
        ProviderLabel: string
        Model: string
        IsCurrent: bool
        IsDefault: bool
    }

/// 插槽：筛选并分组（组合者填 ModelSwitcher.filter + ModelPickerList.groups；组头 None 不显示）与文案。
type ModelSwitchSlots =
    {
        Groups: string -> ModelChoice list -> (string option * ModelChoice list) list
        SearchHelp: string
        EmptyText: string
        AppliesNote: string
    }

/// 快速切换模型浮层内容（迁自旧 Views/ModelSwitcherView.fs）。
/// 激活（点击、行上 Enter/Space、搜索框 Enter 选第一条）调用 `activate`；组合者先关浮层，
/// 当前项不切换（ModelPickerList.shouldPick），其余才真正切换。
module ModelSwitchPanel =

    let rowClass = "model-row"

    let private tag (text: string) : Control =
        let t = TextBlock(Text = text)
        t.Classes.Add "muted"

        let b =
            Border(
                VerticalAlignment = VerticalAlignment.Center,
                Child = t,
                CornerRadius = CornerRadius 999.0,
                Padding = Thickness(6.0, 1.0)
            )

        b.Classes.Add "surface"
        b

    let private row
        (checkIcon: (unit -> Control) option)
        (option: ModelChoice)
        (activate: ModelChoice -> unit)
        : Border =
        let name =
            TextBlock(
                Text = option.Model,
                FontWeight =
                    (if option.IsCurrent then
                         FontWeight.Medium
                     else
                         FontWeight.Normal),
                TextTrimming = TextTrimming.CharacterEllipsis
            )

        let provider =
            TextBlock(Text = option.ProviderLabel, TextTrimming = TextTrimming.CharacterEllipsis)

        provider.Classes.Add "muted"

        let text =
            StackPanel(Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center)

        text.Children.Add name
        text.Children.Add provider

        let marks =
            StackPanel(
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4.0
            )

        if option.IsDefault then
            marks.Children.Add(tag "默认")

        if option.IsCurrent then
            let check =
                match checkIcon with
                | Some f -> f ()
                | None ->
                    let b = Border(Width = 12.0, Height = 12.0)
                    b.Classes.Add "check-mark"
                    b :> Control

            check.VerticalAlignment <- VerticalAlignment.Center
            marks.Children.Add check

        let grid = Grid(ColumnSpacing = 8.0)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(marks, 1)
        grid.Children.Add text
        grid.Children.Add marks

        let host =
            Border(
                Cursor = new Cursor(StandardCursorType.Hand),
                Focusable = true,
                Child = grid,
                Padding = Thickness(12.0, 6.0),
                CornerRadius = CornerRadius 4.0,
                MinHeight = 32.0
            )

        for c in [ rowClass; "menu-item"; "surface"; "hover-plain" ] do
            host.Classes.Add c
        // 读屏名就是模型名；服务与标记进 HelpText。
        AutomationProperties.SetName(host, option.Model)

        AutomationProperties.SetHelpText(
            host,
            sprintf
                "%s%s%s"
                option.ProviderLabel
                (if option.IsDefault then "，默认" else "")
                (if option.IsCurrent then "，当前" else "")
        )

        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.MenuItem)
        ToolTip.SetTip(host, sprintf "%s · %s" option.ProviderLabel option.Model)

        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                activate option)

        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                activate option)

        host

    /// 列表内上下：不循环，顶端再上回搜索框（与 ModelPickerList.move 同规则）。None 表示不处理。
    let move (up: bool) (current: int) (count: int) : int option option =
        if count <= 0 then
            None
        elif up && current <= 0 then
            Some None
        else
            Some(Some(if up then current - 1 else min (count - 1) (current + 1)))

    /// 构建浮层内容。
    let build
        (checkIcon: (unit -> Control) option)
        (slots: ModelSwitchSlots)
        (options: ModelChoice list)
        (title: string)
        (activate: ModelChoice -> unit)
        : Control =
        let search =
            TextBox(
                PlaceholderText = "搜索模型或模型服务",
                BorderThickness = Thickness 0.0,
                Background = Brushes.Transparent,
                Padding = Thickness 0.0,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinHeight = 24.0,
                Name = "ModelSwitcherSearch"
            )

        let shell =
            Border(
                Child = search,
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius 8.0,
                Padding = Thickness(12.0, 6.0),
                Margin = Thickness 4.0
            )

        shell.Classes.Add "surface"
        shell.Classes.Add "control-stroke"
        AutomationProperties.SetName(search, "搜索模型")
        // 键盘路径写进帮助文字；空结果 polite 播报。
        AutomationProperties.SetHelpText(search, slots.SearchHelp)

        let list =
            StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0, Name = "ModelSwitcherList")

        let empty =
            TextBlock(Text = slots.EmptyText, Margin = Thickness 4.0, IsVisible = false)

        empty.Classes.Add "muted"
        AutomationProperties.SetLiveSetting(empty, AutomationLiveSetting.Polite)

        let rows () =
            list.Children
            |> Seq.choose (function
                | :? Border as b when b.Classes.Contains rowClass -> Some b
                | _ -> None)
            |> Array.ofSeq

        let mutable shown: ModelChoice list = options

        let render (query: string) =
            let groups = slots.Groups query options
            shown <- groups |> List.collect snd
            list.Children.Clear()

            for label, items in groups do
                label
                |> Option.iter (fun text ->
                    let h = TextBlock(Text = text, Margin = Thickness 4.0)
                    h.Classes.Add "section-label"
                    list.Children.Add h)

                for o in items do
                    list.Children.Add(row checkIcon o activate)

            empty.IsVisible <- List.isEmpty shown

        render ""
        search.TextChanged.Add(fun _ -> render (search.Text |> Option.ofObj |> Option.defaultValue ""))

        search.KeyDown.Add(fun e ->
            match e.Key with
            | Key.Enter ->
                // 回车直接选第一条命中。
                match List.tryHead shown with
                | Some first ->
                    e.Handled <- true
                    activate first
                | None -> ()
            | Key.Down ->
                match rows () with
                | [||] -> ()
                | items ->
                    e.Handled <- true
                    items[0].Focus(NavigationMethod.Directional) |> ignore
            | _ -> ())

        list.KeyDown.Add(fun e ->
            let items = rows ()

            if items.Length > 0 && (e.Key = Key.Up || e.Key = Key.Down) then
                let current = items |> Array.tryFindIndex _.IsFocused |> Option.defaultValue -1
                e.Handled <- true

                match move (e.Key = Key.Up) current items.Length with
                | Some None -> search.Focus() |> ignore
                | Some(Some next) ->
                    items[next].Focus(NavigationMethod.Directional) |> ignore
                    items[next].BringIntoView()
                | None -> ())

        let heading = TextBlock(Text = title, Margin = Thickness 4.0)
        heading.Classes.Add "muted"

        let note =
            TextBlock(
                Text = slots.AppliesNote,
                Name = "ModelSwitcherNote",
                Margin = Thickness 4.0,
                TextWrapping = TextWrapping.Wrap
            )

        note.Classes.Add "faint"
        let inner = StackPanel(Orientation = Orientation.Vertical)
        inner.Children.Add list
        inner.Children.Add empty

        let scroller =
            ScrollViewer(
                Content = inner,
                MaxHeight = 360.0,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto
            )

        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"

        let root =
            StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0, Width = 300.0)

        for c in [ shell :> Control; heading; scroller; divider; note ] do
            root.Children.Add c

        root :> Control
