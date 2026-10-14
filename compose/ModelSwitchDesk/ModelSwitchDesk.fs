namespace Wanxiang2.ModelSwitchDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.IconSet
open Wanxiang2.OverlayDesk
open Wanxiang2.ModelSwitchPanel

/// 从主题管理者取切换器的外观。
module ModelSwitchLook =
    let ofTheme (t: ThemeManager) : ModelSwitchPanelLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let xl = t.Space Space.Xl
        let xs = t.Space Space.Xs
        let pad x y = Live.map2 (fun a b -> Thickness(a, b)) x y
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          TagText = brush PaletteKey.TextMuted
          TagSurface = brush PaletteKey.SurfaceContainer
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          Caret = brush PaletteKey.Accent
          Check = fun b -> IconSet.image Glyph.Check b (metric Metric.IconGlyph)
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          TagRadius = Live.map CornerRadius (space Space.RadiusPill)
          TagPadding = pad (t.Metric Metric.ChipPaddingX) (t.Metric Metric.ChipPaddingY)
          ItemPadding = pad xl (t.Metric Metric.MenuItemPaddingY)
          ItemRadius = Live.map CornerRadius (space Space.RadiusSm)
          ItemMinHeight = metric Metric.MenuItemMinHeight
          ItemSpacing = space Space.XXs
          ColumnSpacing = space Space.Md
          MarkSpacing = space Space.Xs
          StackSpacing = space Space.Xs
          HeaderMargin = Live.map2 (fun x y -> Thickness(x, y, x, xs.Value)) xl (t.Space Space.Md)
          EmptyMargin = pad xl (t.Space Space.Md)
          HeadingMargin = Live.map2 (fun x y -> Thickness(x, y, x, 0.0)) xl xs
          NoteMargin = Live.map2 (fun x y -> Thickness(x, y, x, y)) xl xs
          FieldMargin = Live.map (fun v -> Thickness(v, v, v, 0.0)) (space Space.Xs)
          ListMaxHeight = metric Metric.MenuMaxHeight
          PanelWidth = metric Metric.ModelSwitcherWidth
          FieldPadding = pad xl (t.Metric Metric.TextFieldPaddingY)
          FieldRadius = Live.map CornerRadius (space Space.RadiusLg)
          FieldEdgeWidth = Live.map Thickness (metric Metric.BorderWidth)
          FieldTextMinHeight = metric Metric.TextFieldTextMinHeight }

/// 快速切换模型（决策 240）的接线：搜索走 ModelSwitcher.filter，分组走 ModelPickerList.groups；
/// 任何激活（点行、行上 Enter/Space、搜索框回车选第一条）都先关浮层，
/// 选中的是当前模型就到此为止（ModelPickerList.shouldPick），否则才交给 `pick`。
type ModelSwitchDesk(look: ModelSwitchPanelLook, overlay: OverlayDesk) =
    let toChoice (o: ModelOption) =
        { ProviderId = o.providerId; ProviderLabel = o.providerLabel; Model = o.model; IsCurrent = o.isCurrent; IsDefault = o.isDefault }

    new(theme: ThemeManager, overlay: OverlayDesk) = ModelSwitchDesk(ModelSwitchLook.ofTheme theme, overlay)

    /// 只建浮层内容（不进浮层层）
    member _.Build(options: ModelOption list, title: string, pick: ModelOption -> unit) : Control =
        let byChoice = options |> List.map (fun o -> toChoice o, o) |> dict
        let slots =
            { Groups =
                fun query _ ->
                    ModelSwitcher.filter query options
                    |> ModelPickerList.groups (fun (o: ModelOption) -> o.providerLabel) query
                    |> List.map (fun (label, items) -> label, items |> List.map toChoice)
              SearchHelp = ModelPickerList.searchHelp
              EmptyText = ModelPickerList.emptyText
              AppliesNote = ModelSwitcher.appliesNote }
        ModelSwitchPanel.build look slots (options |> List.map toChoice) title (fun choice ->
            overlay.ClosePopup()
            match byChoice.TryGetValue choice with
            | true, o when ModelPickerList.shouldPick o.isCurrent -> pick o
            | _ -> ())

    /// 在锚点旁打开（宽 300）
    member this.Show(anchor: Control, alignRight: bool, options: ModelOption list, title: string, pick: ModelOption -> unit) =
        overlay.Layer.ShowPopup(anchor, this.Build(options, title, pick), alignRight, (let mutable w = 0.0 in (look.PanelWidth.Subscribe(fun v -> w <- v)).Dispose(); w))
