namespace Wanxiang2.ToolsPane

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

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
    let card (kit: Kit) (look: ToolsPaneLook) (hint: string) : Control =
        let logo = look.Glyph "logo" look.Muted
        kit.Live(look.EmptyLogoSize, fun v -> logo.Width <- v; logo.Height <- v)
        logo.HorizontalAlignment <- HorizontalAlignment.Center
        let text = kit.Caption hint
        text.TextAlignment <- TextAlignment.Center
        kit.Card(look.Space4xl, look.Space4xl, look.RadiusLg, kit.VStack(look.SpaceMd, [ logo; text ])) :> Control

/// 设置 → 工具分区（迁自旧 Views/SettingsTools.fs 的页面部分）：可用工具与审批方式键、沙箱说明、授权文件夹、调试工具开关、MCP 服务列表。
/// 写回、菜单、对话框由组合者接（ToolsPolicy、McpForm、OverlayDesk、PopupMenu）。
type ToolsPane(look: ToolsPaneLook) =
    let kit = Kit(look)
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
    let sandboxNote = kit.Caption ""
    let debugRow, _, writeDebug =
        kit.SwitchRow("显示调试工具", "打开后，工具清单里会多出一个只用于排查问题的回显工具。", false, (fun v -> debugToggled.Trigger v))

    let rowCard (child: Control) = kit.Card(look.Space2xl, look.SpaceXl, look.RadiusMd, child) :> Control

    let renderTool (tool: ToolsPaneTool) : Control =
        let icon = look.Glyph (if tool.FromMcp then "server" else "wrench") look.Muted
        icon.VerticalAlignment <- VerticalAlignment.Top
        let name = kit.Text(tool.Label, look.BodyFontSize, look.Text)
        name.FontWeight <- FontWeight.Medium
        let identifier = kit.Text(tool.Id, look.MicroFontSize, look.Faint)
        kit.Live(look.MonoFont, fun f -> identifier.FontFamily <- f)
        let description = kit.Caption(if String.IsNullOrWhiteSpace tool.Description then "（MCP 服务未提供描述）" else tool.Description)
        let column = kit.VStack(look.SpaceXXs, [ name :> Control; identifier; description ])
        let mutable policy: Tap = Unchecked.defaultof<_>
        policy <- kit.Button(Tone.Secondary, tool.PolicyLabel, (fun () -> policyClicked.Trigger((tool.Id, policy :> Control))))
        AutomationProperties.SetAutomationId(policy, sprintf "tool-policy-%s" tool.Id)
        AutomationProperties.SetName(policy, sprintf "%s 的审批方式：%s" tool.Label tool.PolicyLabel)
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(policy, Dock.Right)
        dock.Children.Add policy
        dock.Children.Add(kit.HStack(look.SpaceXl, [ icon; column ]))
        rowCard dock

    let renderServer (server: ToolsPaneServer) : Control =
        let name = kit.Text(server.Label, look.BodyFontSize, (if server.Enabled then look.Text else look.Muted))
        name.FontWeight <- FontWeight.Medium
        name.VerticalAlignment <- VerticalAlignment.Center
        let titleRow = kit.HStack(look.SpaceMd, [ name ])
        titleRow.Children.Add(kit.Tag(TagTone.Neutral, sprintf "%d 个工具" server.ToolCount))
        if not server.Enabled then titleRow.Children.Add(kit.Tag(TagTone.Neutral, "已停用"))
        let meta = kit.Text(server.Transport, look.MicroFontSize, look.Faint)
        meta.TextTrimming <- TextTrimming.CharacterEllipsis
        let column = kit.VStack(look.SpaceXXs, [ titleRow :> Control; meta ])
        let edit = kit.IconButton("pencil", "编辑", (fun () -> editServer.Trigger server.Id))
        let mutable more: Tap = Unchecked.defaultof<_>
        more <- kit.IconButton("more", "更多", (fun () -> serverMore.Trigger((server.Id, more :> Control))))
        let actions = kit.HStack(look.SpaceXs, [ edit :> Control; more ])
        let dock = DockPanel(LastChildFill = true)
        DockPanel.SetDock(actions, Dock.Right)
        dock.Children.Add actions
        dock.Children.Add column
        rowCard dock

    let root =
        for p in [ builtinPanel; mcpPanel; foldersPanel ] do kit.Live(look.SpaceMd, fun v -> p.Spacing <- v)
        let addButton = kit.Button(Tone.Primary, "添加 MCP 服务", addServer.Trigger)
        addButton.HorizontalAlignment <- HorizontalAlignment.Left
        let addFolderButton = kit.Button(Tone.Secondary, "授权文件夹", addFolder.Trigger)
        addFolderButton.HorizontalAlignment <- HorizontalAlignment.Left
        AutomationProperties.SetAutomationId(addFolderButton, "tools-add-folder")
        kit.VStack(
            look.Space4xl,
            [ kit.VStack(
                  look.SpaceXs,
                  [ kit.Heading "工具" :> Control
                    kit.Caption "对话设置里勾选的工具会随请求交给模型；每个工具可以设为自动执行、首次询问或每次询问。工具清单来自主机实际发现的结果。" ])
              :> Control
              kit.VStack(look.SpaceMd, [ kit.SectionLabel "可用工具" :> Control; builtinPanel ])
              sandboxNote
              kit.VStack(look.SpaceMd, [ kit.SectionLabel "授权文件夹" :> Control; foldersPanel; addFolderButton ])
              debugRow
              kit.Hairline()
              kit.VStack(look.SpaceMd, [ kit.SectionLabel "MCP 服务" :> Control; addButton; mcpPanel ]) ])

    member _.View = root :> Control

    member _.Render(state: ToolsPaneState) =
        builtinPanel.Children.Clear()
        for t in state.Tools do builtinPanel.Children.Add(renderTool t)
        if state.Tools.IsEmpty then
            builtinPanel.Children.Add(Empty.card kit look "当前没有可用工具。内置文件工具需要主机配置沙箱根目录后才会出现。")
        mcpPanel.Children.Clear()
        for s in state.Servers do mcpPanel.Children.Add(renderServer s)
        if state.Servers.IsEmpty then
            mcpPanel.Children.Add(Empty.card kit look "还没有 MCP 服务。添加后它提供的工具会自动出现在上面的清单里。")
        foldersPanel.Children.Clear()
        for folder in state.Folders do
            let label = kit.Text(folder, look.CaptionFontSize, look.Text)
            kit.Live(look.MonoFont, fun f -> label.FontFamily <- f)
            label.TextTrimming <- TextTrimming.CharacterEllipsis
            label.VerticalAlignment <- VerticalAlignment.Center
            let remove = kit.IconButton("trash", "移除授权", (fun () -> removeFolder.Trigger folder))
            AutomationProperties.SetName(remove, sprintf "移除文件夹授权 %s" folder)
            let dock = DockPanel(LastChildFill = true)
            DockPanel.SetDock(remove, Dock.Right)
            dock.Children.Add remove
            dock.Children.Add label
            foldersPanel.Children.Add(kit.Card(look.Space2xl, look.SpaceMd, look.RadiusMd, dock))
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
type McpEditor(look: ToolsPaneLook, existing: McpEditorValues option, validationFor: TextBox -> Control option) =
    let kit = Kit(look)
    let save = Event<unit>()
    let cancel = Event<unit>()
    let mutable pending = false
    let field label hint (placeholder: string) =
        let shell, box = kit.TextField placeholder
        box, kit.FieldGroup(label, hint, shell, validationFor box)
    let idBox, idField = field "稳定标识" "仅限字母、数字、下划线与连字符。" "例如 filesystem"
    let labelBox, labelField = field "显示名称" "仅用于界面显示。" "在界面上怎么称呼它"
    let commandBox, commandField = field "本地命令" "本地启动命令；与远程端点二选一。" "例如 npx"
    let argsBox, argsField = field "命令参数" "每行一个参数。" "每行一个参数"
    let urlBox, urlField = field "远程端点" "http(s) 地址；与本地命令二选一。" "https://example.com/mcp"
    let timeoutBox, timeoutField = field "单次调用超时（秒）" "1–3600 秒。" "60"
    let summary = kit.Caption ""
    let enabledRow, readEnabled, _ =
        kit.SwitchRow("启用这个 MCP 服务", "", (existing |> Option.map (fun v -> v.Enabled) |> Option.defaultValue true), ignore)
    let idleText = if existing.IsSome then "保存" else "添加"
    let cancelButton = kit.Button(Tone.Ghost, "取消", cancel.Trigger)
    let saveButton = kit.Button(Tone.Primary, idleText, (fun () -> if not pending then save.Trigger()))
    let trySave () = if not pending then save.Trigger()

    let form =
        argsBox.AcceptsReturn <- true
        argsBox.TextWrapping <- TextWrapping.Wrap
        argsBox.VerticalContentAlignment <- VerticalAlignment.Top
        kit.Live(look.TextAreaMinHeight, fun v -> argsBox.MinHeight <- v)
        summary.Foreground <- look.Danger
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
        kit.Live(look.PendingMinWidth, fun v -> saveButton.MinWidth <- v)
        let row = kit.HStack(look.SpaceMd, [ cancelButton :> Control; saveButton ])
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
        let form =
            kit.VStack(
                look.SpaceXl,
                [ kit.Title(if existing.IsSome then "编辑 MCP 服务" else "添加 MCP 服务") :> Control
                  kit.Caption "本地 MCP 服务以子进程方式启动（stdio）；远程 MCP 服务走 Streamable HTTP。"
                  summary
                  idField
                  labelField
                  commandField
                  argsField
                  urlField
                  timeoutField
                  enabledRow
                  kit.Hairline()
                  row ])
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
type FolderEditor(look: ToolsPaneLook, validationFor: TextBox -> Control option) =
    let kit = Kit(look)
    let save = Event<string>()
    let cancel = Event<unit>()
    let shell, pathBox = kit.TextField "例如 /home/me/文档"
    let field = kit.FieldGroup("文件夹的绝对路径", "模型只能读取这个文件夹以内的文件，不能读到它之外。", shell, validationFor pathBox)
    let text () = if isNull pathBox.Text then "" else pathBox.Text.Trim()
    let cancelButton = kit.Button(Tone.Ghost, "取消", cancel.Trigger)
    let confirm = kit.Button(Tone.Primary, "授权", (fun () -> save.Trigger(text ())))
    let form =
        let buttons = kit.HStack(look.SpaceMd, [ cancelButton :> Control; confirm ])
        buttons.HorizontalAlignment <- HorizontalAlignment.Right
        pathBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                e.Handled <- true
                save.Trigger(text ()))
        kit.VStack(
            look.SpaceXl,
            [ kit.Title "授权文件夹" :> Control
              kit.Caption "授权后，读取文件、列出文件夹这两个工具才会出现在工具清单里。"
              field
              buttons ])

    member _.View = form :> Control
    member _.PathBox = pathBox
    /// 去首尾空白的路径
    member _.SaveRequested = save.Publish
    member _.CancelRequested = cancel.Publish
