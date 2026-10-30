module Wanxiang2.AppShell.Tests.A11yTraversal

// 整棵无障碍树遍历断言（决策 G5）：把每个主屏幕的控件树走一遍，
// 要求**每个可聚焦控件**都有可访问名称（WCAG 4.1.2 Name）与一个角色（control type）。
//
// 判定口径：
//   可聚焦 = Focusable && 沿祖先链可见 && 沿祖先链可用
//             （等价于 `libs/FocusOrigin` 的 `focusables`：Focusable && IsEffectivelyVisible && IsEffectivelyEnabled）
//   名称   = 自动化 peer 的 GetName()，为空时退回 AutomationProperties.Name
//   角色   = 自动化 peer 的 GetAutomationControlType()；没有 peer 就没有角色
// 遍历按控件树走（视觉树 + 逻辑树的并集）：弹层内容不在工作区的视觉树里，
// 必须逐个屏幕单独走。

open System
open System.Threading.Tasks
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Automation.Peers
open Avalonia.Automation.Peers
open Avalonia.LogicalTree
open Avalonia.VisualTree
open Xunit
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.SettingsScreen

/// 一棵树的节点集合：视觉树后代与逻辑树后代的并集（去重）。
let private descendantsOf (root: Control) : Control list =
    let visual =
        root.GetVisualDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Control as c -> Some c
            | _ -> None)

    let logical =
        (root :> ILogical).GetLogicalDescendants()
        |> Seq.choose (fun v ->
            match v with
            | :? Control as c -> Some c
            | _ -> None)

    let seen = System.Collections.Generic.HashSet<Control>()

    [
        for c in Seq.append visual logical do
            if seen.Add c then
                yield c
    ]

/// 一棵树的节点：控件 + 树路径 + 是否沿祖先链可见可用。
type private Node =
    {
        Control: Control
        Path: string
        Visible: bool
        Enabled: bool
    }

/// 走一遍控件树。
let private walk (root: Control) : Node list =
    let label (c: Control) =
        let n = c.GetType().Name

        if n.EndsWith("Panel", StringComparison.Ordinal) then
            "Panel"
        elif n.EndsWith("Border", StringComparison.Ordinal) then
            "Border"
        elif n.EndsWith("Button", StringComparison.Ordinal) then
            "Button"
        else
            n

    let all = root :: descendantsOf root
    let members = System.Collections.Generic.HashSet<Control>()

    for c in all do
        members.Add c |> ignore

    /// 最近的在树内的祖先：视觉树优先，其次逻辑树。
    let parentOf (c: Control) : Control option =
        let vp =
            match c.GetVisualParent() with
            | :? Control as p -> Some p
            | _ -> None

        let lp =
            match (c :> ILogical).LogicalParent with
            | :? Control as p -> Some p
            | _ -> None

        let inTree (p: Control) = members.Contains p

        match vp, lp with
        | Some a, Some b when Object.ReferenceEquals(a, b) -> Some a
        | Some a, Some b ->
            if inTree a then Some a
            else if inTree b then Some b
            else None
        | Some a, None -> if inTree a then Some a else None
        | None, Some b -> if inTree b then Some b else None
        | None, None -> None

    let pathOf (c: Control) =
        let rec up (c: Control) (acc: string list) =
            match parentOf c with
            | Some p -> up p ((label c) :: acc)
            | None -> acc

        up c [] |> String.concat "/"

    let rec visibleOf (c: Control) =
        match parentOf c with
        | Some p -> c.IsVisible && visibleOf p
        | None -> c.IsVisible

    let rec enabledOf (c: Control) =
        match parentOf c with
        | Some p -> c.IsEnabled && enabledOf p
        | None -> c.IsEnabled

    [
        for c in all do
            {
                Control = c
                Path = pathOf c
                Visible = visibleOf c
                Enabled = enabledOf c
            }
    ]

/// 可聚焦判定（与 OverlayLayer.focusables 同口径）。
let private focusable (n: Node) =
    n.Control.Focusable && n.Visible && n.Enabled

/// 自动化 peer：没有 peer 的控件对辅助技术没有角色。
let private peerOf (c: Control) =
    try
        ControlAutomationPeer.CreatePeerForElement(c)
    with _ ->
        null

/// 可访问名称：peer 的名字优先，其次 AutomationProperties.Name。
/// 自动派生的类型名（如 "Avalonia.Controls.Grid"）不算名字：读屏念出来没有意义。
let private looksLikeTypeName (s: string) =
    s.StartsWith("Avalonia.", StringComparison.Ordinal)
    || s.StartsWith("Wanxiang", StringComparison.Ordinal)
    || s.EndsWith("Control", StringComparison.Ordinal)
    || s.EndsWith("Panel", StringComparison.Ordinal)
    || s.EndsWith("Presenter", StringComparison.Ordinal)

let private accessibleName (c: Control) =
    let peer = peerOf c

    let peerName =
        if isNull peer then
            ""
        else
            try
                match peer.GetName() with
                | null -> ""
                | s -> s
            with _ ->
                ""

    let explicitName =
        let n = AutomationProperties.GetName(c)
        if isNull n then "" else n

    let pick (s: string) =
        let t = s.Trim()

        if String.IsNullOrWhiteSpace t || looksLikeTypeName t then
            None
        else
            Some t

    match pick peerName with
    | Some n -> n
    | None ->
        match pick explicitName with
        | Some n -> n
        | None -> ""

/// 角色：peer 的 control type；没有 peer → 没有角色。
let private roleOf (c: Control) =
    let peer = peerOf c

    if isNull peer then
        None
    else
        try
            let r = peer.GetAutomationControlType()
            Some(string r)
        with _ ->
            None

/// 一棵树的违例清单：每条形如「屏幕 / 路径 / 类型 / 原因」。
let private violationsOf (screen: string) (root: Control) =
    [
        for n in walk root do
            if focusable n then
                let name = accessibleName n.Control
                let role = roleOf n.Control

                if String.IsNullOrWhiteSpace name then
                    yield sprintf "%s / %s / %s / 缺少可访问名称" screen n.Path (n.Control.GetType().Name)
                else
                    match role with
                    | None -> yield sprintf "%s / %s / %s / 缺少角色（control type）" screen n.Path (n.Control.GetType().Name)
                    | Some r when r.Equals("None", StringComparison.OrdinalIgnoreCase) ->
                        yield sprintf "%s / %s / %s / 角色为 None（未暴露给辅助技术）" screen n.Path (n.Control.GetType().Name)
                    | Some r when r.Equals("Custom", StringComparison.OrdinalIgnoreCase) ->
                        yield sprintf "%s / %s / %s / 角色为 Custom（未指定具体角色）" screen n.Path (n.Control.GetType().Name)
                    | _ -> ()
    ]

// ---------- 各主屏幕的控件树 ----------

let private paletteItems: PaletteItem list =
    [
        {
            id = "act:a11y"
            kind = PaletteAction
            title = "测试动作"
            subtitle = "无障碍遍历用条目"
            keywords = []
            shortcut = None
        }
    ]

let private modelOptions: ModelOption list =
    [
        {
            providerId = "p1"
            providerLabel = "本地服务"
            model = "test-model"
            isCurrent = true
            isDefault = true
            probeNote = Some "未探测"
        }
    ]

/// 全部主屏幕的控件树。每一项是 (屏幕名, 控件树根)。
let private screens (rig: Rig) : (string * Control) list =
    let views = rig.Views

    let paletteContent, _ =
        views.Palette.Build((fun () -> paletteItems), (fun () -> []), (fun _ _ -> ()))

    let modelContent = views.ModelSwitch.Build(modelOptions, "切换模型", (fun _ -> ()))
    let prompt = views.Dialogs.Prompt("重命名", "输入新的名字", "旧名字", "保存", (fun _ _ -> ()))
    let confirm = views.Dialogs.Confirm("确认删除", "删除后不可恢复。", "删除", (fun () -> ()))
    let editText = views.Dialogs.EditText("编辑消息", "原文", "保存", (fun _ -> ()))

    let connect =
        views.Dialogs.Connect("ws://127.0.0.1:1/ws", (fun _ -> ()), (fun _ -> ()))

    let session =
        views.Dialogs.SessionSettings(Catalog.empty, SessionConfig.empty, (fun _ _ -> ()))

    let shortcuts = views.Dialogs.Shortcuts()

    let exportDesk =
        views.NewExportDesk
            ((fun _ -> Task.FromResult true))
            ConversationTransfer.rootLineage
            (Guid "11111111-1111-1111-1111-111111111111")
            "导出用对话"

    let importDesk = views.NewImportDesk(fun _ _ -> ())

    [
        yield "工作区", views.Workspace.Host
        yield "侧栏", views.Sidebar.View
        yield "对话区", views.Chat.Host
        yield "输入区", views.Composer.Host
        yield "审批区", views.Approval.View
        yield "起步引导", views.Onboarding.View
        yield "命令面板", paletteContent
        yield "模型切换浮层", modelContent
        yield "提示条", views.Overlay.Toasts.View
        yield "重命名对话框", prompt.View
        yield "确认对话框", confirm.View
        yield "编辑消息对话框", editText.View
        yield "连接对话框", connect.Pane.View
        yield "对话设置对话框", session.View
        yield "快捷键帮助", shortcuts.View
        yield "导出面板", exportDesk.Sheet.View
        yield "导入面板", importDesk.Sheet.View
        for part in SettingsPart.all do
            views.Settings.ShowSection part
            yield sprintf "设置页 / %s" (SettingsPart.label part), views.Settings.View
    ]

[<Fact>]
let ``每个主屏幕的每个可聚焦控件都有可访问名称与角色`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()

        let all =
            [
                for (screen, root) in screens rig do
                    yield! violationsOf screen root
            ]

        Assert.True(all.IsEmpty, String.Join("\n", all)))
