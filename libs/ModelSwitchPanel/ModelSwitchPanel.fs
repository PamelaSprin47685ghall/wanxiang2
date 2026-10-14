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
    { ProviderId: string
      ProviderLabel: string
      Model: string
      IsCurrent: bool
      IsDefault: bool }

/// 插槽：筛选并分组（组合者填 ModelSwitcher.filter + ModelPickerList.groups；组头 None 不显示）与文案。
type ModelSwitchSlots =
    { Groups: string -> ModelChoice list -> (string option * ModelChoice list) list
      SearchHelp: string
      EmptyText: string
      AppliesNote: string }

/// 外观。全部由主题管理者提供活的实例。
type ModelSwitchPanelLook =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      Accent: IBrush
      TagText: IBrush
      TagSurface: IBrush
      FieldSurface: IBrush
      FieldEdge: IBrush
      Caret: IBrush
      /// 打勾图标（插槽）
      Check: IBrush -> Control
      BodyFontSize: IObservable<float>
      SmallFontSize: IObservable<float>
      CaptionFontSize: IObservable<float>
      MicroFontSize: IObservable<float>
      TagRadius: IObservable<CornerRadius>
      TagPadding: IObservable<Thickness>
      ItemPadding: IObservable<Thickness>
      ItemRadius: IObservable<CornerRadius>
      ItemMinHeight: IObservable<float>
      ItemSpacing: IObservable<float>
      ColumnSpacing: IObservable<float>
      MarkSpacing: IObservable<float>
      StackSpacing: IObservable<float>
      HeaderMargin: IObservable<Thickness>
      EmptyMargin: IObservable<Thickness>
      HeadingMargin: IObservable<Thickness>
      NoteMargin: IObservable<Thickness>
      FieldMargin: IObservable<Thickness>
      ListMaxHeight: IObservable<float>
      PanelWidth: IObservable<float>
      FieldPadding: IObservable<Thickness>
      FieldRadius: IObservable<CornerRadius>
      FieldEdgeWidth: IObservable<Thickness>
      FieldTextMinHeight: IObservable<float> }

/// 快速切换模型浮层内容（迁自旧 Views/ModelSwitcherView.fs）。
/// 激活（点击、行上 Enter/Space、搜索框 Enter 选第一条）调用 `activate`；组合者先关浮层，
/// 当前项不切换（ModelPickerList.shouldPick），其余才真正切换。
module ModelSwitchPanel =

    let private live (o: IObservable<'T>) f = o.Subscribe(fun v -> f v) |> ignore

    let rowClass = "model-row"

    let private tag (look: ModelSwitchPanelLook) (text: string) : Control =
        let t = TextBlock(Text = text, Foreground = look.TagText)
        live look.MicroFontSize (fun v -> t.FontSize <- v)
        let b = Border(Background = look.TagSurface, VerticalAlignment = VerticalAlignment.Center, Child = t)
        live look.TagRadius (fun v -> b.CornerRadius <- v)
        live look.TagPadding (fun v -> b.Padding <- v)
        b

    let private row (look: ModelSwitchPanelLook) (option: ModelChoice) (activate: ModelChoice -> unit) : Border =
        let name =
            TextBlock(Text = option.Model, FontWeight = (if option.IsCurrent then FontWeight.Medium else FontWeight.Normal), Foreground = look.Text, TextTrimming = TextTrimming.CharacterEllipsis)
        live look.SmallFontSize (fun v -> name.FontSize <- v)
        let provider = TextBlock(Text = option.ProviderLabel, Foreground = look.Muted, TextTrimming = TextTrimming.CharacterEllipsis)
        live look.MicroFontSize (fun v -> provider.FontSize <- v)
        let text = StackPanel(Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center)
        text.Children.Add name
        text.Children.Add provider
        let marks = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        live look.MarkSpacing (fun v -> marks.Spacing <- v)
        if option.IsDefault then marks.Children.Add(tag look "默认")
        if option.IsCurrent then
            let check = look.Check look.Accent
            check.VerticalAlignment <- VerticalAlignment.Center
            marks.Children.Add check
        let grid = Grid()
        live look.ColumnSpacing (fun v -> grid.ColumnSpacing <- v)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(marks, 1)
        grid.Children.Add text
        grid.Children.Add marks
        let host = Border(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, Child = grid)
        live look.ItemPadding (fun v -> host.Padding <- v)
        live look.ItemRadius (fun v -> host.CornerRadius <- v)
        live look.ItemMinHeight (fun v -> host.MinHeight <- v)
        for c in [ rowClass; "menu-item"; "surface"; "hover-plain" ] do host.Classes.Add c
        // 读屏名就是模型名；服务与标记进 HelpText。
        AutomationProperties.SetName(host, option.Model)
        AutomationProperties.SetHelpText(
            host,
            sprintf "%s%s%s" option.ProviderLabel (if option.IsDefault then "，默认" else "") (if option.IsCurrent then "，当前" else ""))
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
        if count <= 0 then None
        elif up && current <= 0 then Some None
        else Some(Some(if up then current - 1 else min (count - 1) (current + 1)))

    /// 构建浮层内容。
    let build (look: ModelSwitchPanelLook) (slots: ModelSwitchSlots) (options: ModelChoice list) (title: string) (activate: ModelChoice -> unit) : Control =
        let search =
            TextBox(
                PlaceholderText = "搜索模型或模型服务",
                BorderThickness = Thickness 0.0,
                Background = Brushes.Transparent,
                Foreground = look.Text,
                CaretBrush = look.Caret,
                Padding = Thickness 0.0,
                VerticalContentAlignment = VerticalAlignment.Center,
                Name = "ModelSwitcherSearch")
        live look.BodyFontSize (fun v -> search.FontSize <- v)
        live look.FieldTextMinHeight (fun v -> search.MinHeight <- v)
        let shell = Border(Background = look.FieldSurface, BorderBrush = look.FieldEdge, Child = search)
        live look.FieldEdgeWidth (fun v -> shell.BorderThickness <- v)
        live look.FieldRadius (fun v -> shell.CornerRadius <- v)
        live look.FieldPadding (fun v -> shell.Padding <- v)
        live look.FieldMargin (fun v -> shell.Margin <- v)
        shell.Classes.Add "surface"
        shell.Classes.Add "focus-soft"
        AutomationProperties.SetName(search, "搜索模型")
        // 键盘路径写进帮助文字；空结果 polite 播报。
        AutomationProperties.SetHelpText(search, slots.SearchHelp)
        let list = StackPanel(Orientation = Orientation.Vertical, Name = "ModelSwitcherList")
        live look.ItemSpacing (fun v -> list.Spacing <- v)
        let empty = TextBlock(Text = slots.EmptyText, Foreground = look.Muted, IsVisible = false)
        live look.CaptionFontSize (fun v -> empty.FontSize <- v)
        live look.EmptyMargin (fun v -> empty.Margin <- v)
        AutomationProperties.SetLiveSetting(empty, AutomationLiveSetting.Polite)
        let rows () = list.Children |> Seq.choose (function :? Border as b when b.Classes.Contains rowClass -> Some b | _ -> None) |> Array.ofSeq
        let mutable shown: ModelChoice list = options
        let render (query: string) =
            let groups = slots.Groups query options
            shown <- groups |> List.collect snd
            list.Children.Clear()
            for label, items in groups do
                label
                |> Option.iter (fun text ->
                    let h = TextBlock(Text = text)
                    h.Classes.Add "section-label"
                    live look.HeaderMargin (fun v -> h.Margin <- v)
                    list.Children.Add h)
                for o in items do list.Children.Add(row look o activate)
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
        let heading = TextBlock(Text = title, Foreground = look.Muted)
        live look.CaptionFontSize (fun v -> heading.FontSize <- v)
        live look.HeadingMargin (fun v -> heading.Margin <- v)
        let note = TextBlock(Text = slots.AppliesNote, Name = "ModelSwitcherNote", Foreground = look.Faint, TextWrapping = TextWrapping.Wrap)
        live look.MicroFontSize (fun v -> note.FontSize <- v)
        live look.NoteMargin (fun v -> note.Margin <- v)
        let inner = StackPanel(Orientation = Orientation.Vertical)
        inner.Children.Add list
        inner.Children.Add empty
        let scroller =
            ScrollViewer(
                Content = inner,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
        live look.ListMaxHeight (fun v -> scroller.MaxHeight <- v)
        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        let root = StackPanel(Orientation = Orientation.Vertical)
        live look.StackSpacing (fun v -> root.Spacing <- v)
        live look.PanelWidth (fun v -> root.Width <- v)
        for c in [ shell :> Control; heading; scroller; divider; note ] do root.Children.Add c
        root :> Control
