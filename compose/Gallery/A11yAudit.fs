namespace Wanxiang2.Gallery

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.VisualTree

/// 控件无障碍审查缺陷
[<RequireQualifiedAccess>]
type A11yDefect =
    /// 缺少 AutomationPeer（未能为可交互/可聚焦控件创建原生 peer）
    | MissingPeer of controlType: string * details: string
    /// 原生 peer 角色无效（如 None）
    | InvalidRole of controlType: string * role: AutomationControlType * details: string
    /// 缺少有效可访问名称（peer.GetName() 返回 null 或纯空白；禁止以只读 AutomationProperties 冒充有效名称）
    | MissingAccessibleName of controlType: string * role: AutomationControlType * details: string

/// 单个控件的审查记录
type A11yAuditItem =
    {
        Control: Control
        ControlType: string
        Role: AutomationControlType
        AccessibleName: string
        IsFocusable: bool
        Defect: A11yDefect option
    }

module A11yAudit =

    /// 判断控件是否为可交互或可聚焦的无障碍审查受检对象
    let isCandidate (c: Control) : bool =
        if not c.IsEffectivelyVisible then
            false
        else
            c.Focusable
            || c :? Button
            || c :? TextBox
            || c :? ToggleSwitch
            || c :? CheckBox
            || c :? MenuItem
            || c :? ComboBox
            || c :? ListBoxItem

    /// 审查单个控件的无障碍属性：必须通过原生 peer 检查有效角色与名称
    let auditControl (c: Control) : A11yAuditItem =
        let typeName = c.GetType().Name
        let peer = ControlAutomationPeer.CreatePeerForElement c

        if isNull peer then
            {
                Control = c
                ControlType = typeName
                Role = AutomationControlType.None
                AccessibleName = ""
                IsFocusable = c.Focusable
                Defect =
                    Some(
                        A11yDefect.MissingPeer(
                            typeName,
                            sprintf "控件 %s (Focusable=%b) 未创建 AutomationPeer" typeName c.Focusable
                        )
                    )
            }
        else
            let role = peer.GetAutomationControlType()
            let name = peer.GetName()

            let defect =
                if role = AutomationControlType.None then
                    Some(A11yDefect.InvalidRole(typeName, role, sprintf "控件 %s 的原生 peer 角色为 None" typeName))
                elif String.IsNullOrWhiteSpace name then
                    Some(
                        A11yDefect.MissingAccessibleName(
                            typeName,
                            role,
                            sprintf "控件 %s (角色 %A) 的 peer.GetName() 为空或纯空白" typeName role
                        )
                    )
                else
                    None

            {
                Control = c
                ControlType = typeName
                Role = role
                AccessibleName = (if isNull name then "" else name)
                IsFocusable = c.Focusable
                Defect = defect
            }

    /// 遍历单一可视节点及其子孙节点，提取受检控件审查结果
    let private auditVisualTree (root: Visual) : A11yAuditItem list =
        let descendants =
            VisualExtensions.GetVisualDescendants(root)
            |> Seq.choose (function
                | :? Control as c -> Some c
                | _ -> None)
            |> Seq.filter isCandidate
            |> Seq.toList

        let candidates =
            match box root with
            | :? Control as c when isCandidate c -> c :: descendants
            | _ -> descendants

        candidates |> List.map auditControl

    /// 获取承载窗口以及挂载在其实体树上的活动 PopupRoot / OverlayLayer 的全量真实可视根
    let activeRoots (win: Window) : Visual list =
        let roots = ResizeArray<Visual>()
        roots.Add(win :> Visual)

        // 1. 挂在 Window 上的 Avalonia 原生 OverlayLayer
        let overlayLayer = OverlayLayer.GetOverlayLayer(win)

        if not (isNull overlayLayer) then
            roots.Add(overlayLayer :> Visual)

        // 2. 窗口可视后代中的活动 Popup 控件，将其活动 Child 纳入
        let allDescendants = VisualExtensions.GetVisualDescendants(win) |> Seq.toList

        for d in allDescendants do
            match d with
            | :? Popup as p ->
                if p.IsOpen && not (isNull p.Child) then
                    roots.Add(p.Child :> Visual)
            | :? Control as c ->
                // 3. 关联在控件上的 ContextMenu，若处于打开态，将其菜单自身与子项纳入
                if not (isNull c.ContextMenu) && c.ContextMenu.IsOpen then
                    let m = c.ContextMenu
                    roots.Add(m :> Visual)

                    if not (isNull m.ItemsSource) then
                        for item in m.ItemsSource do
                            match item with
                            | :? Control as ctrl -> roots.Add(ctrl :> Visual)
                            | _ -> ()

                    for item in m.Items do
                        match item with
                        | :? Control as ctrl -> roots.Add(ctrl :> Visual)
                        | _ -> ()
                // 4. 关联在控件上的 ContextFlyout / FlyoutBase
                let flyout = FlyoutBase.GetAttachedFlyout(c)

                if not (isNull flyout) && flyout.IsOpen then
                    match flyout with
                    | :? MenuFlyout as mf ->
                        for item in mf.Items do
                            match item with
                            | :? Control as ctrl -> roots.Add(ctrl :> Visual)
                            | _ -> ()
                    | _ -> ()
            | _ -> ()

        roots |> Seq.distinct |> Seq.toList

    /// 审查承载 Window（穿透扫描主内容树、OverlayLayer 与所有活动中的 PopupRoot / 浮层）
    let auditWindow (win: Window) : A11yAuditItem list =
        let roots = activeRoots win

        roots
        |> List.collect auditVisualTree
        |> Seq.distinctBy (fun item -> item.Control)
        |> Seq.toList

    /// 遍历一棵真实控件树，检查所有受检控件。
    /// 若根节点为 Window 或挂载在 Window 内，自动扩为承载 Window 及其活动 PopupRoot / OverlayLayer 的真实全树。
    let auditTree (root: Visual) : A11yAuditItem list =
        match box root with
        | :? Window as w -> auditWindow w
        | _ ->
            match TopLevel.GetTopLevel(root) with
            | :? Window as w -> auditWindow w
            | _ -> auditVisualTree root

    /// 提取所有缺陷项
    let defectsOf (items: A11yAuditItem list) : A11yDefect list =
        items |> List.choose (fun i -> i.Defect)

    /// 格式化审查缺陷诊断信息
    let formatDefects (defects: A11yDefect list) : string =
        defects
        |> List.mapi (fun i d ->
            match d with
            | A11yDefect.MissingPeer(t, msg) -> sprintf "  [%d] MissingPeer: %s (%s)" (i + 1) t msg
            | A11yDefect.InvalidRole(t, r, msg) -> sprintf "  [%d] InvalidRole: %s (角色 %A) - %s" (i + 1) t r msg
            | A11yDefect.MissingAccessibleName(t, r, msg) ->
                sprintf "  [%d] MissingAccessibleName: %s (角色 %A) - %s" (i + 1) t r msg)
        |> String.concat "\n"

    /// 断言无缺陷：如果有任何缺陷，生成可定位的详细诊断并抛出异常
    let assertClean (items: A11yAuditItem list) : unit =
        let defects = defectsOf items

        if not defects.IsEmpty then
            failwithf "无障碍审查失败，发现 %d 处缺陷：\n%s" defects.Length (formatDefects defects)
