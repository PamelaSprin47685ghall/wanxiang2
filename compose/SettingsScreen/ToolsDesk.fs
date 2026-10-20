namespace Wanxiang2.SettingsScreen

open System
open Avalonia.Controls
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemeMetrics
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.FieldCheck
open Wanxiang2.IconSet
open Wanxiang2.ToolsPane

/// 设置 → 工具的协作（旧 Views/SettingsTools.fs）：工具审批方式、授权文件夹（空 / 非绝对 / 重复校验）、调试工具开关、
/// MCP 服务编辑（McpForm 校验 + FieldCheck）与更多菜单。工具设置走 UpdateGeneration（ToolsPolicy 载荷）。界面归 ToolsPane。
type ToolsDesk(theme: ThemeManager, overlay: OverlayDesk, hooks: unit -> SettingsHooks) =
    let pane = ToolsPane(SettingsLooks.glyphOf theme)
    let check = FieldCheck()
    let mutable catalog = Catalog.empty
    let mutable mcpEditor: McpEditor option = None
    let mutable folderEditor: FolderEditor option = None
    let icon g = fun brush -> IconSet.image g brush (theme.Space Space.GlyphMd)
    let toast msg tone = overlay.Toast(msg, tone)
    let validation (box: TextBox) = Some(check.MessageFor box :> Control)

    let saveTools roots policyDefault policy exposeDebug (onDone: bool -> unit) =
        let g = catalog.generation
        let submitted = { g with fileReadRoots = roots; toolPolicyDefault = policyDefault; toolPolicy = policy; exposeDebug = exposeDebug }
        (hooks ()).UpdateGeneration (ToolsPolicy.payload roots policyDefault policy exposeDebug) (fun ok ->
            if ok then catalog <- { catalog with generation = submitted }
            else toast "保存失败，请重试。" ToastTone.Failure
            onDone ok)

    let render () =
        let g = catalog.generation
        pane.Render
            { Tools =
                [ for t in catalog.tools ->
                      { Id = t.id
                        Label = t.label
                        Description = t.description
                        FromMcp = (t.source = "mcp")
                        PolicyLabel = ToolsPolicy.label (g.toolPolicy |> Map.tryFind t.id |> Option.defaultValue g.toolPolicyDefault) } ]
              Folders = g.fileReadRoots
              SandboxNote =
                (if List.isEmpty g.fileReadRoots then
                     "文件读取工具当前不可用：还没有授权任何文件夹。这是有意的——没有授权范围就等于把整台机器的文件交给模型。点「授权文件夹」添加。"
                 else "文件读取工具的沙箱根目录：" + String.Join("、", g.fileReadRoots))
              ExposeDebug = g.exposeDebug
              Servers =
                [ for s in catalog.mcpServers ->
                      { Id = s.id
                        Label = s.label
                        ToolCount = catalog.tools |> List.filter (fun t -> t.serverId = Some s.id) |> List.length
                        Transport =
                          (match s.command, s.url with
                           | Some command, _ -> sprintf "本地 · %s %s" command (String.Join(" ", s.args))
                           | _, Some url -> sprintf "远程 · %s" url
                           | _ -> "未配置传输")
                        Enabled = s.enabled } ] }

    let showEditor (existing: McpInfo option) =
        let taken = catalog.mcpServers |> List.map (fun s -> s.id)
        let values =
            existing
            |> Option.map (fun s ->
                { Id = s.id
                  Label = s.label
                  Command = s.command |> Option.defaultValue ""
                  Args = String.Join("\n", s.args)
                  Url = s.url |> Option.defaultValue ""
                  Timeout = string s.callTimeoutSeconds
                  Enabled = s.enabled })
        let editor = McpEditor(values, validation)
        let mutable active = true
        FieldCheck.BindSummaryReset(Some editor.Summary, editor.Boxes)
        let text (box: TextBox) = if isNull box.Text then "" else box.Text
        editor.SaveRequested.Add(fun () ->
            for box in editor.Boxes do check.Clear box
            editor.Summary.IsVisible <- false
            editor.Summary.Text <- ""
            let v, fieldErrors =
                McpForm.validate existing.IsNone taken (text editor.IdBox) (text editor.LabelBox) (text editor.CommandBox) (text editor.ArgsBox) (text editor.UrlBox) (text editor.TimeoutBox)
            let boxOf field =
                match field with
                | McpField.Id -> editor.IdBox
                | McpField.Label -> editor.LabelBox
                | McpField.Command -> editor.CommandBox
                | McpField.Url -> editor.UrlBox
                | McpField.Timeout -> editor.TimeoutBox
            let errors = fieldErrors |> List.map (fun (f, m) -> boxOf f, m)
            if errors.IsEmpty then
                editor.ApplyPending true
                (hooks ()).UpsertMcp (McpForm.payload v editor.Enabled) (fun ok ->
                    editor.ApplyPending false
                    if ok && active then overlay.CloseDialog()
                    elif not ok then toast "保存失败，请重试。" ToastTone.Failure)
            else
                for box, m in errors do check.ApplyError(box, m)
                FieldCheck.Apply(errors, Some editor.Summary) |> Option.iter (fun m -> toast m ToastTone.Warning))
        editor.CancelRequested.Add(fun () -> overlay.CloseDialog())
        let height = (theme.Metric Metric.DialogContentMaxHeight).Subscribe(fun h -> editor.ApplyMaxHeight h)
        mcpEditor <- Some editor
        overlay.ShowDialog(
            editor.View,
            (theme.Metric Metric.SettingsDialogWidth).Value,
            onClosed = (fun () -> active <- false; height.Dispose(); mcpEditor <- None),
            canDismiss = (fun () -> not editor.IsPending),
            initialFocus = editor.InitialFocus)

    let showAddFolder () =
        let editor = FolderEditor(validation)
        let mutable pending = false
        editor.SaveRequested.Add(fun raw ->
            if not pending then
                check.Clear editor.PathBox
                let roots = catalog.generation.fileReadRoots
                if String.IsNullOrWhiteSpace raw then check.ApplyError(editor.PathBox, "请填写文件夹路径。")
                elif not (IO.Path.IsPathRooted raw) then check.ApplyError(editor.PathBox, "需要绝对路径，例如 /home/me/文档。")
                elif List.contains raw roots then check.ApplyError(editor.PathBox, "这个文件夹已经授权过了。")
                else
                    pending <- true
                    let g = catalog.generation
                    saveTools (roots @ [ raw ]) g.toolPolicyDefault g.toolPolicy g.exposeDebug (fun ok ->
                        pending <- false
                        if ok then overlay.CloseDialog()))
        editor.CancelRequested.Add(fun () -> overlay.CloseDialog())
        folderEditor <- Some editor
        overlay.ShowDialog(
            editor.View,
            (theme.Metric Metric.SettingsSmallDialogWidth).Value,
            onClosed = (fun () -> folderEditor <- None),
            canDismiss = (fun () -> not pending),
            initialFocus = editor.PathBox)

    do
        pane.PolicyClicked.Add(fun (id, anchor) ->
            let g = catalog.generation
            let current = g.toolPolicy |> Map.tryFind id |> Option.defaultValue g.toolPolicyDefault
            overlay.ShowMenu(
                anchor,
                true,
                [ for value in [ "auto"; "first"; "always" ] ->
                      PopupEntry.create ((if value = current then "✓ " else "") + ToolsPolicy.label value) (fun () ->
                          let g = catalog.generation
                          saveTools g.fileReadRoots g.toolPolicyDefault (g.toolPolicy |> Map.add id value) g.exposeDebug ignore) ]))
        pane.RemoveFolderClicked.Add(fun root ->
            let g = catalog.generation
            saveTools (g.fileReadRoots |> List.filter (fun r -> r <> root)) g.toolPolicyDefault g.toolPolicy g.exposeDebug ignore)
        pane.DebugToggled.Add(fun value ->
            let g = catalog.generation
            if value <> g.exposeDebug then saveTools g.fileReadRoots g.toolPolicyDefault g.toolPolicy value ignore)
        pane.AddFolderClicked.Add(fun () -> showAddFolder ())
        pane.AddServerClicked.Add(fun () -> showEditor None)
        pane.EditServerClicked.Add(fun id -> catalog.mcpServers |> List.tryFind (fun s -> s.id = id) |> Option.iter (Some >> showEditor))
        pane.ServerMoreClicked.Add(fun (id, anchor) ->
            match catalog.mcpServers |> List.tryFind (fun s -> s.id = id) with
            | None -> ()
            | Some s ->
                overlay.ShowMenu(
                    anchor,
                    true,
                    [ PopupEntry.create (if s.enabled then "停用" else "启用") (fun () ->
                          let v =
                              { id = s.id; label = s.label; command = s.command |> Option.defaultValue ""; args = s.args
                                url = s.url |> Option.defaultValue ""; timeout = s.callTimeoutSeconds }
                          (hooks ()).UpsertMcp (McpForm.payload v (not s.enabled)) ignore)
                      PopupEntry.create "删除" (fun () -> (hooks ()).DeleteMcp s.id)
                      |> PopupEntry.withIcon (icon Glyph.Trash)
                      |> PopupEntry.asDanger ]))
        render ()

    member _.View = pane.View
    member _.Pane = pane
    member _.McpEditor = mcpEditor
    member _.FolderEditor = folderEditor
    member _.Catalog = catalog

    /// 单入口幂等渲染：由 SettingsToolsVS 投影全量驱动
    member this.Render(vs: SettingsToolsVS) =
        catalog <- vs.Catalog
        render ()
