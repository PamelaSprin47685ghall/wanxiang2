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
            // 探针文案在这里归成三态：解析失败的写法按未知处理，不猜。
            Probe = ProbeState.ofNote o.probeNote
        }

    new(theme: ThemeManager, overlay: OverlayDesk) =
        let check =
            Some(fun () -> IconSet.image Glyph.Check (theme.Brush PaletteKey.Accent) (theme.Metric Metric.IconGlyph))

        ModelSwitchDesk(check, overlay)

    /// 只建浮层内容（不进浮层层）
    member _.Build(options: ModelOption list, title: string, pick: ModelOption -> unit) : Control =
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
            // 先选出再关。关浮层会拆掉内容，拆掉之后回调里的选择就发不出去。
            // 按服务与模型名找回原项，不靠记录哈希。
            let picked =
                options
                |> List.tryFind (fun o -> o.providerId = choice.ProviderId && o.model = choice.Model)
                |> Option.filter (fun o -> ModelPickerList.shouldPick o.isCurrent)

            overlay.ClosePopup()
            picked |> Option.iter pick)

    /// 在锚点旁打开（宽 300）
    member this.Show
        (anchor: Control, alignRight: bool, options: ModelOption list, title: string, pick: ModelOption -> unit)
        =
        overlay.Layer.ShowPopup(anchor, this.Build(options, title, pick), alignRight, 300.0)
