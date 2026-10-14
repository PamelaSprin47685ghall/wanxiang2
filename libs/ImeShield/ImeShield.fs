namespace Wanxiang2.ImeShield

open System
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.VisualTree

/// 决策 384：输入法组合（拼音/五笔候选）期间，回车、上下键、Esc、Tab 归输入法，
/// 绝不触发发送/提交/历史召回/关闭（迁自旧 Controls/ImeGuard.fs）。
/// 全应用唯一守卫：TextBox 隧道阶段的类处理器，早于任何视图自己挂的 KeyDown。
/// 判据：TextPresenter.PreeditText 非空，或平台把按键标成 Key.ImeProcessed。
/// 本控件没有外观，因此没有 Look 记录。
module ImeShield =
    let presenterOf (tb: TextBox) =
        tb.GetVisualDescendants()
        |> Seq.tryPick (function :? TextPresenter as p -> Some p | _ -> None)

    let isComposing (tb: TextBox) =
        presenterOf tb |> Option.exists (fun p -> not (String.IsNullOrEmpty p.PreeditText))

    /// 组合期间归输入法的键。
    let isImeOwnedKey (k: Key) =
        k = Key.Enter || k = Key.Up || k = Key.Down || k = Key.Escape || k = Key.Tab

    /// 是否应吞掉这次按键。
    let shouldSwallow (composing: bool) (k: Key) =
        k = Key.ImeProcessed || (composing && isImeOwnedKey k)

    let private gate = obj ()
    let mutable private installed = false

    /// 幂等：应用初始化时调用一次（桌面与浏览器共用）。
    let install () =
        lock gate (fun () ->
            if not installed then
                installed <- true
                InputElement.KeyDownEvent.AddClassHandler<TextBox>(
                    (fun (tb: TextBox) (e: KeyEventArgs) ->
                        if not e.Handled && shouldSwallow (isComposing tb) e.Key then
                            e.Handled <- true),
                    RoutingStrategies.Tunnel)
                |> ignore)
