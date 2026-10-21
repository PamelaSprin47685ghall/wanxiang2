module Dump

open System
open System.Globalization
open System.Text
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Layout
open Avalonia.LogicalTree
open Avalonia.Media

/// 金样两边共用的控件树转写：只看解析后的属性值（本地值或样式给的都一样），不看类型实现细节。
let private n (v: float) =
    if Double.IsNaN v then
        "nan"
    elif Double.IsPositiveInfinity v then
        "inf"
    else
        Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture)

let private th (t: Thickness) =
    sprintf "%s,%s,%s,%s" (n t.Left) (n t.Top) (n t.Right) (n t.Bottom)

let private cr (c: CornerRadius) =
    sprintf "%s,%s,%s,%s" (n c.TopLeft) (n c.TopRight) (n c.BottomRight) (n c.BottomLeft)

let private brush (b: IBrush) =
    match b with
    | null -> "null"
    | :? ISolidColorBrush as s -> s.Color.ToString()
    | other -> other.GetType().Name

let private family (f: FontFamily) =
    if isNull f then
        "null"
    else
        f.FamilyNames |> Seq.map string |> String.concat "|"

/// 类型名：框架自己的类型照写；项目自定义类型写成最近的框架基类加星号
let private kind (o: obj) =
    let rec up (t: Type) =
        if isNull t then
            "?"
        elif t.Namespace <> null && t.Namespace.StartsWith "Avalonia" then
            t.Name
        else
            up t.BaseType

    let t = o.GetType()

    if t.Namespace <> null && t.Namespace.StartsWith "Avalonia" then
        t.Name
    else
        up t + "*"

let private decorations (d: TextDecorationCollection) =
    if isNull d || d.Count = 0 then
        ""
    else
        " deco=" + (d |> Seq.map (fun x -> string x.Location) |> String.concat "+")

let rec private control (sb: StringBuilder) (depth: int) (c: Control) =
    let pad = String(' ', depth * 2)

    let line (s: string) =
        sb.Append(pad).Append(s).Append('\n') |> ignore

    let attrs = ResizeArray<string>()
    let add (s: string) = attrs.Add s
    let b = c.Bounds
    add (sprintf "@%s,%s %sx%s" (n b.X) (n b.Y) (n b.Width) (n b.Height))

    if c.Margin <> Thickness() then
        add ("m=" + th c.Margin)

    if not c.IsVisible then
        add "hidden"

    if c.Opacity <> 1.0 then
        add ("op=" + n c.Opacity)

    if c.HorizontalAlignment <> HorizontalAlignment.Stretch then
        add ("ha=" + string c.HorizontalAlignment)

    if c.VerticalAlignment <> VerticalAlignment.Stretch then
        add ("va=" + string c.VerticalAlignment)

    if not (Double.IsNaN c.Width) then
        add ("w=" + n c.Width)

    if not (Double.IsNaN c.Height) then
        add ("h=" + n c.Height)

    if c.MinWidth <> 0.0 then
        add ("minw=" + n c.MinWidth)

    if c.MinHeight <> 0.0 then
        add ("minh=" + n c.MinHeight)

    if not (Double.IsPositiveInfinity c.MaxHeight) then
        add ("maxh=" + n c.MaxHeight)

    if c.Focusable then
        add "focusable"

    if c.ClipToBounds then
        add "clip"

    if not (isNull c.Cursor) then
        add "cursor"

    let name = AutomationProperties.GetName c

    if not (String.IsNullOrEmpty name) then
        add ("name=" + name)

    let help = AutomationProperties.GetHelpText c

    if not (String.IsNullOrEmpty help) then
        add ("help=" + help)

    let status = AutomationProperties.GetItemStatus c

    if not (String.IsNullOrEmpty status) then
        add ("status=" + status)

    let ty = AutomationProperties.GetControlTypeOverride c

    if ty.HasValue then
        add ("role=" + string ty.Value)

    if AutomationProperties.GetAccessibilityView c <> AccessibilityView.Default then
        add ("view=" + string (AutomationProperties.GetAccessibilityView c))

    match ToolTip.GetTip c with
    | null -> ()
    | tip -> add ("tip=" + string tip)

    if c.Parent :? DockPanel then
        add ("dock=" + string (DockPanel.GetDock c))

    if c.Parent :? Grid then
        add (sprintf "cell=%d,%d" (Grid.GetRow c) (Grid.GetColumn c))

    match c with
    | :? Border as x ->
        add ("bg=" + brush x.Background)

        if not (isNull x.BorderBrush) then
            add ("bd=" + brush x.BorderBrush)

        if x.BorderThickness <> Thickness() then
            add ("bt=" + th x.BorderThickness)

        if x.CornerRadius <> CornerRadius() then
            add ("cr=" + cr x.CornerRadius)

        if x.Padding <> Thickness() then
            add ("pad=" + th x.Padding)
    | :? TextBlock as t ->
        add (
            sprintf
                "size=%s weight=%s fg=%s lh=%s wrap=%s"
                (n t.FontSize)
                (string t.FontWeight)
                (brush t.Foreground)
                (n t.LineHeight)
                (string t.TextWrapping)
        )

        add ("font=" + family t.FontFamily)

        if t.FontStyle <> FontStyle.Normal then
            add ("style=" + string t.FontStyle)

        if t.TextAlignment <> TextAlignment.Left then
            add ("align=" + string t.TextAlignment)

        if t.LetterSpacing <> 0.0 then
            add ("track=" + n t.LetterSpacing)

        if t.Padding <> Thickness() then
            add ("pad=" + th t.Padding)

        add (decorations t.TextDecorations)

        match t with
        | :? SelectableTextBlock as s ->
            add (
                "sel="
                + brush s.SelectionBrush
                + (if isNull s.ContextFlyout then " noflyout" else "")
            )
        | _ -> ()
    | :? StackPanel as s -> add (sprintf "orient=%s spacing=%s" (string s.Orientation) (n s.Spacing))
    | :? WrapPanel as w -> add ("orient=" + string w.Orientation)
    | :? DockPanel as d -> add ("fill=" + string d.LastChildFill)
    | :? Grid as g ->
        add (
            "cols="
            + (g.ColumnDefinitions
               |> Seq.map (fun d -> sprintf "%s/%s" (string d.Width) (n d.MinWidth))
               |> String.concat ";")
        )

        add (
            "rows="
            + (g.RowDefinitions |> Seq.map (fun d -> string d.Height) |> String.concat ";")
        )
    | :? ScrollViewer as s ->
        add (sprintf "hbar=%s vbar=%s" (string s.HorizontalScrollBarVisibility) (string s.VerticalScrollBarVisibility))
    | :? Image as i ->
        match i.Source with
        | :? DrawingImage as d ->
            match d.Drawing with
            | :? GeometryDrawing as g -> add (sprintf "glyph=%s fill=%s" (string g.Geometry.Bounds) (brush g.Brush))
            | :? DrawingGroup as grp ->
                let geos =
                    grp.Children
                    |> Seq.choose (function
                        | :? GeometryDrawing as g ->
                            Some(
                                sprintf
                                    "%s:%s"
                                    (string g.Geometry.Bounds)
                                    (brush (
                                        if isNull g.Brush then
                                            (if isNull g.Pen then null else g.Pen.Brush)
                                        else
                                            g.Brush
                                    ))
                            )
                        | _ -> None)

                add ("glyph=" + String.concat ";" geos)
            | other -> add ("drawing=" + (if isNull other then "null" else other.GetType().Name))
        | other -> add ("source=" + (if isNull other then "null" else other.GetType().Name))
    | _ -> ()

    let baseline = c.GetType().GetProperty("Baseline")

    if not (isNull baseline) && baseline.PropertyType = typeof<float> then
        add ("baseline=" + n (baseline.GetValue c :?> float))

    line (kind c + " " + (attrs |> Seq.filter (fun s -> s <> "") |> String.concat " "))

    match c with
    | :? TextBlock as t when not (isNull t.Inlines) && t.Inlines.Count > 0 ->
        for i in t.Inlines do
            inlineOf sb (depth + 1) i
    | :? TextBlock as t -> sb.Append(pad).Append("  text: ").Append(t.Text).Append('\n') |> ignore
    | _ ->
        for child in c.GetLogicalChildren() do
            match child with
            | :? Control as cc -> control sb (depth + 1) cc
            | _ -> ()

and private inlineOf (sb: StringBuilder) depth (i: Inline) =
    let pad = String(' ', depth * 2)

    match i with
    | :? Run as r ->
        let extras = ResizeArray<string>()

        if r.IsSet TextElement.ForegroundProperty then
            extras.Add("fg=" + brush r.Foreground)

        if r.IsSet TextElement.BackgroundProperty then
            extras.Add("bg=" + brush r.Background)

        if r.IsSet TextElement.FontFamilyProperty then
            extras.Add("font=" + family r.FontFamily)

        if r.IsSet TextElement.FontSizeProperty then
            extras.Add("size=" + n r.FontSize)

        if r.FontStyle <> FontStyle.Normal then
            extras.Add("style=" + string r.FontStyle)

        if r.BaselineAlignment <> BaselineAlignment.Baseline then
            extras.Add("base=" + string r.BaselineAlignment)

        let d = decorations r.TextDecorations

        if d <> "" then
            extras.Add(d.Trim())

        sb
            .Append(pad)
            .Append("run ")
            .Append(Text.Json.JsonSerializer.Serialize r.Text)
            .Append(' ')
            .Append(String.Join(" ", extras))
            .Append('\n')
        |> ignore
    | :? LineBreak -> sb.Append(pad).Append("break\n") |> ignore
    | :? InlineUIContainer as u ->
        sb.Append(pad).Append("container base=").Append(string u.BaselineAlignment).Append('\n')
        |> ignore

        control sb (depth + 1) u.Child
    | other -> sb.Append(pad).Append(other.GetType().Name).Append('\n') |> ignore

/// 放进固定宽度的窗口排版后转写。窗口字体取内嵌正文字体：不依赖机器上装了什么系统字体
let tree (body: FontFamily) (root: Control) : string =
    let win = Window(Width = 600.0, Height = 2000.0, Content = root, FontFamily = body)
    win.Show()
    win.UpdateLayout()
    root.InvalidateMeasure()
    win.UpdateLayout()
    let sb = StringBuilder()
    control sb 0 root
    win.Close()
    sb.ToString()
