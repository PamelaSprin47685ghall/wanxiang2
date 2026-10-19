namespace Wanxiang2.ToolsPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState
open Wanxiang2.DevicesPane

/// 一个可用工具
type ToolsPaneTool =
    { Id: string
      Label: string
      Description: string
      /// 来自 MCP 服务（图标用 server，否则 wrench）
      FromMcp: bool
      /// 当前审批方式的文案（ToolsPolicy.label）
      PolicyLabel: string }

/// 一个 MCP 服务
type ToolsPaneServer =
    { Id: string
      Label: string
      ToolCount: int
      /// 「本地 · npx …」/「远程 · url」/「未配置传输」
      Transport: string
      Enabled: bool }

type ToolsPaneState =
    { Tools: ToolsPaneTool list
      Folders: string list
      SandboxNote: string
      ExposeDebug: bool
      Servers: ToolsPaneServer list }

module internal Empty =
    /// 空态卡（旧 Ui.emptyState 无标题、无动作的形态）
    let card (glyphOf: (string -> IBrush -> Control) option) (hint: string) : Control =
        let logo =
            match glyphOf with
            | Some g -> g "logo" (SolidColorBrush(Colors.Gray) :> IBrush)
            | None ->
                let t = TextBlock(Text = "万", FontSize = 32.0)
                t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                t :> Control
        logo.HorizontalAlignment <- HorizontalAlignment.Center
        let text = Ui.caption hint
        text.TextAlignment <- TextAlignment.Center
        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        p.Children.Add logo
        p.Children.Add text
        Ui.card p :> Control

/// 设置 → 工具分区（迁自旧 Views/SettingsTools.fs 的页面部分）：可用工具与审批方式键、沙箱说明、授权文件夹、调试工具开关、MCP 服务列表。
/// 写回、菜单、对话框由组合者接（ToolsPolicy、McpForm、OverlayDesk、PopupMenu）。
type ToolsPane(?glyphOf: string -> IBrush -> Control) =
    let policyClicked = Event<string * Control>()
    let removeFolder = Event<string>()
    let debugToggled = Event<bool>()
    let addFolder = Event<unit>()
    let addServer = Event<unit>()
    let editServer = Event<string>()
    let serverMore = Event<string * Control>()

    let builtinPanel = StackPanel(Orientation = Orientation.Vertical)
    let mcpPanel = StackPanel(Orientation = Orientation.Vertical)
    let foldersPanel = StackPanel(Orientation = Orientation.Vertical)
    let sandboxNote = Ui.caption ""
    let debugRow, _, writeDebug =
        Ui.switchRow "显示调试工具" "打开后，工具清单里会多出一个只用于排查问题的回显工具。" false (fun v -> debugToggled.Trigger v)

    let rowCard (child: Control) = Ui.card child :> Control

    let renderTool (tool: ToolsPaneTool) : Control =
        let icon =
            match glyphOf with
            | Some g -> g (if tool.FromMcp then "server" else "wrench") (SolidColorBrush(Colors.Gray) :> IBrush)
            | None ->
                let t = TextBlock(Text = (if tool.FromMcp then "[server]" else "[wrench]"))
                t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                t :> Control
        icon.VerticalAlignment <- VerticalAlignment.Top
        let name = TextBlock(Text = tool.Label, FontSize = 14.0)
        name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        name.FontWeight <- FontWeight.Medium
        let identifier = TextBlock(Text = tool.Id, FontSize = 11.0)
        identifier.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
        identifier.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore
        let description = Ui.caption (if String.IsNullOrWhiteSpace tool.Description then "（MCP 服务未提供描述）" else tool.Description)
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        column.Children.Add name
        column.Children.Add identifier
        column.Children.Add description
        let mutable policy: Button = Unchecked.defaultof<_>
        policy <- Ui.button "secondary" tool.PolicyLabel (fun () -> policyClicked.Trigger((tool.Id, policy :> Control)))
        AutomationProperties.SetAutomationId(policy, sprintf "tool-policy-%s" tool.Id)
        AutomationProperties.SetName(policy, sprintf "%s 的审批方式：%s" tool.Label tool.PolicyLabel)
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(policy, Dock.Right)
        dock.Children.Add policy
        let content = StackPanel(Orientation = Orientation.Horizontal, Spacing = 16.0)
        content.Children.Add icon
        content.Children.Add column
        dock.Children.Add content
        rowCard dock

    let renderServer (server: ToolsPaneServer) : Control =
        let name = TextBlock(Text = server.Label, FontSize = 14.0)
        if server.Enabled then name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
        else name.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        name.FontWeight <- FontWeight.Medium
        name.VerticalAlignment <- VerticalAlignment.Center
        let titleRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0, VerticalAlignment = VerticalAlignment.Center)
        titleRow.Children.Add name
        titleRow.Children.Add(Ui.tag TagTone.Neutral (sprintf "%d 个工具" server.ToolCount))
        if not server.Enabled then titleRow.Children.Add(Ui.tag TagTone.Neutral "已停用")
        let meta = TextBlock(Text = server.Transport, FontSize = 11.0)
        meta.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
        meta.TextTrimming <- TextTrimming.CharacterEllipsis
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        column.Children.Add titleRow
        column.Children.Add meta
        let edit = Ui.iconButton "✎" "编辑" (fun () -> editServer.Trigger server.Id)
        let mutable more: Button = Unchecked.defaultof<_>
        more <- Ui.iconButton "⋯" "更多" (fun () -> serverMore.Trigger((server.Id, more :> Control)))
        let actions = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0, VerticalAlignment = VerticalAlignment.Center)
        actions.Children.Add edit
        actions.Children.Add more
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(actions, Dock.Right)
        dock.Children.Add actions
        dock.Children.Add column
        rowCard dock

    let root =
        for p in [ builtinPanel; mcpPanel; foldersPanel ] do p.Spacing <- 12.0
        let addButton = Ui.button "primary" "添加 MCP 服务" addServer.Trigger
        addButton.HorizontalAlignment <- HorizontalAlignment.Left
        let addFolderButton = Ui.button "secondary" "授权文件夹" addFolder.Trigger
        addFolderButton.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(addFolderButton, "tools-add-folder")
        let header = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        header.Children.Add(Ui.heading "工具")
        header.Children.Add(Ui.caption "对话设置里勾选的工具会随请求交给模型；每个工具可以设为自动执行、首次询问或每次询问。工具清单来自主机实际发现的结果。")
        let builtinStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        builtinStack.Children.Add(Ui.sectionLabel "可用工具")
        builtinStack.Children.Add builtinPanel
        let foldersStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        foldersStack.Children.Add(Ui.sectionLabel "授权文件夹")
        foldersStack.Children.Add foldersPanel
        foldersStack.Children.Add addFolderButton
        let mcpStack = StackPanel(Orientation = Orientation.Vertical, Spacing = 12.0)
        mcpStack.Children.Add(Ui.sectionLabel "MCP 服务")
        mcpStack.Children.Add addButton
        mcpStack.Children.Add mcpPanel
        let main = StackPanel(Orientation = Orientation.Vertical, Spacing = 32.0)
        main.Children.Add header
        main.Children.Add builtinStack
        main.Children.Add sandboxNote
        main.Children.Add foldersStack
        main.Children.Add debugRow
        main.Children.Add(Ui.hairline())
        main.Children.Add mcpStack
        main

    member _.View = root :> Control

    member _.Render(state: ToolsPaneState) =
        builtinPanel.Children.Clear()
        for t in state.Tools do builtinPanel.Children.Add(renderTool t)
        if state.Tools.IsEmpty then
            builtinPanel.Children.Add(Empty.card glyphOf "当前没有可用工具。内置文件工具需要主机配置沙箱根目录后才会出现。")
        mcpPanel.Children.Clear()
        for s in state.Servers do mcpPanel.Children.Add(renderServer s)
        if state.Servers.IsEmpty then
            mcpPanel.Children.Add(Empty.card glyphOf "还没有 MCP 服务。添加后它提供的工具会自动出现在上面的清单里。")
        foldersPanel.Children.Clear()
        for folder in state.Folders do
            let label = TextBlock(Text = folder, FontSize = 12.0)
            label.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Text") |> ignore
            label.Bind(TextBlock.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore
            label.TextTrimming <- TextTrimming.CharacterEllipsis
            label.VerticalAlignment <- VerticalAlignment.Center
            let remove = Ui.iconButton "🗑" "移除授权" (fun () -> removeFolder.Trigger folder)
            AutomationProperties.SetName(remove, sprintf "移除文件夹授权 %s" folder)
            let dock = DockPanel(LastChildFill = true)
            DockPanel.SetDock(remove, Dock.Right)
            dock.Children.Add remove
            dock.Children.Add label
            foldersPanel.Children.Add(Ui.card dock)
        sandboxNote.Text <- state.SandboxNote
        writeDebug state.ExposeDebug

    /// 工具 id 与审批方式键（组合者在键处弹「自动执行 / 首次询问 / 每次询问」菜单）
    member _.PolicyClicked = policyClicked.Publish
    member _.RemoveFolderClicked = removeFolder.Publish
    /// 用户拨了调试工具开关（新值）
    member _.DebugToggled = debugToggled.Publish
    member _.AddFolderClicked = addFolder.Publish
    member _.AddServerClicked = addServer.Publish
    member _.EditServerClicked = editServer.Publish
    /// 服务 id 与「更多」键（组合者弹「停用/启用、删除」菜单）
    member _.ServerMoreClicked = serverMore.Publish

/// MCP 服务编辑器的现有值
type McpEditorValues =
    { Id: string
      Label: string
      Command: string
      Args: string
      Url: string
      Timeout: string
      Enabled: bool }

/// 添加 / 编辑 MCP 服务的对话框内容（旧 SettingsTools.ShowEditor 的界面部分）。
/// validationFor 给每个输入框的校验行（组合者的 FieldCheck.MessageFor）；校验、保存往返、关闭由组合者接。
type McpEditor(existing: McpEditorValues option, validationFor: TextBox -> Control option) =
    let save = Event<unit>()
    let cancel = Event<unit>()
    let mutable pending = false
    let field label hint (placeholder: string) =
        let box = Ui.textField placeholder
        box, Ui.fieldGroup label hint box (validationFor box)
    let idBox, idField = field "稳定标识" "仅限字母、数字、下划线与连字符。" "例如 filesystem"
    let labelBox, labelField = field "显示名称" "仅用于界面显示。" "在界面上怎么称呼它"
    let commandBox, commandField = field "本地命令" "本地启动命令；与远程端点二选一。" "例如 npx"
    let argsBox, argsField = field "命令参数" "每行一个参数。" "每行一个参数"
    let urlBox, urlField = field "远程端点" "http(s) 地址；与本地命令二选一。" "https://example.com/mcp"
    let timeoutBox, timeoutField = field "单次调用超时（秒）" "1–3600 秒。" "60"
    let summary = Ui.caption ""
    let enabledRow, readEnabled, _ =
        Ui.switchRow "启用这个 MCP 服务" "" (existing |> Option.map (fun v -> v.Enabled) |> Option.defaultValue true) ignore
    let idleText = if existing.IsSome then "保存" else "添加"
    let cancelButton = Ui.button "ghost" "取消" cancel.Trigger
    let saveButton = Ui.button "primary" idleText (fun () -> if not pending then save.Trigger())
    let trySave () = if not pending then save.Trigger()

    let form =
        argsBox.AcceptsReturn <- true
        argsBox.TextWrapping <- TextWrapping.Wrap
        argsBox.VerticalContentAlignment <- VerticalAlignment.Top
        argsBox.Bind(Layoutable.MinHeightProperty, Dyn.res "Metric.TextAreaMinHeight") |> ignore
        summary.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
        summary.IsVisible <- false
        summary.Focusable <- true
        AutomationProperties.SetName(summary, "表单错误摘要")
        AutomationProperties.SetLiveSetting(summary, AutomationLiveSetting.Assertive)
        match existing with
        | Some v ->
            idBox.Text <- v.Id
            idBox.IsEnabled <- false
            labelBox.Text <- v.Label
            commandBox.Text <- v.Command
            argsBox.Text <- v.Args
            urlBox.Text <- v.Url
            timeoutBox.Text <- v.Timeout
        | None -> timeoutBox.Text <- "60"
        AutomationProperties.SetHelpText(cancelButton, "取消并关闭 MCP 服务编辑 (Esc)")
        ToolTip.SetTip(cancelButton, "取消并关闭 (Esc)")
        AutomationProperties.SetHelpText(saveButton, sprintf "确认%s MCP 服务设置（Ctrl / ⌘ + Enter）" idleText)
        ToolTip.SetTip(saveButton, sprintf "确认%s（Ctrl / ⌘ + Enter）" idleText)
        saveButton.MinWidth <- 96.0
        let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        row.Children.Add cancelButton
        row.Children.Add saveButton
        row.HorizontalAlignment <- HorizontalAlignment.Right
        // 页脚左右键：与其它对话框同款
        cancelButton.KeyDown.Add(fun e ->
            if e.Key = Key.Right && saveButton.IsEnabled then
                e.Handled <- true
                saveButton.Focus(NavigationMethod.Directional) |> ignore)
        saveButton.KeyDown.Add(fun e ->
            if e.Key = Key.Left && cancelButton.IsEnabled then
                e.Handled <- true
                cancelButton.Focus(NavigationMethod.Directional) |> ignore)
        let ctrl (e: KeyEventArgs) = e.KeyModifiers.HasFlag KeyModifiers.Control || e.KeyModifiers.HasFlag KeyModifiers.Meta
        // 单行字段回车即提交；多行参数框的回车是换行
        for box in [ idBox; labelBox; commandBox; urlBox; timeoutBox ] do
            box.KeyDown.Add(fun e ->
                if e.Key = Key.Enter && not (ctrl e) then
                    e.Handled <- true
                    trySave ())
        let form = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        form.Children.Add(Ui.title (if existing.IsSome then "编辑 MCP 服务" else "添加 MCP 服务"))
        form.Children.Add(Ui.caption "本地 MCP 服务以子进程方式启动（stdio）；远程 MCP 服务走 Streamable HTTP。")
        form.Children.Add summary
        form.Children.Add idField
        form.Children.Add labelField
        form.Children.Add commandField
        form.Children.Add argsField
        form.Children.Add urlField
        form.Children.Add timeoutField
        form.Children.Add enabledRow
        form.Children.Add(Ui.hairline())
        form.Children.Add row
        form.KeyDown.Add(fun e ->
            if e.Key = Key.Enter && ctrl e then
                e.Handled <- true
                trySave ())
        ScrollViewer(
            Content = form,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto)

    member _.View = form :> Control
    member _.IdBox = idBox
    member _.LabelBox = labelBox
    member _.CommandBox = commandBox
    member _.ArgsBox = argsBox
    member _.UrlBox = urlBox
    member _.TimeoutBox = timeoutBox
    member _.Boxes = [ idBox; labelBox; commandBox; argsBox; urlBox; timeoutBox ]
    member _.Summary = summary
    member _.Enabled = readEnabled ()
    /// 首个焦点：新建落在标识，编辑落在名称（标识只读）
    member _.InitialFocus = (if existing.IsSome then labelBox else idBox) :> Control
    /// 对话框最高（组合者给 ScrollViewer 的上限）
    member _.SetMaxHeight(h: float) = form.MaxHeight <- h
    member _.IsPending = pending

    /// 保存往返中：保存键换「正在保存…」并禁用，取消也禁用；重复的 Ctrl+Enter 不再提交
    member _.SetPending(value: bool) =
        pending <- value
        let text = if value then "正在保存…" else idleText
        saveButton.SetText text
        AutomationProperties.SetName(saveButton, text)
        saveButton.SetEnabled(not value)
        AutomationProperties.SetHelpText(saveButton, (if value then text else sprintf "确认%s MCP 服务设置（Ctrl / ⌘ + Enter）" idleText))
        cancelButton.SetEnabled(not value)

    member _.SaveRequested = save.Publish
    member _.CancelRequested = cancel.Publish

/// 「授权文件夹」对话框内容（旧 SettingsTools.ShowAddFolder 的界面部分）
type FolderEditor(validationFor: TextBox -> Control option) =
    let save = Event<string>()
    let cancel = Event<unit>()
    let pathBox = Ui.textField "例如 /home/me/文档"
    let field = Ui.fieldGroup "文件夹的绝对路径" "模型只能读取这个文件夹以内的文件，不能读到它之外。" pathBox (validationFor pathBox)
    let text () = if isNull pathBox.Text then "" else pathBox.Text.Trim()
    let cancelButton = Ui.button "ghost" "取消" cancel.Trigger
    let confirm = Ui.button "primary" "授权" (fun () -> save.Trigger(text ()))
    let form =
        let buttons = StackPanel(Orientation = Orientation.Horizontal, Spacing = 12.0)
        buttons.Children.Add cancelButton
        buttons.Children.Add confirm
        buttons.HorizontalAlignment <- HorizontalAlignment.Right
        pathBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                save.Trigger(text ()))
        let p = StackPanel(Orientation = Orientation.Vertical, Spacing = 16.0)
        p.Children.Add(Ui.title "授权文件夹")
        p.Children.Add(Ui.caption "授权后，读取文件、列出文件夹这两个工具才会出现在工具清单里。")
        p.Children.Add field
        p.Children.Add buttons
        p

    member _.View = form :> Control
    member _.PathBox = pathBox
    /// 去首尾空白的路径
    member _.SaveRequested = save.Publish
    member _.CancelRequested = cancel.Publish
