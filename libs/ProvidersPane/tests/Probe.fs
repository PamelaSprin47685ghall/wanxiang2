namespace Wanxiang2.ProvidersPane.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.Media
open Avalonia.Threading

/// 旧测试的查找与点击辅助（按逻辑树找，按读屏 Invoke 点）
module Probe =
    let rec descendants (c: obj) : Control seq =
        seq {
            match c with
            | :? ILogical as l ->
                for child in l.LogicalChildren do
                    match child with
                    | :? Control as cc ->
                        yield cc
                        yield! descendants cc
                    | _ -> ()
            | _ -> ()
        }

    let byId (root: Control) (id: string) =
        descendants root |> Seq.tryFind (fun c -> AutomationProperties.GetAutomationId c = id)

    let named (root: Control) (name: string) =
        descendants root |> Seq.tryFind (fun c -> AutomationProperties.GetName c = name)

    let textOf (root: Control) =
        let own =
            match root with
            | :? TextBlock as t -> [ t.Text ]
            | :? TextBox as t -> [ t.Text ]
            | _ -> []
        let inner =
            descendants root
            |> Seq.choose (fun c ->
                match c with
                | :? TextBlock as t -> Some t.Text
                | :? TextBox as t -> Some t.Text
                | _ -> None)
            |> List.ofSeq
        String.Join("\n", own @ inner |> List.filter (fun s -> not (isNull s)))

    /// 经读屏 Invoke 点击（与读屏用户同一路径），再跑完排队的工作
    let click (c: Control) =
        let peer = ControlAutomationPeer.CreatePeerForElement c
        ((box peer) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()

    let host (c: Control) =
        let w = Window(Content = c, Width = 900.0, Height = 900.0)
        w.Show()
        Dispatcher.UIThread.RunJobs()
        w

/// 测试用外观：公共字段全是 Cell，测试可改值验证活性
module Looks =
    let brush (c: Color) = SolidColorBrush(c) :> IBrush
    let glyph (name: string) (b: IBrush) : Control = TextBlock(Text = "[" + name + "]", Foreground = b) :> Control
