namespace Wanxiang2.SidebarSearch

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 可点的 Border（与旧 ActionBorder 同义）：读屏得到 Button 与 Invoke；Enter / Space / 左键松开都触发。
/// 样式按 Border 查（StyleKeyOverride），外观全靠主题类名。
type internal Tap(child: Control, name: string, classes: string list, action: unit -> unit) as this =
    inherit Border()

    do
        this.Child <- child
        this.Focusable <- true
        this.VerticalAlignment <- VerticalAlignment.Center
        this.Cursor <- new Cursor(StandardCursorType.Hand)
        for c in classes do this.Classes.Add c
        if not (String.IsNullOrEmpty name) then AutomationProperties.SetName(this, name)
        this.PointerReleased.Add(fun e ->
            if this.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())

    override _.StyleKeyOverride = typeof<Border>
    override _.OnCreateAutomationPeer() = TapPeer(this) :> AutomationPeer

    member internal _.Invoke() = if this.IsEnabled then action ()

    /// 换文字（读屏名同步）；只对文字按钮有效
    member _.SetText(value: string) =
        match this.Child with
        | :? TextBlock as t -> t.Text <- value
        | _ -> ()
        AutomationProperties.SetName(this, value)

    member _.Text =
        match this.Child with
        | :? TextBlock as t -> t.Text
        | _ -> ""

    /// 与旧 Ui.setEnabled 同义：不可用时也不可聚焦；透明度由主题的 :disabled 给
    member _.SetEnabled(enabled: bool) =
        this.IsEnabled <- enabled
        this.Focusable <- enabled

and internal TapPeer(owner: Tap) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() =
        let o = AutomationProperties.GetControlTypeOverride owner
        if o.HasValue then o.Value else AutomationControlType.Button
    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())

/// 按钮档（旧 Ui.button 的四档）
[<RequireQualifiedAccess>]
type internal Tone =
    | Primary
    | Secondary
    | Ghost
    | Danger

/// 标签语气（旧 Ui.TagTone）
[<RequireQualifiedAccess>]
type internal TagTone =
    | Neutral
    | Warning
    | Danger

/// 旧 Controls/Primitives.fs 中本库用到的原语，外观只取外观记录里的活实例与主题类名。
type internal Kit(look: SidebarSearchLook) =
    let live (o: IObservable<'T>) (f: 'T -> unit) = o.Subscribe(fun v -> f v) |> ignore

    member _.Live(o: IObservable<'T>, f: 'T -> unit) = live o f

    member _.Classes(tone: Tone) =
        match tone with
        | Tone.Primary -> [ "surface"; "button-primary"; "hover-accent" ]
        | Tone.Secondary -> [ "surface"; "button-secondary"; "hover-raised" ]
        | Tone.Ghost -> [ "surface"; "button-ghost"; "hover-plain" ]
        | Tone.Danger -> [ "surface"; "button-danger"; "hover-danger" ]

    member this.Button(tone: Tone, text: string, action: unit -> unit) : Tap =
        let t = TextBlock(Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
        Tap(t, text, this.Classes tone, action)

    /// 图标按钮：图标由外观记录的 Glyph 槽给出（名字见 AGENTS.md）
    member _.IconButton(glyph: string, tip: string, action: unit -> unit) : Tap =
        let g = look.Glyph glyph look.Muted
        g.HorizontalAlignment <- HorizontalAlignment.Center
        g.VerticalAlignment <- VerticalAlignment.Center
        let host = Tap(g, tip, [ "surface"; "icon-button"; "hover-plain" ], action)
        host.HorizontalAlignment <- HorizontalAlignment.Center
        if not (String.IsNullOrWhiteSpace tip) then ToolTip.SetTip(host, tip)
        host

    member _.Text(text: string, size: IObservable<float>, brush: IBrush) =
        let t = TextBlock(Text = text, Foreground = brush)
        live size (fun v -> t.FontSize <- v)
        t

    member this.Label(text: string) =
        let t = this.Text(text, look.BodyFontSize, look.Text)
        t.VerticalAlignment <- VerticalAlignment.Center
        t

    member this.Caption(text: string) =
        let t = this.Text(text, look.CaptionFontSize, look.Muted)
        t.TextWrapping <- TextWrapping.Wrap
        live look.CaptionLineHeight (fun v -> t.LineHeight <- v)
        t

    member this.Heading(text: string) =
        let t = this.Text(text, look.HeadingFontSize, look.Text)
        t.FontWeight <- FontWeight.Medium
        t

    member this.Title(text: string) =
        let t = this.Text(text, look.TitleFontSize, look.Text)
        t.FontWeight <- FontWeight.Medium
        t

    member _.SectionLabel(text: string) =
        let t = TextBlock(Text = text)
        t.Classes.Add "section-label"
        t

    member this.FieldLabel(text: string) =
        let t = this.Text(text, look.MicroFontSize, look.Faint)
        t.FontWeight <- FontWeight.Medium
        live look.LabelTracking (fun v -> t.LetterSpacing <- v)
        live (Kit.Pair(look.FieldInsetX, look.SpaceSm)) (fun (x, b) -> t.Margin <- Thickness(x, 0.0, 0.0, b))
        t

    member _.VStack(spacing: IObservable<float>, children: Control seq) =
        let p = StackPanel(Orientation = Orientation.Vertical)
        live spacing (fun v -> p.Spacing <- v)
        for c in children do p.Children.Add c
        p

    member _.HStack(spacing: IObservable<float>, children: Control seq) =
        let p = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        live spacing (fun v -> p.Spacing <- v)
        for c in children do p.Children.Add c
        p

    /// 分组卡（主题类 grouping-card）；内边距与圆角取活值
    member _.Card(padX: IObservable<float>, padY: IObservable<float>, radius: IObservable<float>, child: Control) =
        let b = Border(Child = child)
        b.Classes.Add "grouping-card"
        live (Kit.Pair(padX, padY)) (fun (x, y) -> b.Padding <- Thickness(x, y))
        live radius (fun r -> b.CornerRadius <- CornerRadius r)
        b

    member _.Hairline() =
        let b = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
        b.Classes.Add "hairline"
        b

    member _.Tag(tone: TagTone, text: string) =
        let b = Border(VerticalAlignment = VerticalAlignment.Center, Child = TextBlock(Text = text, VerticalAlignment = VerticalAlignment.Center))
        b.Classes.Add "tag"
        match tone with
        | TagTone.Neutral -> ()
        | TagTone.Warning -> b.Classes.Add "tag-warning"
        | TagTone.Danger -> b.Classes.Add "tag-danger"
        b

    member _.StatusDot(size: IObservable<float>, brush: IBrush) =
        let e = Ellipse(Fill = brush, VerticalAlignment = VerticalAlignment.Center)
        live size (fun v -> e.Width <- v; e.Height <- v)
        e

    /// 输入壳 + 输入框（旧 Ui.textField）：聚焦时描边换强调色，校验出错（invalid 类）时让给错误描边
    member _.TextField(placeholder: string) : Border * TextBox =
        let box =
            TextBox(
                PlaceholderText = placeholder,
                BorderThickness = Thickness 0.0,
                Background = Brushes.Transparent,
                Foreground = look.Text,
                CaretBrush = look.Accent,
                Padding = Thickness 0.0,
                VerticalContentAlignment = VerticalAlignment.Center)
        live look.BodyFontSize (fun v -> box.FontSize <- v)
        live look.TextFieldTextMinHeight (fun v -> box.MinHeight <- v)
        let shell = Border(Background = look.FieldSurface, BorderBrush = look.FieldEdge, Child = box)
        live look.BorderWidth (fun v -> shell.BorderThickness <- Thickness v)
        live look.RadiusLg (fun v -> shell.CornerRadius <- CornerRadius v)
        live (Kit.Pair(look.SpaceXl, look.TextFieldPaddingY)) (fun (x, y) -> shell.Padding <- Thickness(x, y))
        for c in [ "surface"; "hover-raised"; "focus-soft" ] do shell.Classes.Add c
        let invalid () = shell.Classes.Contains "invalid"
        box.GotFocus.Add(fun _ -> if not (invalid ()) then shell.BorderBrush <- look.Accent)
        box.LostFocus.Add(fun _ -> if not (invalid ()) then shell.BorderBrush <- look.FieldEdge)
        shell, box

    member this.TextArea(placeholder: string, minHeight: IObservable<float>) : Border * TextBox =
        let shell, box = this.TextField placeholder
        box.AcceptsReturn <- true
        box.TextWrapping <- TextWrapping.Wrap
        box.VerticalContentAlignment <- VerticalAlignment.Top
        live minHeight (fun v -> box.MinHeight <- v)
        shell, box

    /// 字段组：标签 → 控件 → 校验行 → 说明；校验行可见时说明让位（旧 Ui.fieldGroup）
    member this.FieldGroup(labelText: string, hint: string, control: Control, validation: Control option) : Control =
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        column.Children.Add(this.FieldLabel labelText)
        column.Children.Add control
        validation |> Option.iter column.Children.Add
        if not (String.IsNullOrWhiteSpace hint) then
            let note = this.Caption hint
            live (Kit.Pair(look.FieldInsetX, look.SpaceXs)) (fun (x, t) -> note.Margin <- Thickness(x, t, 0.0, 0.0))
            match validation with
            | Some error ->
                note.IsVisible <- not error.IsVisible
                error.PropertyChanged.Add(fun args ->
                    if args.Property = Visual.IsVisibleProperty then note.IsVisible <- not error.IsVisible)
            | None -> ()
            column.Children.Add note
        column :> Control

    /// 开关（旧 Ui.toggle）：返回轨道、读、写、翻转
    member _.Toggle(initial: bool, onChanged: bool -> unit) =
        let mutable value = initial
        let knob = Border(VerticalAlignment = VerticalAlignment.Center)
        knob.Classes.Add "toggle-knob"
        let track = Border(Focusable = true, VerticalAlignment = VerticalAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand), Child = knob)
        for c in [ "surface"; "toggle-track" ] do track.Classes.Add c
        AutomationProperties.SetControlTypeOverride(track, Nullable AutomationControlType.CheckBox)
        let render () =
            if value then track.Classes.Add "on" else track.Classes.Remove "on" |> ignore
            knob.HorizontalAlignment <- if value then HorizontalAlignment.Right else HorizontalAlignment.Left
            AutomationProperties.SetItemStatus(track, (if value then "开启" else "关闭"))
        let flip () =
            value <- not value
            render ()
            onChanged value
        track.PointerReleased.Add(fun e ->
            if e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                flip ())
        track.KeyDown.Add(fun e ->
            if e.Key = Key.Enter || e.Key = Key.Space then
                e.Handled <- true
                flip ())
        render ()
        track, (fun () -> value), (fun v -> value <- v; render ()), flip

    /// 设置行内容：左文（标签 + 说明）右控件
    member this.RowContent(title: string, hint: string, right: Control) =
        let column = this.VStack(look.SpaceXs, [ this.Label title :> Control; this.Caption hint :> Control ])
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(right, Dock.Right)
        dock.Children.Add right
        dock.Children.Add column
        dock

    /// 自定义右侧控件的设置行外壳（旧 Ui.settingsRowShell）
    member this.RowShell(title: string, hint: string, right: Control) =
        let b = Border(Child = this.RowContent(title, hint, right))
        live look.SpaceMd (fun v -> b.Padding <- Thickness v)
        live look.SettingsRowMinHeight (fun v -> b.MinHeight <- v)
        b

    /// 设置行开关（旧 Ui.switchRow）：一行一个 Tab 停靠点，行容器代理 Enter/Space，读屏是 CheckBox + 开启/关闭
    member this.SwitchRow(title: string, hint: string, initial: bool, onChanged: bool -> unit) =
        let track, read, write, flip = this.Toggle(initial, onChanged)
        track.Focusable <- false
        AutomationProperties.SetName(track, title)
        AutomationProperties.SetHelpText(track, hint)
        let dock = this.RowContent(title, hint, track)
        let mutable row: Tap = Unchecked.defaultof<_>
        let status () = AutomationProperties.SetItemStatus(row, (if read () then "开启" else "关闭"))
        row <- Tap(dock, title, [ "surface"; "hover-plain" ], (fun () -> flip (); status ()))
        row.Background <- Brushes.Transparent
        live look.SpaceMd (fun v -> row.Padding <- Thickness v)
        live look.RadiusMd (fun v -> row.CornerRadius <- CornerRadius v)
        live look.SettingsRowMinHeight (fun v -> row.MinHeight <- v)
        row.VerticalAlignment <- VerticalAlignment.Stretch
        AutomationProperties.SetHelpText(row, hint)
        AutomationProperties.SetControlTypeOverride(row, Nullable AutomationControlType.CheckBox)
        status ()
        row :> Control, read, (fun v -> write v; status ())

    /// 两个活值合成一个
    static member Pair(a: IObservable<float>, b: IObservable<float>) : IObservable<float * float> =
        { new IObservable<float * float> with
            member _.Subscribe(o) =
                let mutable x = None
                let mutable y = None
                let push () =
                    match x, y with
                    | Some x, Some y -> o.OnNext((x, y))
                    | _ -> ()
                let d1 = a.Subscribe(fun v -> x <- Some v; push ())
                let d2 = b.Subscribe(fun v -> y <- Some v; push ())
                { new IDisposable with member _.Dispose() = d1.Dispose(); d2.Dispose() } }
