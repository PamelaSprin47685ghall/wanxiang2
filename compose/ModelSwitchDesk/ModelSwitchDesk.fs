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

/// 快速切换模型（决策 240）的接线：搜索走 ModelSwitcher.filter，分组走 ModelPickerList.groups；
/// 任何激活（点行、行上 Enter/Space、搜索框回车选第一条）都先关浮层，
/// 选中的是当前模型就到此为止（ModelPickerList.shouldPick），否则才交给 `pick`。
type ModelSwitchDesk(checkIcon: (unit -> Control) option, overlay: OverlayDesk) =
    let toChoice (o: ModelOption) =
        {
            ProviderId = o.providerId
            ProviderLabel = o.providerLabel
            Model = o.model
            IsCurrent = o.isCurrent
            IsDefault = o.isDefault
        }

    new(theme: ThemeManager, overlay: OverlayDesk) =
        let check =
            Some(fun () -> IconSet.image Glyph.Check (theme.Brush PaletteKey.Accent) (theme.Metric Metric.IconGlyph))

        ModelSwitchDesk(check, overlay)

    /// 只建浮层内容（不进浮层层）
    member _.Build(options: ModelOption list, title: string, pick: ModelOption -> unit) : Control =
        let byChoice = options |> List.map (fun o -> toChoice o, o) |> dict

        let slots =
            {
                Groups =
                    fun query _ ->
                        ModelSwitcher.filter query options
                        |> ModelPickerList.groups (fun (o: ModelOption) -> o.providerLabel) query
                        |> List.map (fun (label, items) -> label, items |> List.map toChoice)
                SearchHelp = ModelPickerList.searchHelp
                EmptyText = ModelPickerList.emptyText
                AppliesNote = ModelSwitcher.appliesNote
            }

        ModelSwitchPanel.build checkIcon slots (options |> List.map toChoice) title (fun choice ->
            overlay.ClosePopup()

            match byChoice.TryGetValue choice with
            | true, o when ModelPickerList.shouldPick o.isCurrent -> pick o
            | _ -> ())

    /// 在锚点旁打开（宽 300）
    member this.Show
        (anchor: Control, alignRight: bool, options: ModelOption list, title: string, pick: ModelOption -> unit)
        =
        overlay.Layer.ShowPopup(anchor, this.Build(options, title, pick), alignRight, 300.0)
