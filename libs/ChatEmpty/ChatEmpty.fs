namespace Wanxiang2.ChatEmpty

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Markup.Xaml.MarkupExtensions
open Wanxiang2.WorkspaceState

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 主题资源字典的浮点值现读：用量时刻取值，主题改值后下一次应用即生效。
module private Res =
    let float (key: string) (fallback: float) =
        match Application.Current with
        | null -> fallback
        | app ->
            match app.TryFindResource(box key) with
            | true, v -> unbox v
            | _ -> fallback

/// 对话区空态（旧 ChatView.emptyPanel / ShowEmpty / ShowEmbeddedEmpty / HideEmpty）：
/// 对话起点提示、品牌空态卡、起手式芯片、或内嵌内容。
/// 统一为 Render(ChatPhase) 单入口，废除 Look 记录与 Spot 仿制件，改用原生 Button 与样式类。
/// starters 与 onStarter 是构造期事实（不再有 Set* 写入成员）：有起手式且有宿主回调时才画起手式行。
type ChatEmpty(starters: (string * string) list, onStarter: (string -> unit) option, ?brandLogo: (float -> Control)) =
    let panel =
        StackPanel(
            Orientation = Orientation.Vertical,
            Spacing = 0.0,
            MaxWidth = 600.0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false)
    let mutable primary: unit -> unit = ignore
    let mutable lastKey: (string * string * string option) option = None
    let mutable bottom = false

    /// 起手式芯片（标签、种子、回调）：没有宿主回调就没有可用的芯片
    let starterChips =
        match onStarter with
        | Some pick -> starters |> List.map (fun (label, seed) -> label, seed, pick)
        | None -> []

    let textButton (label: string) (primaryTone: bool) (name: string) (help: string option) (tip: string) (action: unit -> unit) =
        let b = Button(Content = label)
        b.Focusable <- true
        b.Cursor <- new Cursor(StandardCursorType.Hand)
        b.VerticalAlignment <- VerticalAlignment.Center
        if primaryTone then
            b.Classes.Add "primary"
            b.Classes.Add "button-primary"
        else
            b.Classes.Add "secondary"
            b.Classes.Add "button-secondary"
        AutomationProperties.SetName(b, name)
        help |> Option.iter (fun h -> AutomationProperties.SetHelpText(b, h))
        ToolTip.SetTip(b, tip)
        b.Click.Add(fun _ -> action ())
        b

    let actionButton (label: string) =
        let b = textButton label true label None label (fun () -> primary ())
        b.HorizontalAlignment <- HorizontalAlignment.Center
        b.IsVisible <- true
        b

    let prompt (title: string) (hint: string) (action: Button option) =
        let heading =
            TextBlock(
                Text = title,
                FontWeight = FontWeight.Medium,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap)
        heading.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        heading.FontSize <- 20.0
        let caption =
            TextBlock(
                Text = hint,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap)
        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        caption.FontSize <- 13.5
        caption.LineHeight <- 20.0
        let column =
            StackPanel(Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8.0, MaxWidth = 560.0)
        column.Children.Add heading
        column.Children.Add caption
        action |> Option.iter (fun button ->
            button.HorizontalAlignment <- HorizontalAlignment.Center
            column.Children.Add button)
        column :> Control

    let brandCard (logo: Control) (title: string) (hint: string) (action: Button option) =
        let titleBlock =
            TextBlock(
                Text = title,
                FontWeight = FontWeight.Medium,
                FontSize = 16.0,
                HorizontalAlignment = HorizontalAlignment.Center)
        titleBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        let hintBlock =
            TextBlock(
                Text = hint,
                FontSize = 12.5,
                LineHeight = 18.0,
                TextWrapping = TextWrapping.Wrap)
        hintBlock.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        logo.HorizontalAlignment <- HorizontalAlignment.Center
        column.Children.Add logo
        column.Children.Add titleBlock
        if not (String.IsNullOrWhiteSpace hint) then column.Children.Add hintBlock
        action |> Option.iter (fun b -> column.Children.Add b)
        let card = Border(Child = column, Padding = Thickness 24.0, CornerRadius = CornerRadius 12.0, BorderThickness = Thickness 1.0)
        card.Classes.Add "grouping-card"
        card.Bind(Border.BackgroundProperty, Dyn.res "Palette.SurfaceContainer") |> ignore
        card.Bind(Border.BorderBrushProperty, Dyn.res "Palette.Hairline") |> ignore
        card :> Control

    let starterRow () =
        let row = WrapPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = Thickness(0.0, 12.0, 0.0, 0.0))
        for label, seed, pick in starterChips do
            let chip = textButton label false label None (sprintf "填入「%s」" seed) (fun () -> pick seed)
            chip.Name <- "starter-chip"
            chip.Margin <- Thickness 4.0
            row.Children.Add chip
        row :> Control

    let applyMargin () =
        // 空态留白只由这里归属：左右取主题 Space.X2l；贴底时底部收紧到同档，否则留宽
        let side = Res.float "Space.X2l" 16.0
        let bottomPad = if bottom then side else 40.0
        panel.Margin <- Thickness(side, 0.0, side, bottomPad)

    do
        applyMargin ()

    /// 放进布局的根
    member _.Host: Control = panel
    member _.IsShown = panel.IsVisible

    /// 紧凑起步：空态贴底（旧 SetMode）；垂直对齐与外边距只由这里一处归属
    member _.ApplyBottomAligned(value: bool) =
        bottom <- value
        panel.VerticalAlignment <- if value then VerticalAlignment.Bottom else VerticalAlignment.Center
        applyMargin ()

    /// 对话起点提示
    member this.ShowPrompt(title: string, hint: string, onPrimary: (string * (unit -> unit)) option) =
        match onPrimary with
        | Some(_, action) -> primary <- action
        | None -> primary <- ignore
        let key = (title, hint, onPrimary |> Option.map fst)
        if lastKey <> Some key then
            lastKey <- Some key
            let action = onPrimary |> Option.map (fun (label, _) -> actionButton label)
            panel.Children.Clear()
            panel.Children.Add(prompt title hint action)
            if not (List.isEmpty starterChips) then panel.Children.Add(starterRow ())
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        panel.IsVisible <- true

    /// 品牌空态卡
    member this.ShowBrand(logo: Control, title: string, hint: string, onPrimary: (string * (unit -> unit)) option) =
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

    /// 内嵌自带交互的内容
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

    /// 单入口：根据 ChatPhase 幂等渲染空态内容（非 Empty 时自动隐藏）
    member this.Render(phase: ChatPhase, ?onPrimary: string * (unit -> unit)) =
        match phase with
        | ChatPhase.Empty kind ->
            match kind with
            | IdleEmptyKind.NotConnected ->
                let title = "先连接一台万象主机"
                let hint = "主机是运行万象的那台电脑：模型、密钥和对话都在它上面。"
                let logo = brandLogo |> Option.map (fun f -> f 64.0) |> Option.defaultWith (fun () -> Border(Width = 64.0, Height = 64.0) :> Control)
                this.ShowBrand(logo, title, hint, onPrimary)
            | IdleEmptyKind.ConnectionFailed reason ->
                let title = "没能连上主机"
                let hint = sprintf "%s。请确认主机在运行、网络可达，然后重新连接。" reason
                let logo = brandLogo |> Option.map (fun f -> f 64.0) |> Option.defaultWith (fun () -> Border(Width = 64.0, Height = 64.0) :> Control)
                this.ShowBrand(logo, title, hint, onPrimary)
            | IdleEmptyKind.NoProvider ->
                let title = "还没有可用的模型"
                let hint = "添加一个模型服务并填入密钥，就可以开始对话了。"
                let logo = brandLogo |> Option.map (fun f -> f 64.0) |> Option.defaultWith (fun () -> Border(Width = 64.0, Height = 64.0) :> Control)
                this.ShowBrand(logo, title, hint, onPrimary)
            | IdleEmptyKind.NoConversation ->
                let title = "今天想讨论什么？"
                let hint = "发出第一条消息后，会自动建立对话。"
                this.ShowPrompt(title, hint, onPrimary)
            | IdleEmptyKind.EmptyConversation ->
                let title = "开始这段对话"
                let hint = "输入问题，或附上一份文件。"
                this.ShowPrompt(title, hint, onPrimary)
        | _ ->
            this.Hide()
