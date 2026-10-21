namespace Wanxiang2.Gallery

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Templates
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.Theme

/// 交互式画廊窗口：左侧是条目表，右侧是当前条目的真实控件。
///
/// 这是画廊的**开发用法**：改一个控件库 → 只重建那个库 → 重开窗口就看到新样子，
/// 不需要连主机、不需要走旅程、不需要打整个 feed。
module Window =

    /// 条目表（左侧）：单击切换右侧显示的状态。
    /// 条目用字符串本身当数据（名字就是键），不另造包装类型。
    let buildList (entries: Entry list) (onPick: string -> unit) : ListBox =
        let list =
            ListBox(Background = Brushes.Transparent, BorderThickness = Thickness 0.0)

        list.ItemsSource <- entries |> List.map (fun e -> e.Name)

        list.ItemTemplate <-
            FuncDataTemplate<string>(
                (fun name _ ->
                    let e = entries |> List.find (fun x -> x.Name = name)
                    let title = TextBlock(Text = e.Name, FontWeight = FontWeight.Medium)
                    let widget = TextBlock(Text = e.Widget, FontSize = 11.0, Opacity = 0.7)

                    let note =
                        TextBlock(Text = e.Note, FontSize = 11.0, Opacity = 0.8, TextWrapping = TextWrapping.Wrap)

                    let column = StackPanel(Spacing = 1.0)
                    column.Children.Add(title)
                    column.Children.Add(widget)
                    column.Children.Add(note)
                    Border(Padding = Thickness(8.0, 6.0), Child = column) :> Control),
                true
            )

        list.SelectionChanged.Add(fun args ->
            let added = args.AddedItems

            if added.Count > 0 then
                match added[0] with
                | :? string as name -> onPick name
                | _ -> ())

        list

    /// 画廊的主窗口。
    let build (ctx: GalleryContext) (initial: string option) : Window =
        let entries = Gallery.entries

        let stage =
            Border(Background = ctx.Theme.Brush Wanxiang2.ThemePalette.PaletteKey.Canvas)

        let title = TextBlock(FontWeight = FontWeight.SemiBold)

        let note =
            TextBlock(FontSize = 12.0, Opacity = 0.8, TextWrapping = TextWrapping.Wrap)

        let show name =
            match Gallery.find name with
            | None -> ()
            | Some entry ->
                title.Text <- entry.Name
                note.Text <- entry.Note
                // 每次切换都新建上下文：Desk 持有桌内瞬态（上一帧投影、滚动位置、选中项），
                // 复用会让上一个状态漏进下一个。
                let fresh = Gallery.createContext ()
                stage.Child <- Gallery.build fresh entry

        let list = buildList entries (fun name -> show name)

        let header = StackPanel(Spacing = 2.0, Margin = Thickness(12.0, 10.0, 12.0, 8.0))
        header.Children.Add(title)
        header.Children.Add(note)

        let leftPane =
            Border(Background = ctx.Theme.Brush Wanxiang2.ThemePalette.PaletteKey.Surface)

        let leftDock = DockPanel()
        DockPanel.SetDock(header, Dock.Top)
        leftDock.Children.Add(header)
        leftDock.Children.Add(list)
        leftPane.Child <- leftDock

        let splitter =
            GridSplitter(
                Background = Brushes.Transparent,
                ResizeDirection = GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Width = 6.0
            )

        let root = Grid(ColumnDefinitions = ColumnDefinitions.Parse("300, 6, *"))
        Grid.SetColumn(leftPane, 0)
        Grid.SetColumn(splitter, 1)
        Grid.SetColumn(stage, 2)
        root.Children.Add(leftPane)
        root.Children.Add(splitter)
        root.Children.Add(stage)

        let win = Window(Title = "万象组件画廊", Width = 1280.0, Height = 860.0, Content = root)

        // 初次显示
        let first = defaultArg initial (entries |> List.head |> fun e -> e.Name)
        show first
        list.SelectedItem <- first

        win
