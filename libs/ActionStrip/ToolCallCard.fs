namespace Wanxiang2.ToolCallCard

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 卡片的状态色调（由使用方从工具调用状态折算）
[<RequireQualifiedAccess>]
type ToolTone =
    | Running
    | Awaiting
    | Success
    | Failed
    | Unknown

[<RequireQualifiedAccess>]
type ToolInk =
    | Text
    | TextMuted
    | Accent
    | Warning
    | Success
    | Danger
    | DangerSoft
    | SurfaceSoft
    | SurfaceContainer
    | BorderSoft
    | Hairline
    | CodeBg
    | CodeBorder
    | CodeText
    | Selection

[<RequireQualifiedAccess>]
type ToolGauge =
    | BorderWidth
    /// 旧 spaceXXs / Xs / Md / Xl
    | SpaceXXs
    | SpaceXs
    | SpaceMd
    | SpaceXl
    /// 旧 Radius.sm / md / pill
    | RadiusSm
    | RadiusMd
    | RadiusPill
    /// 旧 fontMicro / fontCaption / fontSmall
    | FontMicro
    | FontCaption
    | FontSmall
    /// 箭头槽（旧 iconGlyph）
    | IconGlyph
    /// 箭头转动毫秒（旧 disclosureChevronRotate）
    | RotateMs

[<RequireQualifiedAccess>]
type ToolGlyph =
    | Wrench
    | Alert
    | Info
    | Check
    | Chevron
    /// 转圈（旧 spinnerSize），笔刷参数忽略
    | Spinner
    /// 小转圈（旧 spinnerCompactSize）
    | SpinnerCompact

type ToolCallCardLook =
    { Ink: ToolInk -> IBrush
      Gauge: ToolGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      MonoFont: IObservable<FontFamily>
      Glyph: ToolGlyph -> IBrush -> Control
      /// 头部挂表面状态反馈（旧 Interaction.surface bordered）
      Dress: Border -> unit
      /// 技术文本行高随字号（旧 ReadingRhythm.technicalLineHeight）
      TechLineHeight: float -> float
      Reduced: IObservable<bool> }

/// 展开后的一节（参数或结果）
type ToolSection =
    { Label: string
      /// 复制出去的原文
      Raw: string
      /// 显示用的已排版 JSON（旧 ToolArgsPreview.prettyJson）
      Pretty: string
      IsError: bool }

/// 一张工具卡的显示数据（由使用方从工具调用折算，文案都已算好）
type ToolCardData =
    { /// 函数名（读屏名用）
      CallName: string
      /// 显示名（旧 ToolSummary.displayNameOfFunction）
      DisplayName: string
      Tone: ToolTone
      /// 状态药丸文字（旧 ToolCallStatus.badgeText）
      Badge: string
      /// 一行参数摘要与参数个数（旧 ToolArgsPreview.preview）
      ArgCount: int
      Preview: string
      /// 人话摘要（旧 ToolSummary.describe）
      HumanSummary: string
      /// 失败时的横幅文字；None 表示没有失败
      Error: string option
      Sections: ToolSection list
      /// 头部复制按钮复制的整段（旧 rawPayloadText）
      RawPayload: string }

/// 使用方提供的件：复制按钮（tip、读屏名、取文本）与限高视口（内容、起始可见 → 根与开合函数）
type ToolCardParts =
    { Copy: string -> string -> (unit -> string) -> Control
      Reveal: Control -> bool -> Control * (bool -> unit) }

/// 工具调用卡（旧 MessageCard.toolCallCard）：头部是唯一展开入口，显示图标、名称、状态药丸、
/// 人话摘要、参数摘要、失败横幅；展开后在限高视口里列出参数与结果，各带复制。
type ToolCallCard(look: ToolCallCardLook, data: ToolCardData, parts: ToolCardParts) =
    let ink = look.Ink
    let gauge = look.Gauge
    let toneBrush =
        match data.Tone with
        | ToolTone.Running -> ink ToolInk.Accent
        | ToolTone.Awaiting
        | ToolTone.Unknown -> ink ToolInk.Warning
        | ToolTone.Success -> ink ToolInk.Success
        | ToolTone.Failed -> ink ToolInk.Danger
    let toneBg = if data.Tone = ToolTone.Failed then ink ToolInk.DangerSoft else ink ToolInk.SurfaceSoft
    let hasError = data.Error.IsSome
    let mutable open' = false

    let hstack (spacing: ToolGauge) (children: Control list) =
        let p = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live p (gauge spacing) (fun s -> p.Spacing <- s)
        for c in children do p.Children.Add c
        p
    let sized (t: TextBlock) (g: ToolGauge) = Follow.live t (gauge g) (fun s -> t.FontSize <- s)
    let monoOf (t: TextBlock) = Follow.live t look.MonoFont (fun f -> t.FontFamily <- f)

    let icon =
        look.Glyph
            (match data.Tone with
             | ToolTone.Running -> ToolGlyph.Spinner
             | ToolTone.Awaiting
             | ToolTone.Success -> ToolGlyph.Wrench
             | ToolTone.Failed -> ToolGlyph.Alert
             | ToolTone.Unknown -> ToolGlyph.Info)
            toneBrush
    let statusGlyph =
        look.Glyph
            (match data.Tone with
             | ToolTone.Running -> ToolGlyph.SpinnerCompact
             | ToolTone.Awaiting
             | ToolTone.Unknown -> ToolGlyph.Info
             | ToolTone.Success -> ToolGlyph.Check
             | ToolTone.Failed -> ToolGlyph.Alert)
            toneBrush
    let name =
        TextBlock(
            Text = data.DisplayName,
            // 等宽技术文本用 Medium 即可：规范只有两档字重
            FontWeight = FontWeight.Medium,
            Foreground = ink ToolInk.Text,
            VerticalAlignment = VerticalAlignment.Center)
    let badge = TextBlock(Text = data.Badge, Foreground = toneBrush, VerticalAlignment = VerticalAlignment.Center)
    let pill =
        Border(
            Background = toneBg,
            BorderBrush = ink ToolInk.BorderSoft,
            Margin = Thickness 0.0,
            VerticalAlignment = VerticalAlignment.Center)
    let summary =
        TextBlock(
            Text = data.Preview,
            Foreground = ink ToolInk.TextMuted,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsVisible = not (String.IsNullOrWhiteSpace data.Preview))
    let human =
        TextBlock(Text = data.HumanSummary, Foreground = ink ToolInk.Text, TextWrapping = TextWrapping.Wrap)
    let banner =
        let glyph = look.Glyph ToolGlyph.Alert (ink ToolInk.Danger)
        glyph.VerticalAlignment <- VerticalAlignment.Center
        let text =
            TextBlock(
                Text = defaultArg data.Error "执行失败",
                Foreground = ink ToolInk.Danger,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center)
        monoOf text
        sized text ToolGauge.FontMicro
        let b =
            Border(
                Background = ink ToolInk.DangerSoft,
                BorderBrush = ink ToolInk.Hairline,
                IsVisible = hasError,
                Child = hstack ToolGauge.SpaceXs [ glyph; text ])
        Follow.live2 b (gauge ToolGauge.BorderWidth) (gauge ToolGauge.RadiusSm) (fun w r ->
            b.BorderThickness <- Thickness w
            b.CornerRadius <- CornerRadius r)
        Follow.live2 b (gauge ToolGauge.SpaceMd) (gauge ToolGauge.SpaceXs) (fun md xs ->
            b.Padding <- Thickness(md, xs)
            b.Margin <- Thickness(0.0, xs, 0.0, 0.0))
        b

    let section (s: ToolSection) : Control =
        let header = DockPanel(LastChildFill = false)
        let title =
            TextBlock(
                Text = s.Label,
                FontWeight = FontWeight.Medium,
                Foreground = (if s.IsError then ink ToolInk.Danger else ink ToolInk.TextMuted),
                VerticalAlignment = VerticalAlignment.Center)
        Follow.live title look.BodyFont (fun f -> title.FontFamily <- f)
        sized title ToolGauge.FontMicro
        let copy = parts.Copy (sprintf "复制%s" s.Label) (sprintf "复制工具%s" s.Label) (fun () -> s.Raw)
        DockPanel.SetDock(title, Dock.Left)
        DockPanel.SetDock(copy, Dock.Right)
        header.Children.Add title
        header.Children.Add copy
        let content =
            SelectableTextBlock(
                // 主题自带的英文「Copy」浮层会吞掉右键，消息菜单出不来
                ContextFlyout = null,
                Text = s.Pretty,
                Foreground = (if s.IsError then ink ToolInk.Danger else ink ToolInk.CodeText),
                TextWrapping = TextWrapping.Wrap,
                SelectionBrush = ink ToolInk.Selection)
        Follow.live content look.MonoFont (fun f -> content.FontFamily <- f)
        Follow.live content (gauge ToolGauge.FontCaption) (fun size ->
            content.FontSize <- size
            content.LineHeight <- look.TechLineHeight size)
        let box = Border(Background = ink ToolInk.CodeBg, BorderBrush = ink ToolInk.CodeBorder, Child = content)
        Follow.live2 box (gauge ToolGauge.BorderWidth) (gauge ToolGauge.RadiusSm) (fun w r ->
            box.BorderThickness <- Thickness w
            box.CornerRadius <- CornerRadius r)
        Follow.live2 box (gauge ToolGauge.SpaceXl) (gauge ToolGauge.SpaceMd) (fun xl md -> box.Padding <- Thickness(xl, md))
        let stack = StackPanel(Orientation = Orientation.Vertical)
        Follow.live stack (gauge ToolGauge.SpaceXs) (fun s -> stack.Spacing <- s)
        stack.Children.Add header
        stack.Children.Add box
        stack

    let detailPanel = StackPanel(Orientation = Orientation.Vertical)
    let detail, setDetail = parts.Reveal detailPanel false
    let rotate = RotateTransform 0.0
    let rotation = DoubleTransition(Property = RotateTransform.AngleProperty, Easing = CubicEaseOut())
    let chevronHost = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
    let copyAll = parts.Copy "复制工具参数与结果" (sprintf "复制工具 %s 参数与结果" data.CallName) (fun () -> data.RawPayload)
    let headerContent = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
    let header =
        Spot(
            Margin = Thickness 0.0,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Child = headerContent)
    let card = Border(Background = ink ToolInk.SurfaceContainer, BorderBrush = ink ToolInk.Hairline)

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
        // 展开后参数摘要让位给完整详情
        summary.IsVisible <- not open' && not (String.IsNullOrWhiteSpace data.Preview)
        rotate.Angle <- (if open' then 90.0 else 0.0)
        syncNames ()

    do
        icon.VerticalAlignment <- VerticalAlignment.Center
        statusGlyph.VerticalAlignment <- VerticalAlignment.Center
        Follow.live statusGlyph (gauge ToolGauge.SpaceXs) (fun xs -> statusGlyph.Margin <- Thickness(0.0, 0.0, xs, 0.0))
        sized name ToolGauge.FontSmall
        sized badge ToolGauge.FontMicro
        let pillRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 0.0, VerticalAlignment = VerticalAlignment.Center)
        pillRow.Children.Add statusGlyph
        pillRow.Children.Add badge
        pill.Child <- pillRow
        Follow.live2 pill (gauge ToolGauge.BorderWidth) (gauge ToolGauge.RadiusPill) (fun w r ->
            pill.BorderThickness <- Thickness w
            pill.CornerRadius <- CornerRadius r)
        Follow.live2 pill (gauge ToolGauge.SpaceMd) (gauge ToolGauge.SpaceXXs) (fun md xxs -> pill.Padding <- Thickness(md, xxs))
        monoOf summary
        sized summary ToolGauge.FontMicro
        Follow.live summary (gauge ToolGauge.SpaceXs) (fun xs -> summary.Margin <- Thickness(0.0, xs, 0.0, 0.0))
        sized human ToolGauge.FontCaption
        Follow.live human (gauge ToolGauge.SpaceXs) (fun xs -> human.Margin <- Thickness(0.0, xs, 0.0, 0.0))
        AutomationProperties.SetAutomationId(human, "tool-human-summary")

        Follow.live detailPanel (gauge ToolGauge.SpaceMd) (fun s -> detailPanel.Spacing <- s)
        for s in data.Sections do detailPanel.Children.Add(section s)
        Follow.live detail (gauge ToolGauge.SpaceMd) (fun md -> detail.Margin <- Thickness(0.0, md, 0.0, 0.0))

        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions
        Follow.live2 chevronHost (gauge ToolGauge.RotateMs) look.Reduced (fun ms reduced ->
            rotation.Duration <- (if reduced then TimeSpan.Zero else TimeSpan.FromMilliseconds ms))
        let chevron = look.Glyph ToolGlyph.Chevron (ink ToolInk.TextMuted)
        chevron.HorizontalAlignment <- HorizontalAlignment.Center
        chevron.VerticalAlignment <- VerticalAlignment.Center
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron
        Follow.live chevronHost (gauge ToolGauge.IconGlyph) (fun g ->
            chevronHost.Width <- g
            chevronHost.Height <- g
            chevronHost.MinWidth <- g
            chevronHost.MinHeight <- g)
        Follow.live copyAll (gauge ToolGauge.SpaceXs) (fun xs -> copyAll.Margin <- Thickness(0.0, 0.0, xs, 0.0))
        copyAll.VerticalAlignment <- VerticalAlignment.Center
        let actions = hstack ToolGauge.SpaceXs [ copyAll; chevronHost ]
        let left = hstack ToolGauge.SpaceMd [ icon; name; pill ]
        let dock = DockPanel(LastChildFill = false)
        DockPanel.SetDock(left, Dock.Left)
        DockPanel.SetDock(actions, Dock.Right)
        dock.Children.Add left
        dock.Children.Add actions
        headerContent.Children.Add dock
        // 人话摘要先说「做了什么」，原始参数留在下一行和展开详情里
        headerContent.Children.Add human
        headerContent.Children.Add summary
        if hasError then headerContent.Children.Add banner

        Follow.live2 header (gauge ToolGauge.BorderWidth) (gauge ToolGauge.RadiusSm) (fun w r ->
            header.BorderThickness <- Thickness w
            header.CornerRadius <- CornerRadius r)
        look.Dress header
        // 头部是唯一展开入口：整卡若也能激活，读屏会在同一个开关上停两次
        header.OnInvoke toggle

        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 0.0)
        stack.Children.Add header
        stack.Children.Add detail
        card.Child <- stack
        Follow.live2 card (gauge ToolGauge.BorderWidth) (gauge ToolGauge.RadiusMd) (fun w r ->
            card.BorderThickness <- Thickness w
            card.CornerRadius <- CornerRadius r)
        Follow.live card (gauge ToolGauge.SpaceXl) (fun xl ->
            card.Padding <- Thickness(xl, xl)
            card.Margin <- Thickness(0.0, 0.0, 0.0, xl))
        AutomationProperties.SetName(card, sprintf "工具调用 %s" data.CallName)
        syncNames ()

    member _.Host: Control = card
    member _.Header: Border = header
    member _.Expanded = open'
    member _.Summary = summary
    member _.Banner = banner
    member _.Details = detailPanel
    member _.Rotation = rotate
    /// 按一下头部
    member _.Toggle() = header.Invoke()

/// 由工具调用折算显示数据时用的小规则
module ToolCallCard =
    /// 详情各节（旧 createDetailSection 的取舍）：有参数列参数；有结果列结果（失败时标「异常」）；
    /// 没有结果而状态未知时，列一节说明没收到结果
    let sections (pretty: string -> string) (argumentsJson: string) (result: string option) (tone: ToolTone) =
        [ if not (String.IsNullOrWhiteSpace argumentsJson) then
              { Label = "调用参数"; Raw = argumentsJson; Pretty = pretty argumentsJson; IsError = false }
          match result with
          | Some r when not (String.IsNullOrWhiteSpace r) ->
              let failed = tone = ToolTone.Failed
              { Label = (if failed then "执行结果 (异常)" else "执行结果"); Raw = r; Pretty = pretty r; IsError = failed }
          | _ ->
              if tone = ToolTone.Unknown then
                  let t = "未收到工具执行结果（生成已结束）"
                  { Label = "执行结果"; Raw = t; Pretty = pretty t; IsError = false } ]

    /// 头部复制的整段（旧 rawPayloadText）
    let rawPayload (argumentsJson: string) (result: string option) (tone: ToolTone) =
        [ if not (String.IsNullOrWhiteSpace argumentsJson) then sprintf "// 参数 (Arguments)\n%s" argumentsJson
          match result with
          | Some r when not (String.IsNullOrWhiteSpace r) -> sprintf "// 结果 (Result)\n%s" r
          | _ -> if tone = ToolTone.Unknown then "// 结果 (Result)\n未收到工具执行结果（生成已结束）" ]
        |> String.concat "\n\n"
