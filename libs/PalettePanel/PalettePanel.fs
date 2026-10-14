namespace Wanxiang2.PalettePanel

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 面板里的一行（组合者从 CommandPalette 的条目与排序得出）。
type PaletteRow =
    { Id: string
      Title: string
      Subtitle: string
      /// 种类名（对话 / 操作 / 设置）
      KindLabel: string
      Shortcut: string option
      /// 属于「最近使用」
      Recent: bool }

/// 一次查询的结果：行（全文交接行由组合者追加在末尾）、真正命中条数、是否有最近使用记录。
type PaletteResult = { Rows: PaletteRow list; MatchCount: int; HasRecent: bool }

/// 插槽：查询、键位缩写、是否按触屏对待。
type PaletteSlots =
    { Query: string -> PaletteResult
      CompactKeys: string -> string
      PrefersTouch: unit -> bool }

/// 面板外观。全部由主题管理者提供活的实例。
type PalettePanelLook =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      /// 选中行的底（旧 surfaceContainer）
      Selected: IBrush
      FieldSurface: IBrush
      FieldEdge: IBrush
      Caret: IBrush
      BodyFontSize: IObservable<float>
      SmallFontSize: IObservable<float>
      MicroFontSize: IObservable<float>
      MonoFont: IObservable<FontFamily>
      ItemPadding: IObservable<Thickness>
      ItemRadius: IObservable<CornerRadius>
      ItemMinHeight: IObservable<float>
      ItemSpacing: IObservable<float>
      ColumnSpacing: IObservable<float>
      StackSpacing: IObservable<float>
      StatusMargin: IObservable<Thickness>
      HeaderMargin: IObservable<Thickness>
      HintMargin: IObservable<Thickness>
      ListMaxHeight: IObservable<float>
      FieldPadding: IObservable<Thickness>
      FieldRadius: IObservable<CornerRadius>
      FieldEdgeWidth: IObservable<Thickness>
      FieldTextMinHeight: IObservable<float> }

/// 命令面板内容（迁自旧 Views/CommandPaletteView.fs）。放进对话层、执行前关面板由组合者做。
module PalettePanel =

    let private live (o: IObservable<'T>) f = o.Subscribe(fun v -> f v) |> ignore

    /// 行宿主上的类：基底透明（menu-item）+ 统一悬停（surface hover-plain）+ 自身标记
    let rowClass = "palette-row"

    let private row (look: PalettePanelLook) (slots: PaletteSlots) (item: PaletteRow) (run: PaletteRow -> unit) : Border =
        let title = TextBlock(Text = item.Title, Foreground = look.Text, TextTrimming = TextTrimming.CharacterEllipsis)
        live look.SmallFontSize (fun v -> title.FontSize <- v)
        let text = StackPanel(Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center)
        text.Children.Add title
        if not (String.IsNullOrWhiteSpace item.Subtitle) then
            let sub = TextBlock(Text = item.Subtitle, Foreground = look.Muted, TextTrimming = TextTrimming.CharacterEllipsis)
            live look.MicroFontSize (fun v -> sub.FontSize <- v)
            text.Children.Add sub
        let right = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        live look.ColumnSpacing (fun v -> right.Spacing <- v)
        // 触屏没有键盘：不显示键位列（读屏 HelpText 仍带键位）。
        match item.Shortcut with
        | Some keys when not (slots.PrefersTouch()) ->
            let k = TextBlock(Text = slots.CompactKeys keys, Foreground = look.Faint, VerticalAlignment = VerticalAlignment.Center)
            live look.MicroFontSize (fun v -> k.FontSize <- v)
            live look.MonoFont (fun v -> k.FontFamily <- v)
            right.Children.Add k
        | _ -> ()
        let kind = TextBlock(Text = item.KindLabel, Foreground = look.Muted, VerticalAlignment = VerticalAlignment.Center)
        live look.MicroFontSize (fun v -> kind.FontSize <- v)
        right.Children.Add kind
        let grid = Grid()
        live look.ColumnSpacing (fun v -> grid.ColumnSpacing <- v)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(right, 1)
        grid.Children.Add text
        grid.Children.Add right
        let host = Border(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, Child = grid)
        live look.ItemPadding (fun v -> host.Padding <- v)
        live look.ItemRadius (fun v -> host.CornerRadius <- v)
        live look.ItemMinHeight (fun v -> host.MinHeight <- v)
        for c in [ rowClass; "menu-item"; "surface"; "hover-plain" ] do host.Classes.Add c
        AutomationProperties.SetName(host, item.Title)
        AutomationProperties.SetHelpText(
            host,
            String.Join("，", [ yield item.KindLabel
                               if item.Subtitle <> "" then yield item.Subtitle
                               match item.Shortcut with
                               | Some k -> yield "快捷键 " + k
                               | None -> () ]))
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.ListItem)
        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                run item)
        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                run item)
        host

    let private header (look: PalettePanelLook) (text: string) =
        let h = TextBlock(Text = text)
        h.Classes.Add "section-label"
        live look.HeaderMargin (fun v -> h.Margin <- v)
        h

    /// 构建面板。每次输入都重新查询；`run` 收到所选行与当前输入。返回 (内容, 搜索框)。
    let build (look: PalettePanelLook) (slots: PaletteSlots) (run: PaletteRow -> string -> unit) : Control * TextBox =
        let search =
            TextBox(
                PlaceholderText = "搜索对话、操作或设置",
                BorderThickness = Thickness 0.0,
                Background = Brushes.Transparent,
                Foreground = look.Text,
                CaretBrush = look.Caret,
                Padding = Thickness 0.0,
                VerticalContentAlignment = VerticalAlignment.Center,
                Name = "CommandPaletteSearch")
        live look.BodyFontSize (fun v -> search.FontSize <- v)
        live look.FieldTextMinHeight (fun v -> search.MinHeight <- v)
        let shell = Border(Background = look.FieldSurface, BorderBrush = look.FieldEdge, Child = search)
        live look.FieldEdgeWidth (fun v -> shell.BorderThickness <- v)
        live look.FieldRadius (fun v -> shell.CornerRadius <- v)
        live look.FieldPadding (fun v -> shell.Padding <- v)
        shell.Classes.Add "surface"
        shell.Classes.Add "focus-soft"
        AutomationProperties.SetName(search, "命令面板")
        AutomationProperties.SetHelpText(search, "输入筛选，支持拼音首字母；上下方向键选择，回车执行，Esc 关闭")
        let list = StackPanel(Orientation = Orientation.Vertical, Name = "CommandPaletteList")
        live look.ItemSpacing (fun v -> list.Spacing <- v)
        AutomationProperties.SetName(list, "结果")
        let status = TextBlock(Name = "CommandPaletteStatus", Foreground = look.Muted)
        live look.MicroFontSize (fun v -> status.FontSize <- v)
        live look.StatusMargin (fun v -> status.Margin <- v)
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite)
        let mutable shown: PaletteRow list = []
        let mutable selected = 0
        let rows () = list.Children |> Seq.choose (function :? Border as b when b.Classes.Contains rowClass -> Some b | _ -> None) |> Array.ofSeq
        let paint () =
            rows ()
            |> Array.iteri (fun i r ->
                if i = selected then
                    r.Background <- look.Selected
                    r.Classes.Add "selected"
                else
                    r.ClearValue Border.BackgroundProperty
                    r.Classes.Remove "selected" |> ignore)
        let current () = search.Text |> Option.ofObj |> Option.defaultValue ""
        let execute (item: PaletteRow) = run item (current ())
        let render () =
            let q = current ()
            let result = slots.Query q
            shown <- result.Rows
            selected <- 0
            list.Children.Clear()
            let blank = String.IsNullOrWhiteSpace q
            let mutable headerShown = false
            if blank && shown |> List.exists _.Recent then list.Children.Add(header look "最近使用")
            for item in shown do
                if blank && not headerShown && not item.Recent && result.HasRecent then
                    headerShown <- true
                    list.Children.Add(header look "操作")
                list.Children.Add(row look slots item execute)
            status.Text <-
                if result.MatchCount = 0 then "没有匹配的对话、操作或设置。"
                else sprintf "%d 条结果" result.MatchCount
            paint ()
        render ()
        search.TextChanged.Add(fun _ -> render ())
        let move delta =
            let n = shown.Length
            if n > 0 then
                selected <- (selected + delta + n) % n
                paint ()
                let r = rows ()
                if selected < r.Length then
                    r[selected].BringIntoView()
                    // 读屏：焦点留在输入框，播报所选项。
                    status.Text <- sprintf "%s，%s，第 %d 项，共 %d 项" shown[selected].Title shown[selected].KindLabel (selected + 1) n
        search.KeyDown.Add(fun e ->
            match e.Key with
            | Key.Down -> e.Handled <- true; move 1
            | Key.Up -> e.Handled <- true; move -1
            | Key.Enter ->
                if selected < shown.Length then
                    e.Handled <- true
                    execute shown[selected]
            | _ -> ())
        // 行上也能用方向键（Tab 进入列表后）；首行再上回到输入框。
        list.KeyDown.Add(fun e ->
            let items = rows ()
            if items.Length > 0 && (e.Key = Key.Up || e.Key = Key.Down) then
                let cur = items |> Array.tryFindIndex _.IsFocused |> Option.defaultValue -1
                e.Handled <- true
                if e.Key = Key.Up && cur <= 0 then search.Focus() |> ignore
                else
                    let next = if e.Key = Key.Up then cur - 1 else min (items.Length - 1) (cur + 1)
                    items[next].Focus(NavigationMethod.Directional) |> ignore
                    items[next].BringIntoView())
        let scroller =
            ScrollViewer(
                Content = list,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
        live look.ListMaxHeight (fun v -> scroller.MaxHeight <- v)
        let hint = TextBlock(Text = "↑↓ 选择 · Enter 执行 · Esc 关闭 · 支持拼音首字母", Foreground = look.Faint)
        live look.MicroFontSize (fun v -> hint.FontSize <- v)
        live look.HintMargin (fun v -> hint.Margin <- v)
        let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        divider.Classes.Add "hairline"
        let root = StackPanel(Orientation = Orientation.Vertical, Name = "CommandPalette")
        live look.StackSpacing (fun v -> root.Spacing <- v)
        AutomationProperties.SetName(root, "命令面板")
        for c in [ shell :> Control; status; scroller; divider; hint ] do root.Children.Add c
        root :> Control, search
