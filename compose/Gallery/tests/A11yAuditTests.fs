namespace Wanxiang2.Gallery.Tests

open System
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Threading
open Avalonia
open Xunit
open Wanxiang2.Gallery

module A11yAuditTests =

    let private renderEntryControl (name: string) : Control * Window =
        let entry = Gallery.find name |> Option.get
        let ctx = Gallery.createContextMode Wanxiang2.ThemePalette.PaletteMode.Light
        let control = Gallery.build ctx entry

        let win =
            Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

        win.Show()
        win.UpdateLayout()
        control.InvalidateVisual()
        win.UpdateLayout()
        control, win

    [<Theory>]
    [<InlineData("ComposerBox.attachments")>]
    [<InlineData("MessageCardView.user")>]
    [<InlineData("AttachmentTray.items")>]
    [<InlineData("FieldCheck.invalid")>]
    [<InlineData("ApprovalPanel.approval")>]
    let ``交互条目必须呈现真实控件且没有无障碍缺陷`` (name: string) =
        Headless.run (fun () ->
            let _, win = renderEntryControl name

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items
                Assert.Empty(A11yAudit.defectsOf items)
            finally
                win.Close())

    // ---------------------------------------------------------------- 画廊真实控件树遍历断言 (G5)

    [<Fact>]
    let ``画廊真实控件树遍历：输入区所有可聚焦可交互控件均有有效原生 peer 与非空名称`` () =
        Headless.run (fun () ->
            let _, win = renderEntryControl "ComposerBox.typing"

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items

                for item in items do
                    Assert.True(item.Defect.IsNone, sprintf "受检控件 %s 出现缺陷: %A" item.ControlType item.Defect)

                    Assert.False(
                        String.IsNullOrWhiteSpace item.AccessibleName,
                        sprintf "控件 %s 的可访问名称为空" item.ControlType
                    )

                    Assert.NotEqual(AutomationControlType.None, item.Role)

                A11yAudit.assertClean items
            finally
                win.Close())

    [<Fact>]
    let ``画廊真实控件树遍历：对话列所有卡片按钮与动作均有有效原生 peer 与非空名称`` () =
        Headless.run (fun () ->
            let _, win = renderEntryControl "MessageCardView.toolcall"

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items
                A11yAudit.assertClean items
            finally
                win.Close())

    [<Fact>]
    let ``画廊真实控件树遍历：侧栏行与批量条所有操作控件均有有效原生 peer 与非空名称`` () =
        Headless.run (fun () ->
            let _, win = renderEntryControl "SidebarDesk.batch"

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items
                A11yAudit.assertClean items
            finally
                win.Close())

    [<Fact>]
    let ``画廊真实控件树遍历：设置页面板控件均有有效原生 peer 与非空名称`` () =
        Headless.run (fun () ->
            let _, win = renderEntryControl "SettingsScreen.providers"

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items
                A11yAudit.assertClean items
            finally
                win.Close())

    [<Fact>]
    let ``画廊真实控件树遍历：对话框确认取消键均有有效原生 peer 与非空名称`` () =
        Headless.run (fun () ->
            let _, win = renderEntryControl "OverlayDesk.dialog.confirm"

            try
                let items = A11yAudit.auditWindow win
                Assert.NotEmpty items
                A11yAudit.assertClean items
            finally
                win.Close())

    // ---------------------------------------------------------------- 活动弹窗与 PopupRoot 浮层审查 (G5 扩展)

    [<Fact>]
    let ``活动弹窗与浮层审查：真实打开的 ContextMenu 弹出层中至少一个 MenuItem 被扫描且名称角色正确`` () =
        Headless.run (fun () ->
            // 构造承载 Window 与带有 ContextMenu 的目标控件
            let target = Button(Content = "操作目标")
            let menu = ContextMenu()
            AutomationProperties.SetName(menu, "操作菜单")
            let itemCopy = MenuItem(Header = "复制选区")
            let itemDelete = MenuItem(Header = "删除内容")
            menu.ItemsSource <- [ itemCopy; itemDelete ]
            target.ContextMenu <- menu

            let win = Window(Width = 400.0, Height = 300.0, Content = target)
            win.Show()
            win.UpdateLayout()

            // 真实打开菜单，触发 Avalonia PopupRoot / 浮层挂载
            menu.Open target
            Dispatcher.UIThread.RunJobs()
            Assert.True(menu.IsOpen, "ContextMenu 应当处于真实打开态")

            try
                // 审查承载 Window（必须穿透扫描到活动的 PopupRoot / OverlayLayer 浮层）
                let items = A11yAudit.auditWindow win

                // 断言至少一个 MenuItem 被扫描到
                let menuItems =
                    items |> List.filter (fun i -> i.Role = AutomationControlType.MenuItem)

                Assert.NotEmpty menuItems
                Assert.True(menuItems.Length >= 2, sprintf "期望扫描到至少 2 个 MenuItem，实际扫描到 %d 个" menuItems.Length)

                // 断言扫描到的 MenuItem 角色与名称均正确
                let scannedCopy = menuItems |> List.tryFind (fun i -> i.AccessibleName = "复制选区")
                Assert.True(scannedCopy.IsSome, "应当扫描到名称为'复制选区'的 MenuItem")
                Assert.Equal(AutomationControlType.MenuItem, scannedCopy.Value.Role)
                Assert.True(scannedCopy.Value.Defect.IsNone)

                let scannedDelete = menuItems |> List.tryFind (fun i -> i.AccessibleName = "删除内容")
                Assert.True(scannedDelete.IsSome, "应当扫描到名称为'删除内容'的 MenuItem")
                Assert.Equal(AutomationControlType.MenuItem, scannedDelete.Value.Role)
                Assert.True(scannedDelete.Value.Defect.IsNone)

                A11yAudit.assertClean items
            finally
                menu.Close()
                win.Close())

    [<Fact>]
    let ``活动弹窗与浮层审查：真实打开的 MenuFlyout 弹出层中至少一个 MenuItem 被扫描且名称角色正确`` () =
        Headless.run (fun () ->
            // 构造带有 MenuFlyout 的按钮
            let target = Button(Content = "更多选项")
            let flyout = MenuFlyout()
            let itemExport = MenuItem(Header = "导出对话")
            let itemSettings = MenuItem(Header = "偏好设置")
            flyout.Items.Add(itemExport) |> ignore
            flyout.Items.Add(itemSettings) |> ignore
            target.Flyout <- flyout

            let win = Window(Width = 400.0, Height = 300.0, Content = target)
            win.Show()
            win.UpdateLayout()

            // 真实打开 Flyout
            flyout.ShowAt target
            Dispatcher.UIThread.RunJobs()
            Assert.True(flyout.IsOpen, "MenuFlyout 应当处于真实打开态")

            try
                let items = A11yAudit.auditWindow win

                let menuItems =
                    items |> List.filter (fun i -> i.Role = AutomationControlType.MenuItem)

                Assert.NotEmpty menuItems

                let scannedExport = menuItems |> List.tryFind (fun i -> i.AccessibleName = "导出对话")
                Assert.True(scannedExport.IsSome, "应当穿透扫描到 MenuFlyout 中的 MenuItem")
                Assert.Equal(AutomationControlType.MenuItem, scannedExport.Value.Role)
                Assert.True(scannedExport.Value.Defect.IsNone)

                A11yAudit.assertClean items
            finally
                flyout.Hide()
                win.Close())

    // ---------------------------------------------------------------- 原生 Peer 角色与名称证明

    [<Fact>]
    let ``原生 peer 角色与名称证明：peer.GetName() 优于只读 AutomationProperties`` () =
        Headless.run (fun () ->
            // 构造一个带有原生 Text 内容的 Button，未显式调用 AutomationProperties.SetName
            let btn = Button(Content = "确认提交")
            let win = Window(Content = btn)
            win.Show()
            win.UpdateLayout()

            // 1. 只读 AutomationProperties 为 null（未显式设附加属性）
            let rawPropName = AutomationProperties.GetName btn
            Assert.Null rawPropName

            // 2. 原生 AutomationPeer 能依据 Content 正确派生有效可访问名称
            let item = A11yAudit.auditControl btn
            Assert.Equal(AutomationControlType.Button, item.Role)
            Assert.Equal("确认提交", item.AccessibleName)
            Assert.True(item.Defect.IsNone)

            win.Close())

    // ---------------------------------------------------------------- 无名负例 (Negative Tests)

    [<Fact>]
    let ``无名负例 1：无内容且无名称的按钮被审查判定为 MissingAccessibleName 缺陷`` () =
        Headless.run (fun () ->
            let badBtn = Button(Width = 32.0, Height = 32.0)
            let win = Window(Content = badBtn)
            win.Show()
            win.UpdateLayout()

            let item = A11yAudit.auditControl badBtn

            match item.Defect with
            | Some(A11yDefect.MissingAccessibleName(ctlType, role, _)) ->
                Assert.Equal("Button", ctlType)
                Assert.Equal(AutomationControlType.Button, role)
            | other -> failwithf "期望检测出 MissingAccessibleName 缺陷，实际为: %A" other

            win.Close())

    [<Fact>]
    let ``无名负例 2：无提示且无名称的输入框被审查判定为 MissingAccessibleName 缺陷`` () =
        Headless.run (fun () ->
            let badTb = TextBox()
            let win = Window(Content = badTb)
            win.Show()
            win.UpdateLayout()

            let item = A11yAudit.auditControl badTb

            match item.Defect with
            | Some(A11yDefect.MissingAccessibleName(ctlType, role, _)) ->
                Assert.Equal("TextBox", ctlType)
                Assert.Equal(AutomationControlType.Edit, role)
            | other -> failwithf "期望检测出 MissingAccessibleName 缺陷，实际为: %A" other

            win.Close())

    [<Fact>]
    let ``无名负例 3：可聚焦无名自定义元素被审查判定为 MissingAccessibleName 缺陷`` () =
        Headless.run (fun () ->
            let badBorder = Border(Focusable = true, Width = 100.0, Height = 40.0)
            let win = Window(Content = badBorder)
            win.Show()
            win.UpdateLayout()

            let item = A11yAudit.auditControl badBorder

            match item.Defect with
            | Some(A11yDefect.MissingAccessibleName _)
            | Some(A11yDefect.InvalidRole _) -> ()
            | other -> failwithf "期望检测出无名可聚焦元素缺陷，实际为: %A" other

            win.Close())

    [<Fact>]
    let ``无名负例 4：纯空白名称被审查判定为缺陷，不能蒙混过关`` () =
        Headless.run (fun () ->
            let wsBtn = Button(Content = "    ")
            let win = Window(Content = wsBtn)
            win.Show()
            win.UpdateLayout()

            let item = A11yAudit.auditControl wsBtn
            Assert.True(item.Defect.IsSome)

            match item.Defect.Value with
            | A11yDefect.MissingAccessibleName _ -> ()
            | other -> failwithf "期望检测出空白名称缺陷，实际为: %A" other

            win.Close())

    [<Fact>]
    let ``无名负例 5：真实打开的弹窗浮层中无名 MenuItem 必须被检出缺陷并亮红灯`` () =
        Headless.run (fun () ->
            // 在真实 Window 上打开包含无名 MenuItem 的真实 ContextMenu（非伪造脱节树）
            let target = Button(Content = "右键目标")
            let badMenu = ContextMenu()
            // 故意构造一个 Header 为空且未设 AutomationProperties.Name 的无名 MenuItem
            let badItem = MenuItem(Header = "")
            badMenu.ItemsSource <- [ badItem ]
            target.ContextMenu <- badMenu

            let win = Window(Width = 400.0, Height = 300.0, Content = target)
            win.Show()
            win.UpdateLayout()

            // 真实打开
            badMenu.Open target
            Dispatcher.UIThread.RunJobs()
            Assert.True(badMenu.IsOpen, "ContextMenu 应当处于真实打开态")

            try
                // 审查整个 Window（必须穿透扫描到活动 PopupRoot 浮层）
                let items = A11yAudit.auditWindow win

                // 找到该 MenuItem
                let badScanned = items |> List.tryFind (fun i -> i.Control = (badItem :> Control))
                Assert.True(badScanned.IsSome, "auditWindow 必须穿透扫描到真实活动 PopupRoot 中的无名 MenuItem")
                Assert.Equal(AutomationControlType.MenuItem, badScanned.Value.Role)
                Assert.True(badScanned.Value.Defect.IsSome, "无名 MenuItem 必须被判定存在缺陷")

                match badScanned.Value.Defect.Value with
                | A11yDefect.MissingAccessibleName(ctl, role, _) ->
                    Assert.Equal("MenuItem", ctl)
                    Assert.Equal(AutomationControlType.MenuItem, role)
                | other -> failwithf "期望 MissingAccessibleName，实际为: %A" other

                // assertClean 必须抛出包含 MenuItem 缺陷的异常阻断
                let ex = Assert.Throws<Exception>(fun () -> A11yAudit.assertClean items)
                Assert.Contains("无障碍审查失败", ex.Message)
                Assert.Contains("MenuItem", ex.Message)
                Assert.Contains("MissingAccessibleName", ex.Message)
            finally
                badMenu.Close()
                win.Close())

    [<Fact>]
    let ``负例审查失败判定：存在缺陷时 assertClean 必须抛出异常亮红灯`` () =
        Headless.run (fun () ->
            let badBtn = Button(Width = 32.0, Height = 32.0)
            let item = A11yAudit.auditControl badBtn
            let ex = Assert.Throws<Exception>(fun () -> A11yAudit.assertClean [ item ])
            Assert.Contains("无障碍审查失败", ex.Message)
            Assert.Contains("MissingAccessibleName", ex.Message))
