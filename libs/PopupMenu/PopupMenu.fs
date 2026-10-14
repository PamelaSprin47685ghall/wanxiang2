namespace Wanxiang2.PopupMenu

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

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

/// 菜单与下拉选择器的外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type PopupMenuLook =
    { Text: IBrush
      Muted: IBrush
      Faint: IBrush
      Danger: IBrush
      Accent: IBrush
      /// 打勾图标（插槽）
      Check: IBrush -> Control
      /// 下拉箭头图标（插槽）
      Chevron: IBrush -> Control
      FontSize: IObservable<float>
      HintFontSize: IObservable<float>
      IconSlotWidth: IObservable<float>
      IconHeight: IObservable<float>
      RightSlotMinWidth: IObservable<float>
      HintMargin: IObservable<Thickness>
      ColumnSpacing: IObservable<float>
      ItemPadding: IObservable<Thickness>
      ItemRadius: IObservable<CornerRadius>
      ItemMinHeight: IObservable<float>
      ItemSpacing: IObservable<float>
      MaxHeight: IObservable<float>
      DividerMargin: IObservable<Thickness>
      HeaderMargin: IObservable<Thickness>
      SelectPadding: IObservable<Thickness>
      SelectRadius: IObservable<CornerRadius>
      SelectEdgeWidth: IObservable<Thickness>
      SelectCaptionMaxWidth: IObservable<float> }

/// 浮层菜单内容（迁自旧 Controls/Menu.fs）。只造内容；放进浮层、关浮层由组合者做：
/// 激活时调用 `activate`，组合者先关浮层再执行条目动作。
module PopupMenu =

    let private handCursor = new Cursor(StandardCursorType.Hand)

    let private live (source: IObservable<'T>) (apply: 'T -> unit) = source.Subscribe apply |> ignore

    /// Enter / Space / 左键松开都激活。
    let private onInvoke (host: InvokeBorder) (action: unit -> unit) =
        host.SetAction action
        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && host.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())

    /// 菜单项宿主上的样式类（只挂类名，样式由主题提供）。
    let itemClass = "menu-item"

    let private renderEntry (look: PopupMenuLook) (activate: PopupEntry -> unit) (entry: PopupEntry) : Control =
        let foreground = if entry.Danger then look.Danger else look.Text
        let iconSlot = Border(VerticalAlignment = VerticalAlignment.Center)
        live look.IconSlotWidth (fun v -> iconSlot.Width <- v)
        live look.IconHeight (fun v -> iconSlot.Height <- v)
        entry.Icon
        |> Option.iter (fun icon ->
            let glyph = icon (if entry.Danger then look.Danger else look.Muted)
            glyph.HorizontalAlignment <- HorizontalAlignment.Center
            glyph.VerticalAlignment <- VerticalAlignment.Center
            iconSlot.Child <- glyph)
        let caption =
            TextBlock(Text = entry.Label, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis)
        live look.FontSize (fun v -> caption.FontSize <- v)
        let left = Grid(VerticalAlignment = VerticalAlignment.Center)
        live look.ColumnSpacing (fun v -> left.ColumnSpacing <- v)
        left.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        left.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        Grid.SetColumn(iconSlot, 0)
        Grid.SetColumn(caption, 1)
        left.Children.Add iconSlot
        left.Children.Add caption
        let rightSlot = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right)
        live look.RightSlotMinWidth (fun v -> rightSlot.MinWidth <- v)
        if entry.Selected then
            let mark = look.Check look.Accent
            mark.VerticalAlignment <- VerticalAlignment.Center
            mark.HorizontalAlignment <- HorizontalAlignment.Right
            rightSlot.Child <- mark
        elif not (String.IsNullOrWhiteSpace entry.Hint) then
            let hint = TextBlock(Text = entry.Hint, Foreground = look.Faint, VerticalAlignment = VerticalAlignment.Center)
            live look.HintFontSize (fun v -> hint.FontSize <- v)
            live look.HintMargin (fun v -> hint.Margin <- v)
            rightSlot.Child <- hint
        let row = Grid(VerticalAlignment = VerticalAlignment.Center)
        live look.ColumnSpacing (fun v -> row.ColumnSpacing <- v)
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        Grid.SetColumn(left, 0)
        Grid.SetColumn(rightSlot, 1)
        row.Children.Add left
        row.Children.Add rightSlot
        let host = InvokeBorder(Cursor = handCursor, Focusable = true, Child = row)
        live look.ItemPadding (fun v -> host.Padding <- v)
        live look.ItemRadius (fun v -> host.CornerRadius <- v)
        live look.ItemMinHeight (fun v -> host.MinHeight <- v)
        host.Classes.Add itemClass
        host.Classes.Add "surface"
        host.Classes.Add(if entry.Danger then "hover-danger" else "hover-plain")
        AutomationProperties.SetName(host, entry.Label)
        // 长文案被截断时补原生 tooltip。
        ToolTip.SetTip(host, entry.Label)
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.MenuItem)
        onInvoke host (fun () -> activate entry)
        host :> Control

    /// 与 RovingIndex.menu 相同的落点：Home/PageUp 到首、End/PageDown 到尾、Up/Down 首尾回绕。
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

    let private wrap (look: PopupMenuLook) (panel: StackPanel) : Control =
        live look.ItemSpacing (fun v -> panel.Spacing <- v)
        wireDirectionalNavigation panel
        let scroller =
            ScrollViewer(
                Content = panel,
                HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto)
        live look.MaxHeight (fun v -> scroller.MaxHeight <- v)
        scroller

    /// 菜单内容；`entries` 为空时 None（旧代码此时不打开）。
    let build (look: PopupMenuLook) (activate: PopupEntry -> unit) (entries: PopupEntry list) : Control option =
        if List.isEmpty entries then None
        else
            let panel = StackPanel(Orientation = Orientation.Vertical)
            for entry in entries do
                panel.Children.Add(renderEntry look activate entry)
            Some(wrap look panel)

    /// 带分组标题的菜单内容；空组跳过，全空时 None。
    let buildGrouped (look: PopupMenuLook) (activate: PopupEntry -> unit) (groups: (string * PopupEntry list) list) : Control option =
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
                    live look.DividerMargin (fun v -> divider.Margin <- v)
                    panel.Children.Add divider
                if not (String.IsNullOrWhiteSpace groupLabel) then
                    // 字号、字重、颜色、字距由主题的 section-label 类给（旧 Ui.sectionLabel）。
                    let header = TextBlock(Text = groupLabel)
                    header.Classes.Add "section-label"
                    live look.HeaderMargin (fun v -> header.Margin <- v)
                    panel.Children.Add header
                for entry in items do
                    panel.Children.Add(renderEntry look activate entry))
            Some(wrap look panel)

/// 下拉选择器按钮：显示当前值，点击或 Down/Up 发 `OpenRequested`，组合者据此弹出分组菜单。
type SelectButton(look: PopupMenuLook, initialText: string) =
    let openRequested = Event<unit>()
    let caption = TextBlock(Text = initialText, Foreground = look.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis)
    let chevron = look.Chevron look.Faint
    let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let host = InvokeBorder(Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, VerticalAlignment = VerticalAlignment.Center, Child = row)
    // 上一次由本按钮写入的显示文本：调用方另起了名就与它不同，此后不再覆盖。
    let mutable lastText = initialText

    do
        chevron.VerticalAlignment <- VerticalAlignment.Center
        row.Children.Add caption
        row.Children.Add chevron
        look.FontSize.Subscribe(fun v -> caption.FontSize <- v) |> ignore
        look.SelectCaptionMaxWidth.Subscribe(fun v -> caption.MaxWidth <- v) |> ignore
        look.ColumnSpacing.Subscribe(fun v -> row.Spacing <- v) |> ignore
        look.SelectEdgeWidth.Subscribe(fun v -> host.BorderThickness <- v) |> ignore
        look.SelectRadius.Subscribe(fun v -> host.CornerRadius <- v) |> ignore
        look.SelectPadding.Subscribe(fun v -> host.Padding <- v) |> ignore
        host.Classes.Add "surface"
        host.Classes.Add "select-button"
        host.Classes.Add "hover-bordered"
        AutomationProperties.SetControlTypeOverride(host, Nullable AutomationControlType.ComboBox)
        host.SetAction openRequested.Trigger
        host.PointerReleased.Add(fun e ->
            if host.IsEnabled && host.IsHitTestVisible && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                openRequested.Trigger())
        host.KeyDown.Add(fun e ->
            if host.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                openRequested.Trigger()
            // Down / Up 都能展开，与菜单内 Up/Down 对称。
            elif e.Key = Key.Down || e.Key = Key.Up then
                e.Handled <- true
                openRequested.Trigger())
        ToolTip.SetTip(host, initialText)
        AutomationProperties.SetName(host, initialText)

    member _.View: Border = host :> Border
    member _.Text = caption.Text

    /// 换显示文本；读屏名只在仍是自己上次写入的值时跟随。
    member _.SetText(text: string) =
        caption.Text <- text
        ToolTip.SetTip(host, text)
        if AutomationProperties.GetName host = lastText then
            AutomationProperties.SetName(host, text)
        lastText <- text

    [<CLIEvent>] member _.OpenRequested = openRequested.Publish
