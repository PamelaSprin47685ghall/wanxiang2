namespace Wanxiang2.ActionStrip

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

[<RequireQualifiedAccess>]
type StripGlyph =
    | Copy
    | Check
    | Pencil
    | Refresh
    | Trash

/// 闲置时的显示档
[<RequireQualifiedAccess>]
type StripRest =
    /// 完全隐去（仍占位、可命中、可聚焦，只改不透明度）
    | Hidden
    /// 淡显（最新一条助手回复用作可发现锚点）
    | Subtle

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 消息脚注里的操作条（旧 MessageCard.actionButtons）：按钮随文档流排布，整行恒定占一个命中区高，
/// 流式期间撤掉按钮也不缩；显隐只改不透明度，不动几何。
type ActionStrip(rest: StripRest) =
    let fade =
        DoubleTransition(Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds 120.0)

    let row =
        StackPanel(
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4.0,
            MinHeight = 30.0
        )

    let mutable shown = false
    let mutable subtle = 0.55

    let apply () =
        row.Opacity <-
            if shown then
                1.0
            else
                match rest with
                | StripRest.Hidden -> 0.0
                | StripRest.Subtle -> subtle

    do
        let transitions = Transitions()
        transitions.Add fade
        row.Transitions <- transitions
        apply ()

    member _.Host: Control = row
    member _.Shown = shown

    /// 悬停、聚焦或触屏选中时显示，否则回到闲置档
    member _.ApplyShown(value: bool) =
        shown <- value
        apply ()

    static member GlyphControl(glyph: StripGlyph, ?brush: IBrush) : Control =
        let data =
            match glyph with
            | StripGlyph.Copy ->
                "M 4 2 L 11 2 A 2 2 0 0 1 13 4 L 13 11 A 2 2 0 0 1 11 13 L 4 13 A 2 2 0 0 1 2 11 L 2 4 A 2 2 0 0 1 4 2 Z M 5 5 L 14 5 L 14 14 L 5 14"
            | StripGlyph.Check -> "M 3 8 L 6 11 L 13 4"
            | StripGlyph.Pencil -> "M 11 2 L 14 5 L 5 14 L 2 14 L 2 11 Z"
            | StripGlyph.Refresh -> "M 2 8 A 6 6 0 1 1 5 13 L 2 10"
            | StripGlyph.Trash -> "M 3 4 L 13 4 M 5 4 L 5 13 L 11 13 L 11 4 M 6 4 L 6 2 L 10 2 L 10 4"

        let p =
            Path(
                Data = Geometry.Parse data,
                StrokeThickness = 1.3,
                Fill = Brushes.Transparent,
                Tag = glyph,
                Width = 16.0,
                Height = 16.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        match brush with
        | Some b -> p.Stroke <- b
        | None -> p.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore

        p :> Control

    /// 换按钮的图标（复制确认时换成对勾）
    member _.ApplyIcon(button: Button, glyph: StripGlyph, ?brush: IBrush) =
        let g = ActionStrip.GlyphControl(glyph, ?brush = brush)
        button.Content <- g

    /// 加一个图标按钮：tip 是悬停提示，name / help 是读屏名与说明
    member this.Add(glyph: StripGlyph, tip: string, name: string, help: string, action: unit -> unit) : Button =
        let button =
            Button(
                Focusable = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = Thickness 0.0,
                Padding = Thickness 0.0,
                Width = 30.0,
                Height = 30.0,
                MinWidth = 30.0,
                MinHeight = 30.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        button.Classes.Add "icon-button"
        button.Classes.Add "ghost"
        this.ApplyIcon(button, glyph)
        ToolTip.SetTip(button, tip)
        AutomationProperties.SetName(button, name)
        AutomationProperties.SetHelpText(button, help)
        button.Click.Add(fun _ -> action ())
        row.Children.Add button
        button

    /// 已加的按钮
    member _.Buttons = row.Children |> Seq.cast<Button> |> List.ofSeq

    /// 单入口：从 ActionStripVS 渲染
    member this.Render(vs: ActionStripVS, onAction: string -> unit) =
        row.Children.Clear()

        for item in vs.Items do
            let glyph =
                match item.Glyph.ToLowerInvariant() with
                | "copy" -> StripGlyph.Copy
                | "check" -> StripGlyph.Check
                | "pencil"
                | "edit" -> StripGlyph.Pencil
                | "refresh"
                | "regenerate" -> StripGlyph.Refresh
                | "trash"
                | "delete" -> StripGlyph.Trash
                | _ -> StripGlyph.Copy

            let b = this.Add(glyph, item.Tip, item.Name, item.Help, fun () -> onAction item.Id)
            b.IsEnabled <- item.Enabled

        this.ApplyShown vs.Visible

module ActionStrip =
    /// 复制按钮的读屏名带角色与序号，同屏多个「复制」不撞名
    let copyName (isUser: bool) (index: int option) =
        let role = if isUser then "用户" else "助手"

        match index with
        | Some i when i >= 0 -> sprintf "复制%s消息 %d" role (i + 1)
        | _ -> sprintf "复制%s消息" role
