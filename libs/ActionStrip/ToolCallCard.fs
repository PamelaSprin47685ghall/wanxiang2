namespace Wanxiang2.ToolCallCard

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 卡片的状态色调（由使用方从工具调用状态折算）
[<RequireQualifiedAccess>]
type ToolTone =
    | Running
    | Awaiting
    | Success
    | Failed
    | Unknown

[<RequireQualifiedAccess>]
type ToolGlyph =
    | Wrench
    | Alert
    | Info
    | Check
    | Chevron
    | Spinner
    | SpinnerCompact

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 展开后的一节（参数或结果）
type ToolSection =
    {
        Label: string
        Raw: string
        Pretty: string
        IsError: bool
    }

module ToolCallCard =
    let sections (pretty: string -> string) (argumentsJson: string) (result: string option) (tone: ToolTone) =
        [
            if not (String.IsNullOrWhiteSpace argumentsJson) then
                {
                    Label = "调用参数"
                    Raw = argumentsJson
                    Pretty = pretty argumentsJson
                    IsError = false
                }
            match result with
            | Some r when not (String.IsNullOrWhiteSpace r) ->
                let failed = tone = ToolTone.Failed

                {
                    Label = (if failed then "执行结果 (异常)" else "执行结果")
                    Raw = r
                    Pretty = pretty r
                    IsError = failed
                }
            | _ ->
                if tone = ToolTone.Unknown then
                    let t = "未收到工具执行结果（生成已结束）"

                    {
                        Label = "执行结果"
                        Raw = t
                        Pretty = pretty t
                        IsError = false
                    }
        ]

    let rawPayload (argumentsJson: string) (result: string option) (tone: ToolTone) =
        [
            if not (String.IsNullOrWhiteSpace argumentsJson) then
                sprintf "// 参数 (Arguments)\n%s" argumentsJson
            match result with
            | Some r when not (String.IsNullOrWhiteSpace r) -> sprintf "// 结果 (Result)\n%s" r
            | _ ->
                if tone = ToolTone.Unknown then
                    "// 结果 (Result)\n未收到工具执行结果（生成已结束）"
        ]
        |> String.concat "\n\n"

/// 一张工具卡的显示数据（由使用方从工具调用折算，文案都已算好）
type ToolCardData =
    {
        CallName: string
        DisplayName: string
        Tone: ToolTone
        Badge: string
        ArgCount: int
        Preview: string
        HumanSummary: string
        Error: string option
        Sections: ToolSection list
        RawPayload: string
    }

/// 使用方提供的件：复制按钮（tip、读屏名、取文本）与限高视口（内容、起始可见 → 根与开合函数）
type ToolCardParts =
    {
        Copy: string -> string -> (unit -> string) -> Control
        Reveal: Control -> bool -> Control * (bool -> unit)
    }

/// 工具调用：外框只有一层。头部是展开入口，状态是一行字；展开后的参数与结果不再各自再套边框。
type ToolCallCard(data: ToolCardData, parts: ToolCardParts) =
    let toneResKey =
        match data.Tone with
        | ToolTone.Running -> "Palette.Accent"
        | ToolTone.Awaiting
        | ToolTone.Unknown -> "Palette.Warning"
        | ToolTone.Success -> "Palette.Success"
        | ToolTone.Failed -> "Palette.Danger"

    let hasError = data.Error.IsSome
    let mutable open' = false

    let hstack (spacing: float) (children: Control list) =
        let p =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = spacing,
                VerticalAlignment = VerticalAlignment.Center
            )

        for c in children do
            p.Children.Add c

        p

    let createGlyph (glyph: ToolGlyph) (resKey: string) =
        let pathData =
            match glyph with
            | ToolGlyph.Wrench -> "M 14 3 A 3 3 0 0 0 10 7 L 3 14 L 5 16 L 12 9 A 3 3 0 0 0 16 5"
            | ToolGlyph.Alert -> "M 7 2 L 12 12 L 2 12 Z M 7 5 L 7 8 M 7 10 L 7 10.01"
            | ToolGlyph.Info -> "M 7 2 A 5 5 0 1 1 7 12 A 5 5 0 1 1 7 2 M 7 5 L 7 5.01 M 7 7 L 7 10"
            | ToolGlyph.Check -> "M 2 7 L 5 10 L 12 3"
            | ToolGlyph.Chevron -> "M 4 2 L 9 7 L 4 12"
            | ToolGlyph.Spinner
            | ToolGlyph.SpinnerCompact -> "M 7 1 A 6 6 0 0 1 13 7"

        let p =
            Path(
                Data = Geometry.Parse pathData,
                StrokeThickness = 1.2,
                Fill = Brushes.Transparent,
                Tag = glyph,
                Width = 14.0,
                Height = 14.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        p.Bind(Path.StrokeProperty, Dyn.res resKey) |> ignore

        if glyph = ToolGlyph.Spinner || glyph = ToolGlyph.SpinnerCompact then
            p.Classes.Add "anim-spin"

        p :> Control

    let icon =
        createGlyph
            (match data.Tone with
             | ToolTone.Running -> ToolGlyph.Spinner
             | ToolTone.Awaiting
             | ToolTone.Success -> ToolGlyph.Wrench
             | ToolTone.Failed -> ToolGlyph.Alert
             | ToolTone.Unknown -> ToolGlyph.Info)
            toneResKey

    let statusGlyph =
        createGlyph
            (match data.Tone with
             | ToolTone.Running -> ToolGlyph.SpinnerCompact
             | ToolTone.Awaiting
             | ToolTone.Unknown -> ToolGlyph.Info
             | ToolTone.Success -> ToolGlyph.Check
             | ToolTone.Failed -> ToolGlyph.Alert)
            toneResKey

    let name =
        TextBlock(
            Text = data.DisplayName,
            FontWeight = FontWeight.Medium,
            FontSize = 13.0,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            MinWidth = 0.0,
            VerticalAlignment = VerticalAlignment.Center
        )

    do name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore

    let badge =
        TextBlock(Text = data.Badge, FontSize = 11.0, VerticalAlignment = VerticalAlignment.Center)

    do badge.Bind(TextBlock.ForegroundProperty, Dyn.res toneResKey) |> ignore

    let pill =
        Border(
            Margin = Thickness 0.0,
            Padding = Thickness(8.0, 2.0),
            BorderThickness = Thickness 0.0,
            Background = Brushes.Transparent,
            CornerRadius = CornerRadius 0.0,
            VerticalAlignment = VerticalAlignment.Center
        )

    let summary =
        TextBlock(
            Text = data.Preview,
            FontSize = 11.0,
            Margin = Thickness(0.0, 4.0, 0.0, 0.0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            IsVisible = not (String.IsNullOrWhiteSpace data.Preview)
        )

    do
        summary.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        summary.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

    let human =
        TextBlock(
            Text = data.HumanSummary,
            FontSize = 12.0,
            Margin = Thickness(0.0, 4.0, 0.0, 0.0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        )

    do
        human.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        AutomationProperties.SetAutomationId(human, "tool-human-summary")

    let banner =
        let glyph = createGlyph ToolGlyph.Alert "Palette.Danger"

        let text =
            TextBlock(
                Text = defaultArg data.Error "执行失败",
                FontSize = 11.0,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            )

        text.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        text.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

        let b =
            Border(
                BorderThickness = Thickness 0.0,
                CornerRadius = CornerRadius 0.0,
                Background = Brushes.Transparent,
                Padding = Thickness(0.0, 4.0),
                Margin = Thickness(0.0, 4.0, 0.0, 0.0),
                IsVisible = hasError,
                Child = hstack 4.0 [ glyph; text ]
            )

        b

    let section (s: ToolSection) : Control =
        let header = DockPanel(LastChildFill = false)

        let title =
            TextBlock(
                Text = s.Label,
                FontWeight = FontWeight.Medium,
                FontSize = 11.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        if s.IsError then
            title.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        else
            title.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore

        let copy =
            parts.Copy (sprintf "复制%s" s.Label) (sprintf "复制工具%s" s.Label) (fun () -> s.Raw)

        DockPanel.SetDock(title, Dock.Left)
        DockPanel.SetDock(copy, Dock.Right)
        header.Children.Add title
        header.Children.Add copy

        let content =
            SelectableTextBlock(
                ContextFlyout = null,
                Text = s.Pretty,
                FontSize = 12.0,
                LineHeight = 12.0 * 1.52,
                TextWrapping = TextWrapping.Wrap
            )

        if s.IsError then
            content.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        else
            content.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.CodeText") |> ignore

        content.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore

        content.Bind(SelectableTextBlock.SelectionBrushProperty, Dyn.res "Palette.AccentSoft")
        |> ignore

        let box =
            Border(
                BorderThickness = Thickness 0.0,
                CornerRadius = CornerRadius 8.0,
                Padding = Thickness(12.0, 8.0),
                Child = content
            )

        box.Bind(Border.BackgroundProperty, Dyn.res "Palette.CodeBg") |> ignore
        box.Bind(Border.BorderBrushProperty, Dyn.res "Palette.CodeBorder") |> ignore
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        stack.Children.Add header
        stack.Children.Add box
        stack :> Control

    let detailPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
    let detail, setDetail = parts.Reveal detailPanel false
    let rotate = RotateTransform(Angle = 0.0)

    let rotation =
        DoubleTransition(
            Property = RotateTransform.AngleProperty,
            Duration = TimeSpan.FromMilliseconds 160.0,
            Easing = CubicEaseOut()
        )

    let chevronHost =
        Border(
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 14.0,
            Height = 14.0
        )

    let copyAll =
        parts.Copy "复制工具参数与结果" (sprintf "复制工具 %s 参数与结果" data.CallName) (fun () -> data.RawPayload)

    let headerContent = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)

    let header =
        Button(
            Margin = Thickness 0.0,
            Padding = Thickness 0.0,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Content = headerContent
        )

    let card =
        Border(
            BorderThickness = Thickness 1.0,
            CornerRadius = CornerRadius 12.0,
            Padding = Thickness(12.0, 12.0),
            Margin = Thickness(0.0, 0.0, 0.0, 12.0)
        )

    let syncNames () =
        let verb = if open' then "收起" else "展开"
        AutomationProperties.SetName(header, sprintf "%s工具调用 %s" verb data.CallName)
        AutomationProperties.SetItemStatus(header, sprintf "调用工具 · %s（%s）" data.CallName data.Badge)
        AutomationProperties.SetHelpText(header, (if open' then "收起参数与结果" else "展开查看参数与结果"))
        AutomationProperties.SetHelpText(card, (if open' then "收起参数与结果" else "展开查看参数与结果"))
        ToolTip.SetTip(header, (if open' then "点击收起参数与结果" else "点击展开参数与结果"))

    let toggle () =
        open' <- not open'
        setDetail open'
        summary.IsVisible <- not open' && not (String.IsNullOrWhiteSpace data.Preview)
        rotate.Angle <- (if open' then 90.0 else 0.0)
        syncNames ()

    do
        statusGlyph.Margin <- Thickness(0.0, 0.0, 4.0, 0.0)

        let pillRow =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 0.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        pillRow.Children.Add statusGlyph
        pillRow.Children.Add badge
        pill.Child <- pillRow

        for s in data.Sections do
            detailPanel.Children.Add(section s)

        detail.Margin <- Thickness(0.0, 8.0, 0.0, 0.0)

        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions
        let chevron = createGlyph ToolGlyph.Chevron "Palette.TextMuted"
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron
        copyAll.Margin <- Thickness(0.0, 0.0, 4.0, 0.0)
        copyAll.VerticalAlignment <- VerticalAlignment.Center
        let actions = hstack 4.0 [ copyAll; chevronHost ]
        let left =
            DockPanel(
                LastChildFill = true,
                MinWidth = 0.0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            )

        DockPanel.SetDock(icon, Dock.Left)
        DockPanel.SetDock(pill, Dock.Right)
        left.Children.Add icon
        left.Children.Add pill
        left.Children.Add name
        let dock = DockPanel(LastChildFill = true, MinWidth = 0.0)
        DockPanel.SetDock(actions, Dock.Right)
        dock.Children.Add actions
        dock.Children.Add left
        headerContent.Children.Add dock
        headerContent.Children.Add human
        headerContent.Children.Add summary

        if hasError then
            headerContent.Children.Add banner

        header.HorizontalAlignment <- HorizontalAlignment.Stretch
        header.Classes.Add "ghost"
        header.Classes.Add "hover-bordered"
        header.Click.Add(fun _ -> toggle ())

        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        stack.Children.Add header
        stack.Children.Add detail
        card.Child <- stack
        card.Bind(Border.BackgroundProperty, Dyn.res "Palette.Surface") |> ignore

        card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Line") |> ignore
        AutomationProperties.SetName(card, sprintf "工具调用 %s" data.CallName)
        syncNames ()

    member _.Host: Control = card
    member _.Header: Button = header
    member _.Expanded = open'
    member _.Summary = summary
    member _.Banner = banner
    member _.Details = detailPanel
    member _.Rotation = rotate
    member _.Toggle() = toggle ()

    /// 单入口：从 ToolCallCardVS 渲染
    static member Render(vs: ToolCallCardVS, parts: ToolCardParts) : Control =
        let tone =
            match vs.Tone.ToLowerInvariant() with
            | "running" -> ToolTone.Running
            | "awaiting" -> ToolTone.Awaiting
            | "success" -> ToolTone.Success
            | "failed" -> ToolTone.Failed
            | _ -> ToolTone.Unknown

        let prettyJson raw = raw
        let sections = ToolCallCard.sections prettyJson vs.ArgumentsJson vs.Result tone
        let rawPayload = ToolCallCard.rawPayload vs.ArgumentsJson vs.Result tone

        let data =
            {
                CallName = vs.CallName
                DisplayName = vs.DisplayName
                Tone = tone
                Badge = vs.Badge
                ArgCount = vs.ArgCount
                Preview = vs.Preview
                HumanSummary = vs.HumanSummary
                Error = vs.Error
                Sections = sections
                RawPayload = rawPayload
            }

        ToolCallCard(data, parts).Host
