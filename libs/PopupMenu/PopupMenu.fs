namespace Wanxiang2.PopupMenu

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions

/// 浮层菜单里的一项。
type PopupEntry =
    { Label: string
      /// 右侧弱字（快捷键、当前值等）
      Hint: string
      Icon: (IBrush -> Control) option
      /// 选中态打勾
      Selected: bool
      Danger: bool
      Action: unit -> unit }

module PopupEntry =
    let create (label: string) (action: unit -> unit) =
        { Label = label; Hint = ""; Icon = None; Selected = false; Danger = false; Action = action }

    let withIcon icon (entry: PopupEntry) = { entry with Icon = Some icon }
    let withHint hint (entry: PopupEntry) = { entry with Hint = hint }
    let markSelected selected (entry: PopupEntry) = { entry with Selected = selected }
    let asDanger (entry: PopupEntry) = { entry with Danger = true }

/// 动态资源绑定辅助：主题管理者把活值写进 ResourceDictionary，控件绑定即随主题资源生效。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 菜单与下拉选择器的外观：画笔、图标插槽与组合者自定的静态参数（普通记录，无活值字段）。
/// 主题管得住的排版走资源键声明式绑定：字号 Metric.FontBody / Metric.FontCaption、
/// 图标高 Metric.IconGlyph、右侧槽宽 Metric.MenuRightSlotMinWidth、
/// 列间距 Space.Sm、项间距 Space.XXs、内边距 Padding.MenuItem、
/// 圆角 CornerRadius.RadiusXs、提示外边距 Margin.MenuHint、
/// 分隔外边距 Margin.MenuDivider、标题外边距 Margin.MenuHeader、
/// 下拉标题最大宽 Metric.SelectCaptionMaxWidth。本库不再有 IObservable 字段与手工 Subscribe。
type PopupMenuAppearance =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      Danger: IBrush
      Accent: IBrush
      /// 打勾图标（插槽）
      Check: IBrush -> Control
      /// 下拉箭头图标（插槽）
      Chevron: IBrush -> Control
      /// 图标槽宽
      IconSlotWidth: float
      /// 菜单项最小高
      ItemMinHeight: float
      /// 菜单最大高（滚动上限）
      MaxHeight: float }

/// 浮层菜单内容（迁自旧 Controls/Menu.fs）。只造内容；放进浮层、关浮层由组合者做：
/// 激活时调用 `activate`，组合者先关浮层再执行条目动作。
module PopupMenu =

    let private handCursor = new Cursor(StandardCursorType.Hand)

    /// 菜单项宿主上的样式类（只挂类名，样式由主题提供）。
    let itemClass = "menu-item"

    let renderEntry (look: PopupMenuAppearance) (activate: PopupEntry -> unit) (entry: PopupEntry) : Control =
        let foreground = if entry.Danger then look.Danger else look.Text
        let iconSlot = Border(VerticalAlignment = VerticalAlignment.Center)
        iconSlot.Width <- look.IconSlotWidth
        iconSlot.Bind(Layoutable.HeightProperty, Dyn.res "Metric.IconGlyph") |> ignore
        entry.Icon
        |> Option.iter (fun icon ->
            let glyph = icon (if entry.Danger then look.Danger else look.Muted)
            glyph.HorizontalAlignment <- HorizontalAlignment.Center
            glyph.VerticalAlignment <- VerticalAlignment.Center
            iconSlot.Child <- glyph)
        let caption =
            TextBlock(Text = entry.Label, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis)
        caption.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
        let left = Grid(VerticalAlignment = VerticalAlignment.Center)
        left.Bind(Grid.ColumnSpacingProperty, Dyn.res "Space.Sm") |> ignore
        left.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        left.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        Grid.SetColumn(iconSlot, 0)
        Grid.SetColumn(caption, 1)
        left.Children.Add iconSlot
        left.Children.Add caption
        let rightSlot = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right)
        rightSlot.Bind(Layoutable.MinWidthProperty, Dyn.res "Metric.MenuRightSlotMinWidth") |> ignore
        if entry.Selected then
            let mark = look.Check look.Accent
            mark.VerticalAlignment <- VerticalAlignment.Center
            mark.HorizontalAlignment <- HorizontalAlignment.Right
            rightSlot.Child <- mark
        elif not (String.IsNullOrWhiteSpace entry.Hint) then
            let hint = TextBlock(Text = entry.Hint, Foreground = look.Faint, VerticalAlignment = VerticalAlignment.Center)
            hint.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontCaption") |> ignore
            hint.Bind(Layoutable.MarginProperty, Dyn.res "Margin.MenuHint") |> ignore
            rightSlot.Child <- hint
        let row = Grid(VerticalAlignment = VerticalAlignment.Center)
        row.Bind(Grid.ColumnSpacingProperty, Dyn.res "Space.Sm") |> ignore
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(left, 0)
        Grid.SetColumn(rightSlot, 1)
        row.Children.Add left
        row.Children.Add rightSlot
        // 原生交互宿主：Border 配合原生样式类，不再手搓 TapPeer
        let host = Border(Cursor = handCursor, Focusable = true, Child = row)
        host.Bind(Decorator.PaddingProperty, Dyn.res "Padding.MenuItem") |> ignore
        host.Bind(Border.CornerRadiusProperty, Dyn.res "CornerRadius.RadiusXs") |> ignore
        host.MinHeight <- look.ItemMinHeight
        host.Classes.Add itemClass
        host.Classes.Add "surface"
        host.Classes.Add(if entry.Danger then "hover-danger" else "hover-plain")
        AutomationProperties.SetName(host, entry.Label)
        // 长文案被截断时补原生 tooltip。
        ToolTip.SetTip(host, box entry.Label)
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.MenuItem)
        let fire () = activate entry
        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && host.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                fire ())
        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                fire ())
        host :> Control

    /// 菜单方向键落点（在本文件内联）：Home/PageUp 到首、End/PageDown 到尾、Up/Down 首尾回绕。
    let nextIndex (key: Key) (current: int) (count: int) =
        if count <= 0 then None
        else
            match key with
            | Key.Home | Key.PageUp -> Some 0
            | Key.End | Key.PageDown -> Some(count - 1)
            | Key.Up -> Some(if current <= 0 then count - 1 else current - 1)
            | Key.Down -> Some(if current < 0 || current >= count - 1 then 0 else current + 1)
            | _ -> None

    /// Up/Down/Home/End/PageUp/PageDown 做 roving focus；几何仍完全交给 Avalonia。
    let private wireDirectionalNavigation (panel: StackPanel) =
        panel.KeyDown.Add(fun e ->
            let items =
                panel.Children
                |> Seq.choose (function :? Border as b when b.Classes.Contains itemClass && b.Focusable && b.IsEnabled -> Some b | _ -> None)
                |> Array.ofSeq
            let current = items |> Array.tryFindIndex _.IsFocused |> Option.defaultValue -1
            match nextIndex e.Key current items.Length with
            | Some next ->
                e.Handled <- true
                items[next].Focus(NavigationMethod.Directional) |> ignore
                items[next].BringIntoView()
            | None -> ())

    let private wrap (look: PopupMenuAppearance) (panel: StackPanel) : Control =
        panel.Bind(StackPanel.SpacingProperty, Dyn.res "Space.XXs") |> ignore
        wireDirectionalNavigation panel
        let scroller =
            ScrollViewer(
                Content = panel,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
        scroller.MaxHeight <- look.MaxHeight
        scroller


    let build (look: PopupMenuAppearance) (activate: PopupEntry -> unit) (entries: PopupEntry list) : Control option =
        if List.isEmpty entries then None
        else
            let panel = StackPanel(Orientation = Orientation.Vertical)
            for entry in entries do
                panel.Children.Add(renderEntry look activate entry)
            Some(wrap look panel)

    /// 带分组标题的菜单内容；空组跳过，全空时 None。
    let buildGrouped (look: PopupMenuAppearance) (activate: PopupEntry -> unit) (groups: (string * PopupEntry list) list) : Control option =
        let visible = groups |> List.filter (fun (_, items) -> not (List.isEmpty items))
        if List.isEmpty visible then None
        else
            let panel = StackPanel(Orientation = Orientation.Vertical)
            visible
            |> List.iteri (fun index (groupLabel, items) ->
                if index > 0 then
                    // 外观由主题的 hairline 类给（旧 Ui.hairline）。
                    let divider = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
                    divider.Classes.Add "hairline"
                    divider.Bind(Layoutable.MarginProperty, Dyn.res "Margin.MenuDivider") |> ignore
                    panel.Children.Add divider
                if not (String.IsNullOrWhiteSpace groupLabel) then
                    // 字号、字重、颜色、字距由主题的 section-label 类给（旧 Ui.sectionLabel）。
                    let header = TextBlock(Text = groupLabel)
                    header.Classes.Add "section-label"
                    header.Bind(Layoutable.MarginProperty, Dyn.res "Margin.MenuHeader") |> ignore
                    panel.Children.Add header
                for entry in items do
                    panel.Children.Add(renderEntry look activate entry))
            Some(wrap look panel)

/// 原生下拉选择器：基于 Button + MenuFlyout 构成。
/// 显示当前选项标签，外观经样式类走主题（surface select-button hover-bordered），不再逐属性订阅 appearance。
type SelectButton(look: PopupMenuAppearance, initialText: string) =
    let openRequested = Event<unit>()
    let caption = TextBlock(Text = initialText, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis)
    let chevron = look.Chevron look.Faint
    let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let button = Button(Content = row, Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center)
    let menuFlyout = MenuFlyout()
    let mutable lastText = initialText

    do
        chevron.VerticalAlignment <- VerticalAlignment.Center
        row.Children.Add caption
        row.Children.Add chevron
        caption.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontBody") |> ignore
        caption.Bind(Layoutable.MaxWidthProperty, Dyn.res "Metric.SelectCaptionMaxWidth") |> ignore
        row.Bind(StackPanel.SpacingProperty, Dyn.res "Space.Sm") |> ignore
        button.Classes.Add "surface"
        button.Classes.Add "select-button"
        button.Classes.Add "hover-bordered"
        button.Flyout <- menuFlyout
        button.AddHandler(InputElement.KeyDownEvent, (fun (_sender: obj) (e: KeyEventArgs) ->
            if button.IsEnabled && (e.Key = Key.Down || e.Key = Key.Up) then
                e.Handled <- true
                openRequested.Trigger()
                if not menuFlyout.IsOpen then menuFlyout.ShowAt button
        ), Interactivity.RoutingStrategies.Tunnel)
        button.Click.Add(fun _ -> openRequested.Trigger())
        ToolTip.SetTip(button, box initialText)
        AutomationProperties.SetName(button, initialText)

    member _.View: Button = button
    member _.Text = caption.Text
    member _.Flyout = menuFlyout

    /// 换显示文本；读屏名只在仍是自己上次写入的值时跟随。
    member _.ApplyText(text: string) =
        caption.Text <- text
        ToolTip.SetTip(button, box text)
        if AutomationProperties.GetName button = lastText then
            AutomationProperties.SetName(button, text)
        lastText <- text

    [<CLIEvent>] member _.OpenRequested = openRequested.Publish
