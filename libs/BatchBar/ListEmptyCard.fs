namespace Wanxiang2.ListEmptyCard

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 卡的样式
[<RequireQualifiedAccess>]
type EmptyCardStyle =
    /// 一句说明 + 骨架 + 行动键
    | Note
    /// 品牌标 + 标题 + 骨架 + 行动键
    | Brand
    /// 标题 + 说明
    | Plain

/// 列表空态卡。
/// 废除 Look 记录，采用原生 Button、样式类与语义化布局。
type ListEmptyCard() =
    let action = Event<unit>()
    let mutable current: EmptyCardStyle option = None

    let host = Border(IsVisible = false, VerticalAlignment = VerticalAlignment.Center)

    let skeleton =
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        panel.IsVisible <- false
        panel.Margin <- Thickness(0.0, 8.0, 0.0, 0.0)
        for fraction in [ 0.85; 0.60; 0.72 ] do
            let bar = Border(HorizontalAlignment = HorizontalAlignment.Stretch)
            bar.Classes.Add "surface"
            bar.Height <- 14.0
            bar.CornerRadius <- CornerRadius 4.0
            bar.Opacity <- 0.6
            let row = Grid(HorizontalAlignment = HorizontalAlignment.Stretch)
            row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(fraction, GridUnitType.Star)))
            row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(max 0.01 (1.0 - fraction), GridUnitType.Star)))
            Grid.SetColumn(bar, 0)
            row.Children.Add bar
            panel.Children.Add row
        AutomationProperties.SetName(panel, "正在加载对话")
        panel

    let button =
        let b = Button(Content = "")
        b.Classes.Add "primary"
        b.Classes.Add "button-primary"
        b.HorizontalAlignment <- HorizontalAlignment.Center
        b.IsVisible <- false
        b.Click.Add(fun _ -> action.Trigger())
        b

    let detach (c: Control) =
        match c.Parent with
        | :? Panel as p -> p.Children.Remove c |> ignore
        | _ -> ()

    let centered (t: TextBlock) =
        t.HorizontalAlignment <- HorizontalAlignment.Center
        t.TextAlignment <- TextAlignment.Center
        t.TextWrapping <- TextWrapping.Wrap
        t

    let build style (title: string) (note: string) =
        detach skeleton
        detach button
        match style with
        | EmptyCardStyle.Note ->
            let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
            let noteBlock = TextBlock(Text = note)
            stack.Children.Add(centered noteBlock)
            stack.Children.Add skeleton
            stack.Children.Add button
            stack.Margin <- Thickness 16.0
            stack :> Control
        | EmptyCardStyle.Brand ->
            let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
            if not (String.IsNullOrWhiteSpace title) then
                let titleBlock = TextBlock(Text = title, FontWeight = FontWeight.Bold)
                stack.Children.Add(centered titleBlock)
            if not (String.IsNullOrWhiteSpace note) then
                let noteBlock = TextBlock(Text = note)
                stack.Children.Add(centered noteBlock)
            stack.Children.Add skeleton
            stack.Children.Add button
            stack :> Control
        | EmptyCardStyle.Plain ->
            let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
            let titleBlock = TextBlock(Text = title, FontWeight = FontWeight.SemiBold)
            let noteBlock = TextBlock(Text = note)
            stack.Children.Add(centered titleBlock)
            stack.Children.Add(centered noteBlock)
            stack :> Control

    do
        host.Margin <- Thickness(24.0, 48.0, 24.0, 48.0)

    member _.View = host :> Control
    member _.ActionButton = button :> Control
    member _.Skeleton = skeleton :> Control
    member _.Style = current
    member _.IsVisible = host.IsVisible

    /// 显示：样式翻转才换卡，同样式只刷可见性
    member _.Show(style: EmptyCardStyle, title: string, note: string, skeletonVisible: bool, actionVisible: bool) =
        if current <> Some style then
            current <- Some style
            host.Child <- build style title note
        skeleton.IsVisible <- skeletonVisible
        button.IsVisible <- actionVisible
        host.IsVisible <- true

    member _.Hide() = host.IsVisible <- false

    member _.FocusAction() =
        if button.IsEffectivelyVisible && button.IsEnabled then
            button.Focus Avalonia.Input.NavigationMethod.Directional |> ignore
            true
        else false

    /// 单入口渲染
    member this.Render(kind: SidebarEmptyKind option) =
        match kind with
        | None -> this.Hide()
        | Some (SidebarEmptyKind.NotConnected actionVis) ->
            this.Show(EmptyCardStyle.Note, "", "连接主机后，对话记录会显示在这里。", false, actionVis)
        | Some SidebarEmptyKind.Loading ->
            this.Show(EmptyCardStyle.Brand, "正在加载对话…", "", true, false)
        | Some SidebarEmptyKind.NoConversations ->
            this.Show(EmptyCardStyle.Plain, "还没有对话", "发送第一条消息后，对话会显示在这里。", false, false)
        | Some (SidebarEmptyKind.NoSearchResults _) ->
            this.Hide()

    [<CLIEvent>] member _.ActionClicked = action.Publish
