namespace Wanxiang2.ChatEmpty

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 用到的笔刷
[<RequireQualifiedAccess>]
type EmptyInk =
    | Text
    | TextMuted
    | SurfaceContainer
    | Hairline
    | Accent
    | TextOnAccent
    | Surface
    | Border

/// 用到的活度量
[<RequireQualifiedAccess>]
type EmptyGauge =
    | SpaceXs
    | SpaceMd
    | SpaceXl
    | Space2xl
    | Space4xl
    | Space6xl
    | RadiusMd
    | RadiusLg
    | BorderWidth
    | FontDisplay
    | FontTitle
    | FontBody
    | FontCaption
    | FontSmall
    | TextButtonPadY
    | TextButtonMinHeight
    | ActionMinWidth
    | ActionHeight
    | MaxWidth
    /// 强调标题字距（旧 Spacing.Tracking.emphasis）
    | TrackingEmphasis
    /// 空态提示行高（旧 ReadingRhythm.uiBodyLineHeight）
    | PromptLineHeight
    /// 说明文案行高（旧 ReadingRhythm.captionLineHeight）
    | CaptionLineHeight

/// 外观
type ChatEmptyLook =
    { Ink: EmptyInk -> IBrush
      Gauge: EmptyGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      /// 主按钮 / 次按钮样式类（旧 Ui.Primary / Secondary）
      DressPrimary: Border -> unit
      DressSecondary: Border -> unit
      /// 分组卡样式类（旧 groupingCard）
      DressCard: Border -> unit }

/// 可点文字按钮（本库自带）
type private TextSpot(label: string) as this =
    inherit Spot()
    let text = TextBlock(Text = label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
    do this.Child <- text
    member _.Label = text

/// 对话区空态（旧 ChatView.emptyPanel / ShowEmpty / ShowEmbeddedEmpty / HideEmpty）：
/// 对话起点提示、品牌空态卡、起手式与预设芯片、或内嵌自带交互的内容。
type ChatEmpty(look: ChatEmptyLook) =
    let ink = look.Ink
    let gauge = look.Gauge
    let panel =
        StackPanel(
            Orientation = Orientation.Vertical,
            Spacing = 0.0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false)
    let mutable primary: unit -> unit = ignore
    let mutable lastKey: (string * string * string option) option = None
    let mutable presets: (string * string * (unit -> unit)) list = []
    let mutable starterPick: (string -> unit) option = None
    let mutable starters: (string * string) list = []
    let mutable bottom = false

    let sized (t: TextBlock) g = Follow.live t (gauge g) (fun s -> t.FontSize <- s)
    let font (t: TextBlock) = Follow.live t look.BodyFont (fun f -> t.FontFamily <- f)

    let textButton (label: string) (primaryTone: bool) (name: string) (help: string option) (tip: string) (action: unit -> unit) =
        let b = TextSpot label
        b.Focusable <- true
        b.Cursor <- new Cursor(StandardCursorType.Hand)
        b.VerticalAlignment <- VerticalAlignment.Center
        if primaryTone then look.DressPrimary b else look.DressSecondary b
        Follow.live b (gauge EmptyGauge.TextButtonMinHeight) (fun h -> b.MinHeight <- h)
        AutomationProperties.SetName(b, name)
        help |> Option.iter (fun h -> AutomationProperties.SetHelpText(b, h))
        ToolTip.SetTip(b, tip)
        b.OnInvoke action
        b

    let chipPad (b: Border) =
        Follow.live2 b (gauge EmptyGauge.Space2xl) (gauge EmptyGauge.TextButtonPadY) (fun x y -> b.Padding <- Thickness(x, y))

    let actionButton (label: string) =
        let b = textButton label true label (Some label) label (fun () -> primary ())
        b.HorizontalAlignment <- HorizontalAlignment.Center
        // 空态主按钮横向更宽、纵向不留内边距（旧 emptyActionButton）
        Follow.live b (gauge EmptyGauge.Space2xl) (fun x -> b.Padding <- Thickness(x, 0.0))
        Follow.live b (gauge EmptyGauge.ActionMinWidth) (fun w -> b.MinWidth <- w)
        Follow.live b (gauge EmptyGauge.ActionHeight) (fun h -> b.Height <- h)
        Follow.live b (gauge EmptyGauge.RadiusMd) (fun r -> b.CornerRadius <- CornerRadius r)
        b.IsVisible <- true
        b :> Border

    let prompt (title: string) (hint: string) (action: Border option) =
        let heading =
            TextBlock(
                Text = title,
                FontWeight = FontWeight.Medium,
                Foreground = ink EmptyInk.Text,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap)
        sized heading EmptyGauge.FontDisplay
        font heading
        let caption =
            TextBlock(
                Text = hint,
                Foreground = ink EmptyInk.TextMuted,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap)
        sized caption EmptyGauge.FontBody
        Follow.live caption (gauge EmptyGauge.PromptLineHeight) (fun lh -> caption.LineHeight <- lh)
        font caption
        let column =
            StackPanel(Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center)
        Follow.live column (gauge EmptyGauge.SpaceMd) (fun s -> column.Spacing <- s)
        Follow.live column (gauge EmptyGauge.MaxWidth) (fun w -> column.MaxWidth <- w)
        column.Children.Add heading
        column.Children.Add caption
        action |> Option.iter (fun button ->
            button.HorizontalAlignment <- HorizontalAlignment.Center
            column.Children.Add button)
        column :> Control

    let brandCard (logo: Control) (title: string) (hint: string) (action: Border option) =
        let titleBlock =
            TextBlock(
                Text = title,
                Foreground = ink EmptyInk.Text,
                HorizontalAlignment = HorizontalAlignment.Center)
        sized titleBlock EmptyGauge.FontTitle
        Follow.live titleBlock (gauge EmptyGauge.TrackingEmphasis) (fun t -> titleBlock.LetterSpacing <- t)
        font titleBlock
        let hintBlock =
            TextBlock(
                Text = hint,
                Foreground = ink EmptyInk.TextMuted,
                TextWrapping = TextWrapping.Wrap)
        sized hintBlock EmptyGauge.FontCaption
        Follow.live hintBlock (gauge EmptyGauge.CaptionLineHeight) (fun lh -> hintBlock.LineHeight <- lh)
        font hintBlock
        let column = StackPanel(Orientation = Orientation.Vertical)
        Follow.live column (gauge EmptyGauge.SpaceXl) (fun s -> column.Spacing <- s)
        logo.HorizontalAlignment <- HorizontalAlignment.Center
        column.Children.Add logo
        column.Children.Add titleBlock
        if not (String.IsNullOrWhiteSpace hint) then column.Children.Add hintBlock
        action |> Option.iter (fun b -> column.Children.Add b)
        let card = Border(Child = column)
        look.DressCard card
        Follow.live2 card (gauge EmptyGauge.Space4xl) (gauge EmptyGauge.Space4xl) (fun x y -> card.Padding <- Thickness(x, y))
        Follow.live card (gauge EmptyGauge.RadiusLg) (fun r -> card.CornerRadius <- CornerRadius r)
        Follow.live card (gauge EmptyGauge.BorderWidth) (fun w -> card.BorderThickness <- Thickness w)
        card.Background <- ink EmptyInk.SurfaceContainer
        card.BorderBrush <- ink EmptyInk.Hairline
        card :> Control

    let presetRow () =
        let row = WrapPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center)
        for name, detail, run in presets do
            let chip = textButton name false (sprintf "套用预设 %s" name) (Some detail) detail run
            chip.Name <- "preset-chip"
            chipPad chip
            Follow.live chip (gauge EmptyGauge.SpaceXs) (fun s -> chip.Margin <- Thickness s)
            row.Children.Add chip
        let caption =
            TextBlock(Text = "从预设开始", Foreground = ink EmptyInk.TextMuted, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center)
        sized caption EmptyGauge.FontCaption
        Follow.live caption (gauge EmptyGauge.CaptionLineHeight) (fun lh -> caption.LineHeight <- lh)
        font caption
        let column = StackPanel(Orientation = Orientation.Vertical)
        Follow.live column (gauge EmptyGauge.SpaceXs) (fun s -> column.Spacing <- s)
        Follow.live column (gauge EmptyGauge.SpaceXl) (fun s -> column.Margin <- Thickness(0.0, s, 0.0, 0.0))
        column.Children.Add caption
        column.Children.Add row
        column :> Control

    let starterRow () =
        let row = WrapPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center)
        Follow.live row (gauge EmptyGauge.SpaceXl) (fun s -> row.Margin <- Thickness(0.0, s, 0.0, 0.0))
        for label, seed in starters do
            let chip = textButton label false label None (sprintf "填入「%s」" seed) (fun () ->
                starterPick |> Option.iter (fun pick -> pick seed))
            chip.Name <- "starter-chip"
            chipPad chip
            Follow.live chip (gauge EmptyGauge.SpaceXs) (fun s -> chip.Margin <- Thickness s)
            row.Children.Add chip
        row :> Control

    let applyMargin () =
        let side = Follow.now (gauge EmptyGauge.Space2xl)
        let bottomPad = Follow.now (if bottom then gauge EmptyGauge.Space2xl else gauge EmptyGauge.Space6xl)
        panel.Margin <- Thickness(side, 0.0, side, bottomPad)

    do
        Follow.live panel (gauge EmptyGauge.MaxWidth) (fun w -> panel.MaxWidth <- w)
        Follow.live2 panel (gauge EmptyGauge.Space2xl) (gauge EmptyGauge.Space6xl) (fun _ _ -> applyMargin ())
        Follow.live panel (gauge EmptyGauge.Space2xl) (fun _ -> applyMargin ())

    /// 放进布局的根
    member _.Host: Control = panel
    member _.IsShown = panel.IsVisible

    /// 起手式芯片列表（标签 → 填入输入框的开头）；空则不画行
    member _.SetStarters(items: (string * string) list) = starters <- items; lastKey <- None

    /// 选中起手式时把半句话交给宿主（旧 SetStarterHandler）
    member _.SetStarterPick(pick: (string -> unit) option) = starterPick <- pick; lastKey <- None

    /// 预设芯片（名、说明、动作）；空则不画行
    member _.SetPresets(items: (string * string * (unit -> unit)) list) = presets <- items; lastKey <- None

    /// 紧凑起步：空态贴底（旧 SetStartMode）
    member _.SetBottomAligned(value: bool) =
        bottom <- value
        panel.VerticalAlignment <- if value then VerticalAlignment.Bottom else VerticalAlignment.Center
        applyMargin ()

    /// 对话起点提示（旧 NoConversation / EmptyConversation 分支）
    member _.ShowPrompt(title: string, hint: string, onPrimary: (string * (unit -> unit)) option) =
        match onPrimary with
        | Some(_, action) -> primary <- action
        | None -> primary <- ignore
        let key = (title, hint, onPrimary |> Option.map fst)
        if lastKey <> Some key then
            lastKey <- Some key
            let action = onPrimary |> Option.map (fun (label, _) -> actionButton label)
            panel.Children.Clear()
            panel.Children.Add(prompt title hint action)
            if not (List.isEmpty presets) then panel.Children.Add(presetRow ())
            if starterPick.IsSome && not (List.isEmpty starters) then panel.Children.Add(starterRow ())
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        panel.IsVisible <- true

    /// 品牌空态卡（旧 NotConnected / ConnectionFailed / NoProvider 分支）
    member _.ShowBrand(logo: Control, title: string, hint: string, onPrimary: (string * (unit -> unit)) option) =
        match onPrimary with
        | Some(_, action) -> primary <- action
        | None -> primary <- ignore
        let key = ("brand:" + title, hint, onPrimary |> Option.map fst)
        if lastKey <> Some key then
            lastKey <- Some key
            let action = onPrimary |> Option.map (fun (label, _) -> actionButton label)
            panel.Children.Clear()
            panel.Children.Add(brandCard logo title hint action)
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        panel.IsVisible <- true

    /// 内嵌自带交互的内容（旧 ShowEmbeddedEmpty）
    member _.ShowEmbedded(content: Control) =
        lastKey <- None
        primary <- ignore
        let already = panel.Children.Count = 1 && obj.ReferenceEquals(panel.Children[0], content)
        if not already then
            match content.Parent with
            | :? Panel as p -> p.Children.Remove content |> ignore
            | _ -> ()
            panel.Children.Clear()
            panel.Children.Add content
        panel.HorizontalAlignment <- HorizontalAlignment.Stretch
        panel.IsVisible <- true

    /// 隐藏空态（旧 HideEmpty 的空态部分）
    member _.Hide() =
        lastKey <- None
        primary <- ignore
        panel.IsVisible <- false
